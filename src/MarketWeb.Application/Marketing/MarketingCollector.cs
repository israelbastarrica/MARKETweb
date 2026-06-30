using System.Text;
using System.Text.Json;
using Dapper;
using MarketWeb.Application.Data;

namespace MarketWeb.Application.Marketing;

public interface IMarketingCollector
{
    /// <summary>Recolecta FB+IG (perfil + posts + métricas) y los guarda en MKT_Redes*. Token desde MKT_Config.</summary>
    Task<(bool Ok, string Resultado)> RecolectarAsync(int limite, CancellationToken ct = default);
}

/// <summary>
/// Port C# del colector Python (Meta Graph API → FB/IG). Corre en el scheduler de MarketWeb
/// (tarea REDES). El token de Meta lo lee de MKT_Config (lo deja ahí el colector/renovador Python),
/// así sobrevive la rotación del token sin tocar config del server.
/// </summary>
public sealed class MarketingCollector : IMarketingCollector
{
    private readonly ISqlConnectionFactory _db;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private const string API = "https://graph.facebook.com/v22.0";
    private static readonly TimeZoneInfo TzAr = ResolverTz();

    public MarketingCollector(ISqlConnectionFactory db) => _db = db;

    private static TimeZoneInfo ResolverTz()
    {
        foreach (var id in new[] { "Argentina Standard Time", "America/Argentina/Buenos_Aires" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.CreateCustomTimeZone("AR", TimeSpan.FromHours(-3), "AR", "AR");
    }

    private const string Ddl = @"
IF OBJECT_ID('dbo.MKT_RedesPublicaciones') IS NULL
CREATE TABLE dbo.MKT_RedesPublicaciones (
    Id INT IDENTITY(1,1) PRIMARY KEY, Red VARCHAR(4) NOT NULL, PostId VARCHAR(120) NOT NULL,
    Tipo VARCHAR(40) NULL, FechaPublicacion DATETIME NULL, Texto NVARCHAR(MAX) NULL, Permalink NVARCHAR(600) NULL,
    FechaAlta DATETIME NOT NULL DEFAULT GETDATE(), Eliminado BIT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_MKT_RedesPub UNIQUE (Red, PostId));
IF OBJECT_ID('dbo.MKT_RedesMetricas') IS NULL
CREATE TABLE dbo.MKT_RedesMetricas (
    Id INT IDENTITY(1,1) PRIMARY KEY, PublicacionId INT NOT NULL, Fecha DATE NOT NULL, FechaHora DATETIME NOT NULL,
    Alcance INT NULL, Reproducciones INT NULL, MeGusta INT NULL, Comentarios INT NULL, Compartidos INT NULL,
    Guardados INT NULL, Interacciones INT NULL, TiempoPromedioSeg DECIMAL(10,1) NULL,
    CONSTRAINT UQ_MKT_RedesMet UNIQUE (PublicacionId, Fecha),
    CONSTRAINT FK_MKT_RedesMet FOREIGN KEY (PublicacionId) REFERENCES dbo.MKT_RedesPublicaciones(Id));
IF OBJECT_ID('dbo.MKT_RedesPerfil') IS NULL
CREATE TABLE dbo.MKT_RedesPerfil (
    Id INT IDENTITY(1,1) PRIMARY KEY, Red VARCHAR(4) NOT NULL, Fecha DATE NOT NULL, FechaHora DATETIME NOT NULL,
    Seguidores INT NULL, Publicaciones INT NULL, CONSTRAINT UQ_MKT_RedesPerfil UNIQUE (Red, Fecha));
IF COL_LENGTH('dbo.MKT_RedesMetricas','ImpresionesPago') IS NULL ALTER TABLE dbo.MKT_RedesMetricas ADD ImpresionesPago INT NULL;
IF COL_LENGTH('dbo.MKT_RedesMetricas','ImpresionesOrganico') IS NULL ALTER TABLE dbo.MKT_RedesMetricas ADD ImpresionesOrganico INT NULL;";

    public async Task<(bool Ok, string Resultado)> RecolectarAsync(int limite, CancellationToken ct = default)
    {
        if (limite <= 0 || limite > 100) limite = 25;
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(Ddl, cancellationToken: ct));

        var token = await ConfigAsync(cn, "META_ACCESS_TOKEN", ct);
        var igUser = await ConfigAsync(cn, "META_IG_USER_ID", ct);
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(igUser))
            return (false, "Falta META_ACCESS_TOKEN / META_IG_USER_ID en MKT_Config (corré el colector Python una vez para sembrarlos).");

        var ahora = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TzAr).DateTime;
        var hoy = ahora.Date;

