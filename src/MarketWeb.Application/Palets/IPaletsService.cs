using MarketWeb.Shared.Palets;

namespace MarketWeb.Application.Palets;

/// <summary>
/// Reporte de palets (espejo de frmRepoPalets). Fase 1: listado + detalle + desarmar.
/// Pendiente Fase 2: armado (alta + agregar remitos), inconsistencias, impresión de etiqueta.
/// </summary>
public interface IPaletsService
{
    Task<IReadOnlyList<PaletDto>> ListarAsync(
        string? nroPalet, string? codArticulo, string? tipo, string? categoria,
        bool verDesarmados, DateTime desde, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListarTiposAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListarCategoriasAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PaletArticuloDto>> ListarArticulosAsync(int idPalet, CancellationToken ct = default);

    Task DesarmarAsync(int idPalet, CancellationToken ct = default);
}
