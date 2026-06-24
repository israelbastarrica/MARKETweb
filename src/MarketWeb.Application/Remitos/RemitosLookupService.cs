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
        // La ultima corrida activa QUE TENGA filas del local pedido (no la ultima global:
        // una corrida posterior sin filas de ese local dejaria la lista vacia).
        const string sql =
            "SELECT DISTINCT RTRIM(d.ARTCOD) AS ArtCod, RTRIM(d.ARTDES) AS ArtDes " +
            "FROM MARKET.dbo.ReposicionDetalle d " +
            "WHERE d.IDReposicion = ( " +
            "    SELECT TOP 1 r.ID FROM MARKET.dbo.Reposicion r " +
            "    WHERE ISNULL(r.Eliminado, 0) = 0 " +
            "      AND EXISTS ( SELECT 1 FROM MARKET.dbo.ReposicionDetalle dd " +
            "                   WHERE dd.IDReposicion = r.ID AND RTRIM(dd.LocalDestino) = @Local ) " +
            "    ORDER BY r.FechaHoraCorrida DESC " +
            ") AND RTRIM(d.LocalDestino) = @Local " +
            "ORDER BY ArtCod;";   // alias, no d.ARTCOD: con SELECT DISTINCT el ORDER BY debe estar en el select

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
            Variantes = combos
                .Where(c => !string.IsNullOrWhiteSpace(c.Talle) || !string.IsNullOrWhiteSpace(c.Color))
                .Select(c => new ComboVarianteDto { Color = c.Color, Talle = c.Talle })
                .ToList(),
        };
    }

    public async Task<BolsaDto?> BuscarBolsaAsync(string nroBolsa, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nroBolsa)) return null;
        var nro = nroBolsa.Trim();

        using var cn = _db.Create();

        // 1) Bolsa por código de barras (NroBolsa). Primero activa (Eliminado=0); si no aparece,
        //    caemos al fallback SIN filtro: en el depósito hay bolsas con baja lógica que igual se remiten.
        var id = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 ID FROM MARKET.dbo.PacksBolsas WHERE RTRIM(NroBolsa) = @Nro AND ISNULL(Eliminado, 0) = 0 ORDER BY ID DESC;",
            new { Nro = nro }, cancellationToken: ct));
        var eliminada = false;
        if (id is null)
        {
            id = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT TOP 1 ID FROM MARKET.dbo.PacksBolsas WHERE RTRIM(NroBolsa) = @Nro ORDER BY ID DESC;",
                new { Nro = nro }, cancellationToken: ct));
            eliminada = id is not null;
        }
        if (id is null) return null;

        // 2) Detalle (artículo + color + talle + cantidad) + descripción del ART. Si la bolsa estaba
        //    activa, excluimos renglones dados de baja; si la bolsa venía eliminada, su detalle también
        //    está en Eliminado=1 → traemos todos (si no, la encontraríamos vacía).
        var sqlDet =
            "SELECT RTRIM(BD.ARTCOD) AS ArtCod, RTRIM(ISNULL(ART.ARTDES, '')) AS ArtDes, " +
            "       RTRIM(ISNULL(BD.Color, '')) AS Color, RTRIM(ISNULL(BD.Talle, '')) AS Talle, BD.Cantidad " +
            "FROM MARKET.dbo.PacksBolsasDetalle BD " +
            "LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.ART ART ON RTRIM(ART.ARTCOD) = RTRIM(BD.ARTCOD) " +
            "WHERE BD.IDPackBolsa = @Id " +
            (eliminada ? "" : "AND ISNULL(BD.Eliminado, 0) = 0 ") +
            "ORDER BY BD.ARTCOD, BD.Color, BD.Talle;";

        var renglones = (await cn.QueryAsync<BolsaRenglonDto>(new CommandDefinition(sqlDet, new { Id = id.Value }, cancellationToken: ct))).ToList();

        return new BolsaDto { NroBolsa = nro, IdPackBolsa = id.Value, Eliminada = eliminada, Renglones = renglones };
    }

    private sealed record ArtRow(string Cod, string Des);
    private sealed record CombRow(string Color, string Talle);
}
