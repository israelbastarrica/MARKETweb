using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Marketing;

namespace MarketWeb.Application.Marketing;

public interface IMarketingService
{
    Task<IReadOnlyList<MktPerfilDto>> PerfilesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MktPublicacionDto>> PublicacionesAsync(string? red, int top, CancellationToken ct = default);
    Task<MktDashboardDto> DashboardAsync(CancellationToken ct = default);
    Task<string?> ThumbUrlAsync(string red, string postId, CancellationToken ct = default);
    Task<CalMesDto> CalendarioMesAsync(int anio, int mes, CancellationToken ct = default);
    Task<int> GuardarAccionAsync(CalAccionSaveRequest req, string aud, CancellationToken ct = default);
    Task<bool> EliminarAccionAsync(int id, string aud, CancellationToken ct = default);
}

/// <summary>
/// Marketing / Redes sociales. Solo lectura de las tablas MKT_Redes* que carga el
/// colector Python (Meta Graph API → FB/IG): MKT_RedesPerfil (seguidores por día),
/// MKT_RedesPublicaciones (catálogo de posts) + MKT_RedesMetricas (snapshot por día).
/// </summary>
public sealed class MarketingService : IMarketingService
{
    private readonly ISqlConnectionFactory _db;
    public MarketingService(ISqlConnectionFactory db) => _db = db;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private const string Api = "https://graph.facebook.com/v22.0";

