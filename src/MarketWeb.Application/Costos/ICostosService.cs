using MarketWeb.Shared.Costos;
using MarketWeb.Shared.Insumos; // UbicacionDto (entidad genérica reutilizada)

namespace MarketWeb.Application.Costos;

public interface ICostosService
{
    Task<IReadOnlyList<UbicacionDto>> ListarUbicacionesAsync(CancellationToken ct = default);

    /// <summary>
    /// Reporte de margen (venta vs costo) llamando a sp_ConsultaMargenVentas.
    /// </summary>
    /// <param name="local">"TODOS" o la descripción de la ubicación.</param>
    /// <param name="agrupamiento">"DÍA" | "MES" | "AÑO".</param>
    Task<IReadOnlyList<CostoMargenDto>> ListarMargenAsync(
        DateTime desde, DateTime hasta, string local, string agrupamiento, CancellationToken ct = default);
}