        var (igFollowers, igMedia, igPosts) = await FetchInstagramAsync(token!, igUser!, limite, ct);
        var (fbFollowers, _, fbPosts) = await FetchFacebookAsync(token!, limite, ct);

        await UpsertPerfilAsync(cn, "IG", igFollowers, igMedia, hoy, ahora, ct);
        // FB no tiene media_count directo; no usamos fan_count como publicaciones (≈ seguidores, confunde).
        await UpsertPerfilAsync(cn, "FB", fbFollowers, null, hoy, ahora, ct);

        int n = 0;
        foreach (var (red, posts) in new[] { ("IG", igPosts), ("FB", fbPosts) })
            foreach (var p in posts)
            {
                var pubId = await UpsertPublicacionAsync(cn, red, p, ct);
                await UpsertMetricasAsync(cn, pubId, p.Met, hoy, ahora, ct);
                n++;
            }

        return (true, $"OK: {n} publicaciones (IG {igPosts.Count} + FB {fbPosts.Count}) · IG {igFollowers} seg · FB {fbFollowers} seg.");
    }

    // ---------------- Meta Graph API ----------------
    private sealed class Post
    {
        public string PostId = "";
        public string? Tipo;
        public DateTime? Fecha;
        public string? Texto;
        public string? Permalink;
        public Dictionary<string, int?> Met = new();
        public decimal? TiempoPromedioSeg;
    }

    private async Task<JsonElement> GetAsync(string path, IEnumerable<(string, string)> pars, string token, CancellationToken ct)
    {
        var sb = new StringBuilder($"{API}/{path}?");
        foreach (var (k, v) in pars) sb.Append($"{Uri.EscapeDataString(k)}={Uri.EscapeDataString(v)}&");
        sb.Append($"access_token={Uri.EscapeDataString(token)}");
        using var resp = await Http.GetAsync(sb.ToString(), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();
        if (root.TryGetProperty("error", out var err))
            throw new Exception($"API {Str(err, "code")}: {Str(err, "message")}");
        return root;
    }

    private async Task<(int? followers, int media, List<Post> posts)> FetchInstagramAsync(string token, string igUser, int limite, CancellationToken ct)
    {
        var perfil = await GetAsync(igUser, new[] { ("fields", "username,followers_count,media_count") }, token, ct);
        var media = await GetAsync($"{igUser}/media", new[]
        {
            ("fields", "id,caption,media_type,media_product_type,timestamp,permalink,like_count,comments_count"),
            ("limit", limite.ToString())
        }, token, ct);

        var posts = new List<Post>();
        if (media.TryGetProperty("data", out var arr))
            foreach (var m in arr.EnumerateArray())
            {
                var met = new Dictionary<string, int?>
                {
                    ["MeGusta"] = Int(m, "like_count"),
                    ["Comentarios"] = Int(m, "comments_count"),
                };
                var id = Str(m, "id") ?? "";
                try
                {
                    var ins = await GetAsync($"{id}/insights", new[] { ("metric", "reach,saved,shares,total_interactions") }, token, ct);
                    var v = Insights(ins);
                    met["Alcance"] = v.GetValueOrDefault("reach");
                    met["Guardados"] = v.GetValueOrDefault("saved");
                    met["Compartidos"] = v.GetValueOrDefault("shares");
                    met["Interacciones"] = v.GetValueOrDefault("total_interactions");
                }
                catch
                {
                    try
                    {
                        var ins = await GetAsync($"{id}/insights", new[] { ("metric", "reach") }, token, ct);
                        met["Alcance"] = Insights(ins).GetValueOrDefault("reach");
                    }
                    catch { }
                }
                posts.Add(new Post
                {
                    PostId = id,
                    Tipo = Str(m, "media_product_type") ?? Str(m, "media_type"),
                    Fecha = ParseFecha(Str(m, "timestamp")),
                    Texto = Str(m, "caption"),
                    Permalink = Str(m, "permalink"),
                    Met = met
                });
            }
        return (Int(perfil, "followers_count"), Int(perfil, "media_count") ?? 0, posts);
    }

    private async Task<(int? followers, int? fans, List<Post> posts)> FetchFacebookAsync(string token, int limite, CancellationToken ct)
    {
        var acc = await GetAsync("me/accounts", new[] { ("fields", "name,id,access_token") }, token, ct);
        var pg = acc.GetProperty("data").EnumerateArray().First();
        var pid = Str(pg, "id")!;
        var ptoken = Str(pg, "access_token")!;

        var perfil = await GetAsync(pid, new[] { ("fields", "followers_count,fan_count") }, ptoken, ct);
        var raw = await GetAsync($"{pid}/published_posts", new[]
        {
            ("fields", "id,message,created_time,permalink_url,shares,reactions.summary(true).limit(0),comments.summary(true).limit(0)"),
            ("limit", limite.ToString())
        }, ptoken, ct);

        var posts = new List<Post>();
        if (raw.TryGetProperty("data", out var arr))
            foreach (var p in arr.EnumerateArray())
            {
                var id = Str(p, "id") ?? "";
                var met = new Dictionary<string, int?>
                {
                    ["MeGusta"] = IntPath(p, "reactions", "summary", "total_count"),
                    ["Comentarios"] = IntPath(p, "comments", "summary", "total_count"),
                    ["Compartidos"] = IntPath(p, "shares", "count") ?? 0,
                };
                decimal? tiempo = null;
                try
                {
                    var ins = await GetAsync($"{id}/insights", new[] { ("metric", "post_video_views,post_video_avg_time_watched") }, ptoken, ct);
                    var v = Insights(ins);
                    met["Reproducciones"] = v.GetValueOrDefault("post_video_views");
                    var avg = v.GetValueOrDefault("post_video_avg_time_watched");
                    tiempo = avg.HasValue ? Math.Round(avg.Value / 1000m, 1) : (decimal?)null;
                }
                catch { }
                try
                {
                    var ins = await GetAsync($"{id}/insights", new[] { ("metric", "post_impressions_unique,post_impressions_paid,post_impressions_organic") }, ptoken, ct);
                    var v = Insights(ins);
                    met["Alcance"] = v.GetValueOrDefault("post_impressions_unique");
                    met["ImpresionesPago"] = v.GetValueOrDefault("post_impressions_paid");
                    met["ImpresionesOrganico"] = v.GetValueOrDefault("post_impressions_organic");
                }
                catch { }
                var permalink = Str(p, "permalink_url");
                posts.Add(new Post
                {
                    PostId = id,
                    Tipo = (permalink ?? "").Contains("/reel/") ? "REEL" : "POST",
                    Fecha = ParseFecha(Str(p, "created_time")),
                    Texto = Str(p, "message"),
                    Permalink = permalink,
                    Met = met,
                    TiempoPromedioSeg = tiempo
                });
            }
        return (Int(perfil, "followers_count"), Int(perfil, "fan_count"), posts);
    }

    private static Dictionary<string, int?> Insights(JsonElement root)
    {
        var d = new Dictionary<string, int?>();
        if (root.TryGetProperty("data", out var arr))
            foreach (var x in arr.EnumerateArray())
            {
                var name = Str(x, "name");
                if (name is null) continue;
                if (x.TryGetProperty("values", out var vals) && vals.GetArrayLength() > 0
                    && vals[0].TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.Number)
                    d[name] = val.GetInt32();
            }
        return d;
    }

    // ---------------- DB ----------------
    private static async Task<string?> ConfigAsync(Microsoft.Data.SqlClient.SqlConnection cn, string clave, CancellationToken ct)
        => await cn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Valor FROM dbo.MKT_Config WHERE Clave = @clave", new { clave }, cancellationToken: ct));

    private static async Task<int> UpsertPublicacionAsync(Microsoft.Data.SqlClient.SqlConnection cn, string red, Post p, CancellationToken ct)
    {
        var id = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Id FROM dbo.MKT_RedesPublicaciones WHERE Red=@red AND PostId=@pid",
            new { red, pid = p.PostId }, cancellationToken: ct));
        if (id.HasValue)
        {
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.MKT_RedesPublicaciones SET Texto=@t, Permalink=@pl, Tipo=@tp WHERE Id=@id",
                new { t = p.Texto, pl = p.Permalink, tp = p.Tipo, id = id.Value }, cancellationToken: ct));
            return id.Value;
        }
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO dbo.MKT_RedesPublicaciones (Red, PostId, Tipo, FechaPublicacion, Texto, Permalink)
              OUTPUT INSERTED.Id VALUES (@red,@pid,@tp,@fp,@t,@pl)",
            new { red, pid = p.PostId, tp = p.Tipo, fp = p.Fecha, t = p.Texto, pl = p.Permalink }, cancellationToken: ct));
    }

    private static async Task UpsertMetricasAsync(Microsoft.Data.SqlClient.SqlConnection cn, int pubId, Dictionary<string, int?> met, DateTime hoy, DateTime ahora, CancellationToken ct)
    {
        var pars = new
        {
            pubId, hoy, ahora,
            Alcance = met.GetValueOrDefault("Alcance"),
            Reproducciones = met.GetValueOrDefault("Reproducciones"),
            MeGusta = met.GetValueOrDefault("MeGusta"),
            Comentarios = met.GetValueOrDefault("Comentarios"),
            Compartidos = met.GetValueOrDefault("Compartidos"),
            Guardados = met.GetValueOrDefault("Guardados"),
            Interacciones = met.GetValueOrDefault("Interacciones"),
            TiempoPromedioSeg = (decimal?)null,
            ImpresionesPago = met.GetValueOrDefault("ImpresionesPago"),
            ImpresionesOrganico = met.GetValueOrDefault("ImpresionesOrganico"),
        };
        var existe = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Id FROM dbo.MKT_RedesMetricas WHERE PublicacionId=@pubId AND Fecha=@hoy", pars, cancellationToken: ct));
        if (existe.HasValue)
            await cn.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.MKT_RedesMetricas SET FechaHora=@ahora, Alcance=@Alcance, Reproducciones=@Reproducciones,
                   MeGusta=@MeGusta, Comentarios=@Comentarios, Compartidos=@Compartidos, Guardados=@Guardados,
                   Interacciones=@Interacciones, ImpresionesPago=@ImpresionesPago, ImpresionesOrganico=@ImpresionesOrganico
                  WHERE PublicacionId=@pubId AND Fecha=@hoy", pars, cancellationToken: ct));
        else
            await cn.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO dbo.MKT_RedesMetricas (PublicacionId, Fecha, FechaHora, Alcance, Reproducciones, MeGusta,
                   Comentarios, Compartidos, Guardados, Interacciones, ImpresionesPago, ImpresionesOrganico)
                  VALUES (@pubId,@hoy,@ahora,@Alcance,@Reproducciones,@MeGusta,@Comentarios,@Compartidos,@Guardados,
                   @Interacciones,@ImpresionesPago,@ImpresionesOrganico)", pars, cancellationToken: ct));
    }

    private static async Task UpsertPerfilAsync(Microsoft.Data.SqlClient.SqlConnection cn, string red, int? seguidores, int? publicaciones, DateTime hoy, DateTime ahora, CancellationToken ct)
    {
        var existe = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Id FROM dbo.MKT_RedesPerfil WHERE Red=@red AND Fecha=@hoy", new { red, hoy }, cancellationToken: ct));
        if (existe.HasValue)
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.MKT_RedesPerfil SET FechaHora=@ahora, Seguidores=@seguidores, Publicaciones=@publicaciones WHERE Red=@red AND Fecha=@hoy",
                new { red, hoy, ahora, seguidores, publicaciones }, cancellationToken: ct));
        else
            await cn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.MKT_RedesPerfil (Red, Fecha, FechaHora, Seguidores, Publicaciones) VALUES (@red,@hoy,@ahora,@seguidores,@publicaciones)",
                new { red, hoy, ahora, seguidores, publicaciones }, cancellationToken: ct));
    }

    // ---------------- helpers JSON ----------------
    private static string? Str(JsonElement e, string prop)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v)
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString())
            : null;

    private static int? Int(JsonElement e, string prop)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32() : (int?)null;

    private static int? IntPath(JsonElement e, params string[] path)
    {
        var cur = e;
        foreach (var p in path)
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(p, out var nxt)) return null;
            cur = nxt;
        }
        return cur.ValueKind == JsonValueKind.Number ? cur.GetInt32() : (int?)null;
    }

    private static DateTime? ParseFecha(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, out var dto))
            return TimeZoneInfo.ConvertTime(dto, TzAr).DateTime;
        return null;
    }
}
