using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Marketing;

namespace MarketWeb.Application.Marketing;

public interface IMarketingService
{
    Task<IReadOnlyList<MktPerfilDto>> PerfilesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MktPublicacionDto>> PublicacionesAsync(string? red, int top, CancellationToken ct = default);
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
