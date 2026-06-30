using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Marketing;

namespace MarketWeb.Application.Marketing;

public interface IMarketingService
{
    Task<IReadOnlyList<MktPerfilDto>> PerfilesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MktPublicacionDto>> PublicacionesAsync(string? red, int top, CancellationToken ct = default);
    Task<MktDashboardDto> DashboardAsync(CancellationToken ct = default);
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

    private static async Task<bool> ExistenAsync(Microsoft.Data.SqlClient.SqlConnection cn, CancellationToken ct)
        => await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID('dbo.MKT_RedesPerfil') IS NULL THEN 0 ELSE 1 END", cancellationToken: ct)) == 1;

    private static async Task<int?> InsVal(Microsoft.Data.SqlClient.SqlConnection cn, string red, string met, string seg, DateTime fecha, CancellationToken ct)
        => await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT CAST(Valor AS INT) FROM dbo.MKT_RedesInsights WHERE Red=@red AND Metrica=@met AND Segmento=@seg AND Fecha=@fecha",
            new { red, met, seg, fecha }, cancellationToken: ct));

    private static async Task<int?> InsSum(Microsoft.Data.SqlClient.SqlConnection cn, string red, string met, CancellationToken ct)
        => await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT CAST(SUM(Valor) AS INT) FROM dbo.MKT_RedesInsights WHERE Red=@red AND Metrica=@met AND Segmento='TOTAL'",
            new { red, met }, cancellationToken: ct));

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
                dto.VisualizacionesIG = await InsVal(cn, "IG", "views", "TOTAL", maxF.Value, ct);
                dto.Interacciones = await InsVal(cn, "IG", "total_interactions", "TOTAL", maxF.Value, ct);
                dto.CuentasAlcanzadas = await InsVal(cn, "IG", "accounts_engaged", "TOTAL", maxF.Value, ct);
                dto.AlcanceSeguidores = await InsVal(cn, "IG", "reach", "FOLLOWER", maxF.Value, ct);
                dto.AlcanceNoSeguidores = await InsVal(cn, "IG", "reach", "NON_FOLLOWER", maxF.Value, ct);
                dto.Alcance = (dto.AlcanceSeguidores ?? 0) + (dto.AlcanceNoSeguidores ?? 0);
            }
            dto.FbInteracciones = await InsSum(cn, "FB", "page_post_engagements", ct);
            dto.FbSeguidoresNuevos = await InsSum(cn, "FB", "page_daily_follows", ct);
            dto.FbDejaronSeguir = await InsSum(cn, "FB", "page_daily_unfollows", ct);
            dto.AlcanceSerie = (await cn.QueryAsync<MktSeriePuntoDto>(new CommandDefinition(
                "SELECT Fecha, Valor FROM dbo.MKT_RedesInsights WHERE Red='IG' AND Metrica='reach' AND Segmento='TOTAL' ORDER BY Fecha",
                cancellationToken: ct))).ToList();
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
}
