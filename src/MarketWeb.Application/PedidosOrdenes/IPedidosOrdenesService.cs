using MarketWeb.Shared.PedidosOrdenes;

namespace MarketWeb.Application.PedidosOrdenes;

/// <summary>
/// Órdenes de Pedido (Diseño). Tabla propia PedidosOrdenes + cruces a Dragonfish (ART/PROV) y
/// catálogo de equivalencias de talles (CatalogosConfigImagenes). Baja lógica, queries parametrizadas.
/// </summary>
public interface IPedidosOrdenesService
{
    Task<IReadOnlyList<PedidoOrdenListaDto>> ListarAsync(PedidoOrdenFiltro filtro, CancellationToken ct = default);
    Task<PedidoOrdenDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<int> CrearAsync(PedidoOrdenSaveRequest req, string usuario, CancellationToken ct = default);
    Task ModificarAsync(int id, PedidoOrdenSaveRequest req, string usuario, CancellationToken ct = default);
    Task EliminarAsync(int id, string usuario, CancellationToken ct = default);

    // Apoyos para la pantalla
    Task<ArticuloDragonDto> ResolverArticuloAsync(string artCod, CancellationToken ct = default);
    Task<IReadOnlyList<string>> EstadosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EquivalenciaTalleDto>> EquivalenciasTallesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProveedorOrdenDto>> ProveedoresAsync(CancellationToken ct = default);
}
