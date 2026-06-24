using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Packs;

namespace MarketWeb.Application.Packs;

/// <summary>
/// Reescritura del reporte de FrmRepoPack. Agrupa Packs + PacksBolsas + PacksBolsasDetalle.
/// Baja lógica (Eliminado=1). El "ver desarmados" muestra los eliminados (Eliminado=1).
/// </summary>
public sealed class PacksService : IPacksService
{
    private readonly ISqlConnectionFactory _db;
    public PacksService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<PackDto>> ListarAsync(string? nroPedido, string? codArt, bool verDesarmados, CancellationToken ct = default)
    {
        var estado = verDesarmados ? 1 : 0;
        var sql = """
            SELECT  P.ID                                   AS Id,
                    P.NroPedido                            AS NroPedido,
                    NroInterno = CASE WHEN LTRIM(RTRIM(CAST(ISNULL(P.NroInterno, '') AS varchar(50)))) IN ('', '0')
                                      THEN '' ELSE CAST(P.NroInterno AS varchar(50)) END,
                    D.ARTCOD                               AS ArtCod,
                    CantPacks   = ISNULL(P.CantPacks, 0),
                    CantBolsas  = CAST(COUNT(DISTINCT B.ID) AS int),
                    CantPrendas = CAST(SUM(D.Cantidad) AS int),
                    TienePdf = CAST(MAX(CASE WHEN P.PDF       IS NOT NULL AND DATALENGTH(P.PDF) > 0       THEN 1 ELSE 0 END) AS BIT),
                    TieneTxt = CAST(MAX(CASE WHEN P.PedidoTXT IS NOT NULL AND DATALENGTH(P.PedidoTXT) > 0 THEN 1 ELSE 0 END) AS BIT)
            FROM    Packs P WITH (NOLOCK)
            INNER JOIN PacksBolsas B WITH (NOLOCK) ON P.ID = B.IDPack
            INNER JOIN PacksBolsasDetalle D WITH (NOLOCK) ON B.ID = D.IDPackBolsa
            WHERE   P.Eliminado = @estado AND B.Eliminado = @estado AND D.Eliminado = @estado
            """;
        var ped = Nz(nroPedido);
        var art = Nz(codArt);
        if (ped is not null) sql += " AND P.NroPedido LIKE '%' + @ped + '%'";
        if (art is not null) sql += " AND D.ARTCOD LIKE '%' + @art + '%'";
        sql += """
             GROUP BY P.NroPedido, P.NroInterno, D.ARTCOD, P.ID, ISNULL(P.CantPacks, 0)
             ORDER BY P.NroPedido DESC;
            """;

        using var cn = _db.Create();
        var rows = await cn.QueryAsync<PackDto>(new CommandDefinition(sql, new { estado, ped, art }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<byte[]?> ObtenerPdfAsync(int id, CancellationToken ct = default)
    {
        const string sql = "SELECT PDF FROM Packs WHERE ID = @id;";
        using var cn = _db.Create();
        return await cn.ExecuteScalarAsync<byte[]?>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<byte[]?> ObtenerTxtAsync(int id, CancellationToken ct = default)
    {
        const string sql = "SELECT PedidoTXT FROM Packs WHERE ID = @id;";
        using var cn = _db.Create();
        return await cn.ExecuteScalarAsync<byte[]?>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task EliminarAsync(string nroPedido, string usuario, CancellationToken ct = default)
    {
        var ped = (nroPedido ?? "").Trim();
        if (ped.Length == 0) throw new BusinessException("Falta el N° de pedido a desarmar.");

        const string sql = """
            UPDATE Packs SET Eliminado = 1, Auditoria = @aud WHERE NroPedido = @ped AND Eliminado = 0;
            UPDATE PacksBolsas SET Eliminado = 1, Auditoria = @aud
                WHERE IDPack IN (SELECT ID FROM Packs WHERE NroPedido = @ped);
            UPDATE PacksBolsasDetalle SET Eliminado = 1, Auditoria = @aud
                WHERE IDPackBolsa IN (SELECT ID FROM PacksBolsas WHERE IDPack IN (SELECT ID FROM Packs WHERE NroPedido = @ped));
            """;
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ped, aud = Auditoria("Pedido desarmado", usuario) }, cancellationToken: ct));
    }

    private static string? Nz(string? s)
    {
        var t = s?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static string Auditoria(string accion, string usuario) =>
        $"{accion} | {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
}
