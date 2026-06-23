using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Dragonfish;

namespace MarketWeb.Application.Remitos;

public sealed class RemitosLookupService : IRemitosLookupService
{
    private readonly ISqlConnectionFactory _db;
    public RemitosLookupService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<UltimaRepoItemDto>> UltimaRepoAsync(string local, CancellationToken ct = default)
    {
        const string sql =
            "SELECT DISTINCT RTRIM(d.ARTCOD) AS ArtCod, RTRIM(d.ARTDES) AS ArtDes " +
            "FROM MARKET.dbo.ReposicionDetalle d " +
            "WHERE d.IDReposicion = ( " +
            "    SELECT TOP 1 ID FROM MARKET.dbo.Reposicion " +
            "    WHERE ISNULL(Eliminado, 0) = 0 " +
            "    ORDER BY FechaHoraCorrida DESC " +
            ") AND RTRIM(d.LocalDestino) = @Local " +
            "ORDER BY d.ARTCOD;";

        using var cn = _db.Create();
        return (await cn.QueryAsync<UltimaRepoItemDto>(new CommandDefinition(sql, new { Local = local.ToUpperInvariant() }, cancellationToken: ct))).ToList();
    }

    public async Task<ArticuloLookupDto?> BuscarArticuloAsync(string cod, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cod)) return null;
        cod = cod.Trim().ToUpperInvariant();

        const string sql =
            "SELECT TOP 1 RTRIM(ARTCOD) AS Cod, RTRIM(ARTDES) AS Des " +
            "FROM DRAGONFISH_CENTRAL.ZooLogic.ART " +
            "WHERE RTRIM(ARTCOD) = @Cod; " +
            "SELECT DISTINCT RTRIM(COCOL) AS Color, RTRIM(TALLE) AS Talle " +
            "FROM DRAGONFISH_CENTRAL.ZooLogic.COMB " +
            "WHERE RTRIM(COART) = @Cod " +
            "ORDER BY Color, Talle;";

        using var cn = _db.Create();
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { Cod = cod }, cancellationToken: ct));

        var art = await multi.ReadFirstOrDefaultAsync<ArtRow>();
        if (art is null) return null;

        var combos = (await multi.ReadAsync<CombRow>()).ToList();
        return new ArticuloLookupDto
        {
            Cod = art.Cod,
            Des = art.Des,
            Colores = combos.Select(c => c.Color).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList(),
            Talles = combos.Select(c => c.Talle).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList(),
        };
    }

    private sealed record ArtRow(string Cod, string Des);
    private sealed record CombRow(string Color, string Talle);
}