    // Resuelve la URL de imagen vigente de una publicación (las URLs de Meta caducan → se piden al vuelo).
    public async Task<string?> ThumbUrlAsync(string red, string postId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(postId)) return null;
        using var cn = _db.Create();
        var token = await cn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Valor FROM dbo.MKT_Config WHERE Clave='META_ACCESS_TOKEN'", cancellationToken: ct));
        if (string.IsNullOrWhiteSpace(token)) return null;
        var campos = red == "FB" ? "full_picture" : "thumbnail_url,media_url";
        try
        {
            using var resp = await Http.GetAsync(
                $"{Api}/{Uri.EscapeDataString(postId)}?fields={campos}&access_token={Uri.EscapeDataString(token!)}", ct);
            using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var r = doc.RootElement;
            foreach (var f in new[] { "thumbnail_url", "media_url", "full_picture" })
                if (r.TryGetProperty(f, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                    return v.GetString();
        }
        catch { }
        return null;
    }

    private static async Task<bool> ExistenAsync(Microsoft.Data.SqlClient.SqlConnection cn, CancellationToken ct)
        => await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID('dbo.MKT_RedesPerfil') IS NULL THEN 0 ELSE 1 END", cancellationToken: ct)) == 1;

    private static async Task<int?> InsVal(Microsoft.Data.SqlClient.SqlConnection cn, string red, string met, string seg, DateTime fecha, CancellationToken ct)
        => await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT CAST(Valor AS INT) FROM dbo.MKT_RedesInsights WHERE Red=@red AND Metrica=@met AND Segmento=@seg AND Fecha=@fecha",
            new { red, met, seg, fecha }, cancellationToken: ct));

    private static async Task<int?> InsSumRange(Microsoft.Data.SqlClient.SqlConnection cn, string red, string met, DateTime desde, DateTime hasta, CancellationToken ct)
        => await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT CAST(SUM(Valor) AS INT) FROM dbo.MKT_RedesInsights WHERE Red=@red AND Metrica=@met AND Segmento='TOTAL' AND Fecha > @desde AND Fecha <= @hasta",
            new { red, met, desde, hasta }, cancellationToken: ct));

    public async Task<MktDashboardDto> DashboardAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        var dto = new MktDashboardDto();

        var hayIns = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID('dbo.MKT_RedesInsights') IS NULL THEN 0 ELSE 1 END", cancellationToken: ct)) == 1;
        if (hayIns)
        {
            var maxF = await cn.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
                "SELECT MAX(Fecha) FROM dbo.MKT_RedesInsights WHERE Red='IG' AND Metrica='views' AND Segmento='TOTAL'", cancellationToken: ct));
            dto.Hasta = maxF;
            if (maxF.HasValue)
            {
                var max = maxF.Value;
                var prevF = await cn.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
                    "SELECT MAX(Fecha) FROM dbo.MKT_RedesInsights WHERE Red='IG' AND Metrica='views' AND Segmento='TOTAL' AND Fecha < @max",
                    new { max }, cancellationToken: ct));

                dto.VisualizacionesIG = await InsVal(cn, "IG", "views", "TOTAL", max, ct);
                dto.Interacciones = await InsVal(cn, "IG", "total_interactions", "TOTAL", max, ct);
                dto.CuentasAlcanzadas = await InsVal(cn, "IG", "accounts_engaged", "TOTAL", max, ct);
                dto.AlcanceSeguidores = await InsVal(cn, "IG", "reach", "FOLLOWER", max, ct);
                dto.AlcanceNoSeguidores = await InsVal(cn, "IG", "reach", "NON_FOLLOWER", max, ct);
                dto.Alcance = (dto.AlcanceSeguidores ?? 0) + (dto.AlcanceNoSeguidores ?? 0);

                if (prevF.HasValue)
                {
                    dto.VisualizacionesPrevIG = await InsVal(cn, "IG", "views", "TOTAL", prevF.Value, ct);
                    dto.InteraccionesPrev = await InsVal(cn, "IG", "total_interactions", "TOTAL", prevF.Value, ct);
                    var rf = await InsVal(cn, "IG", "reach", "FOLLOWER", prevF.Value, ct);
                    var rn = await InsVal(cn, "IG", "reach", "NON_FOLLOWER", prevF.Value, ct);
                    if (rf.HasValue || rn.HasValue) dto.AlcancePrev = (rf ?? 0) + (rn ?? 0);
                }

                // FB: sumas por ventana (actual = últimos 28 días; previo = los 28 anteriores).
                var ini = max.AddDays(-28); var iniPrev = max.AddDays(-56);
                dto.FbInteracciones = await InsSumRange(cn, "FB", "page_post_engagements", ini, max, ct);
                dto.FbSeguidoresNuevos = await InsSumRange(cn, "FB", "page_daily_follows", ini, max, ct);
                dto.FbDejaronSeguir = await InsSumRange(cn, "FB", "page_daily_unfollows", ini, max, ct);
                dto.FbInteraccionesPrev = await InsSumRange(cn, "FB", "page_post_engagements", iniPrev, ini, ct);

                dto.AlcanceSerie = (await cn.QueryAsync<MktSeriePuntoDto>(new CommandDefinition(
                    "SELECT Fecha, Valor FROM dbo.MKT_RedesInsights WHERE Red='IG' AND Metrica='reach' AND Segmento='TOTAL' AND Fecha > @ini ORDER BY Fecha",
                    new { ini }, cancellationToken: ct))).ToList();
            }
        }

        if (await ExistenAsync(cn, ct))
        {
            dto.Destacados = (await cn.QueryAsync<MktPublicacionDto>(new CommandDefinition(@"
SELECT TOP 8 p.Red, p.PostId, ISNULL(p.Tipo,'') AS Tipo, p.FechaPublicacion, ISNULL(p.Texto,'') AS Texto, ISNULL(p.Permalink,'') AS Permalink,
       m.Alcance, m.MeGusta, m.Comentarios, m.Interacciones
FROM dbo.MKT_RedesPublicaciones p
OUTER APPLY (SELECT TOP 1 mm.* FROM dbo.MKT_RedesMetricas mm WHERE mm.PublicacionId = p.Id ORDER BY mm.Fecha DESC) m
WHERE ISNULL(p.Eliminado,0) = 0
ORDER BY ISNULL(m.Alcance,0) DESC, ISNULL(m.MeGusta,0) DESC", cancellationToken: ct))).ToList();
        }
        return dto;
    }

    public async Task<IReadOnlyList<MktPerfilDto>> PerfilesAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        if (!await ExistenAsync(cn, ct)) return new List<MktPerfilDto>();
        const string sql = @"
SELECT p.Red, p.Seguidores, p.Publicaciones, p.FechaHora
FROM dbo.MKT_RedesPerfil p
WHERE p.Fecha = (SELECT MAX(Fecha) FROM dbo.MKT_RedesPerfil x WHERE x.Red = p.Red)
ORDER BY p.Red;";
        return (await cn.QueryAsync<MktPerfilDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    private const string EnsureColsDdl = @"
IF OBJECT_ID('dbo.MKT_RedesMetricas') IS NOT NULL AND COL_LENGTH('dbo.MKT_RedesMetricas','ImpresionesPago') IS NULL ALTER TABLE dbo.MKT_RedesMetricas ADD ImpresionesPago INT NULL;
IF OBJECT_ID('dbo.MKT_RedesMetricas') IS NOT NULL AND COL_LENGTH('dbo.MKT_RedesMetricas','ImpresionesOrganico') IS NULL ALTER TABLE dbo.MKT_RedesMetricas ADD ImpresionesOrganico INT NULL;";

    public async Task<IReadOnlyList<MktPublicacionDto>> PublicacionesAsync(string? red, int top, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        if (!await ExistenAsync(cn, ct)) return new List<MktPublicacionDto>();
        await cn.ExecuteAsync(new CommandDefinition(EnsureColsDdl, cancellationToken: ct));
        if (top <= 0 || top > 2000) top = 100;
        var r = string.IsNullOrWhiteSpace(red) || red == "TODOS" ? null : red.Trim().ToUpperInvariant();
        // Gasto de pauta (MKT_RedesAds) solo si la tabla existe (la crea colector_ads.py).
        var hayAds = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID('dbo.MKT_RedesAds') IS NULL THEN 0 ELSE 1 END", cancellationToken: ct)) == 1;
        var selAds = hayAds ? "ad.Gasto, ad.Moneda AS MonedaAd," : "CAST(NULL AS DECIMAL(14,2)) AS Gasto, CAST('' AS VARCHAR(5)) AS MonedaAd,";
        var joinAds = hayAds ? "LEFT JOIN dbo.MKT_RedesAds ad ON ad.Red = p.Red AND ad.PostId = p.PostId" : "";
        var sql = $@"
SELECT TOP {top}
       p.Red, p.PostId, ISNULL(p.Tipo,'') AS Tipo, p.FechaPublicacion, ISNULL(p.Texto,'') AS Texto, ISNULL(p.Permalink,'') AS Permalink,
       m.Alcance, m.Reproducciones, m.MeGusta, m.Comentarios, m.Compartidos, m.Guardados, m.Interacciones, m.TiempoPromedioSeg,
       m.ImpresionesPago, m.ImpresionesOrganico,
       {selAds}
       m.FechaHora AS FechaMetrica
FROM dbo.MKT_RedesPublicaciones p
OUTER APPLY (SELECT TOP 1 mm.* FROM dbo.MKT_RedesMetricas mm WHERE mm.PublicacionId = p.Id ORDER BY mm.Fecha DESC) m
{joinAds}
WHERE ISNULL(p.Eliminado,0) = 0 AND (@r IS NULL OR p.Red = @r)
ORDER BY p.FechaPublicacion DESC;";
        return (await cn.QueryAsync<MktPublicacionDto>(new CommandDefinition(sql, new { r }, cancellationToken: ct))).ToList();
    }

    // ===================== Calendario de Marketing =====================

    private const string CalDdl = @"
IF OBJECT_ID('dbo.MKT_CalendarioAcciones','U') IS NULL
CREATE TABLE dbo.MKT_CalendarioAcciones(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Fecha DATE NOT NULL,
    FechaFin DATE NULL,
    Titulo NVARCHAR(200) NULL,
    Tipo NVARCHAR(40) NULL,
    Notas NVARCHAR(1000) NULL,
    Eliminado BIT NOT NULL CONSTRAINT DF_MktCalAcc_Elim DEFAULT(0),
    Auditoria NVARCHAR(300) NULL);";

    public async Task<CalMesDto> CalendarioMesAsync(int anio, int mes, CancellationToken ct = default)
    {
        if (mes < 1 || mes > 12) mes = 1;
        var ini = new DateTime(anio, mes, 1);
        var fin = ini.AddMonths(1);
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(CalDdl, cancellationToken: ct));
        var dto = new CalMesDto();

        // Publicaciones del mes (con la última métrica: me gusta / alcance).
        var hayPubs = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID('dbo.MKT_RedesPublicaciones') IS NULL THEN 0 ELSE 1 END", cancellationToken: ct)) == 1;
        if (hayPubs)
        {
            const string sqlP = @"
SELECT p.Red, p.PostId, p.FechaPublicacion AS Fecha, ISNULL(p.Texto,'') AS Texto, ISNULL(p.Permalink,'') AS Permalink,
       m.MeGusta, m.Alcance
FROM dbo.MKT_RedesPublicaciones p
OUTER APPLY (SELECT TOP 1 mm.MeGusta, mm.Alcance FROM dbo.MKT_RedesMetricas mm WHERE mm.PublicacionId = p.Id ORDER BY mm.Fecha DESC) m
WHERE ISNULL(p.Eliminado,0) = 0 AND p.FechaPublicacion >= @ini AND p.FechaPublicacion < @fin
ORDER BY p.FechaPublicacion;";
            dto.Publicaciones = (await cn.QueryAsync<CalPublicacionDto>(new CommandDefinition(sqlP, new { ini, fin }, cancellationToken: ct))).ToList();
        }

        // Acciones planificadas que tocan el mes (incluye rangos que lo cruzan).
        const string sqlA = @"
SELECT Id, Fecha, FechaFin, ISNULL(Titulo,'') AS Titulo, ISNULL(Tipo,'OTRO') AS Tipo, ISNULL(Notas,'') AS Notas
FROM dbo.MKT_CalendarioAcciones
WHERE Eliminado = 0 AND Fecha < @fin AND ISNULL(FechaFin, Fecha) >= @ini
ORDER BY Fecha, Id;";
        dto.Acciones = (await cn.QueryAsync<CalAccionDto>(new CommandDefinition(sqlA, new { ini, fin }, cancellationToken: ct))).ToList();
        return dto;
    }

    public async Task<int> GuardarAccionAsync(CalAccionSaveRequest req, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(CalDdl, cancellationToken: ct));
        var p = new
        {
            req.Id,
            req.Fecha,
            FechaFin = req.FechaFin,
            Titulo = (req.Titulo ?? "").Trim(),
            Tipo = string.IsNullOrWhiteSpace(req.Tipo) ? "OTRO" : req.Tipo.Trim().ToUpperInvariant(),
            Notas = (req.Notas ?? "").Trim(),
            aud
        };
        if (req.Id > 0)
        {
            await cn.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.MKT_CalendarioAcciones SET Fecha=@Fecha, FechaFin=@FechaFin, Titulo=@Titulo, Tipo=@Tipo, Notas=@Notas, Auditoria=@aud
                  WHERE Id=@Id", p, cancellationToken: ct));
            return req.Id;
        }
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO dbo.MKT_CalendarioAcciones (Fecha, FechaFin, Titulo, Tipo, Notas, Eliminado, Auditoria)
              VALUES (@Fecha, @FechaFin, @Titulo, @Tipo, @Notas, 0, @aud);
              SELECT CAST(SCOPE_IDENTITY() AS INT);", p, cancellationToken: ct));
    }

    public async Task<bool> EliminarAccionAsync(int id, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(CalDdl, cancellationToken: ct));
        var n = await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.MKT_CalendarioAcciones SET Eliminado=1, Auditoria=@aud WHERE Id=@id AND Eliminado=0",
            new { id, aud }, cancellationToken: ct));
        return n > 0;
    }
}
