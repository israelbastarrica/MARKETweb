using MarketWeb.Shared.Packs;

namespace MarketWeb.Application.Packs;

/// <summary>
/// Reporte de packs (espejo de FrmRepoPack). Tablas Packs / PacksBolsas / PacksBolsasDetalle.
/// Solo lectura + baja lógica (desarmar). NO toca el motor de packs de reposición.
/// </summary>
public interface IPacksService
{
    Task<IReadOnlyList<PackDto>> ListarAsync(string? nroPedido, string? codArt, bool verDesarmados, CancellationToken ct = default);
    Task<byte[]?> ObtenerPdfAsync(int id, CancellationToken ct = default);
    Task<byte[]?> ObtenerTxtAsync(int id, CancellationToken ct = default);
    Task EliminarAsync(string nroPedido, string usuario, CancellationToken ct = default);
}
