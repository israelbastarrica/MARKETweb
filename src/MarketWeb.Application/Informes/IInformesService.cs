using MarketWeb.Shared.Informes;

namespace MarketWeb.Application.Informes;

public interface IInformesService
{
    Task<InformeVentaCombosDto> CombosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InformeVentaFila>> VentasAsync(InformeVentaFiltro f, CancellationToken ct = default);
    Task<byte[]> VentasExcelAsync(InformeVentaFiltro f, CancellationToken ct = default);
    Task<IReadOnlyList<InformeSerieFila>> VentasSerieAsync(InformeVentaFiltro f, string dimension, CancellationToken ct = default);
}
