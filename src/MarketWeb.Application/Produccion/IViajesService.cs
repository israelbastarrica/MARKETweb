using MarketWeb.Shared.Produccion;

namespace MarketWeb.Application.Produccion;

public interface IViajesService
{
    /// <summary>Listado de viajes (cabecera + conteos).</summary>
    Task<IReadOnlyList<ViajeDto>> ListarViajesAsync(CancellationToken ct = default);

    /// <summary>Cabecera de un viaje.</summary>
    Task<ViajeDto?> ViajeAsync(int idViaje, CancellationToken ct = default);

    /// <summary>Artículos del viaje.</summary>
    Task<IReadOnlyList<ViajeArticuloDto>> ArticulosAsync(int idViaje, CancellationToken ct = default);

    /// <summary>Proveedores con artículos en el viaje.</summary>
    Task<IReadOnlyList<ViajeProveedorDto>> ProveedoresAsync(int idViaje, CancellationToken ct = default);

    /// <summary>Contenedores del viaje.</summary>
    Task<IReadOnlyList<ViajeContenedorDto>> ContenedoresAsync(int idViaje, CancellationToken ct = default);

    /// <summary>Ficha técnica completa de un artículo (foto, talles/curva, packs, costo).</summary>
    Task<ViajeArticuloFichaDto?> FichaAsync(int idArticulo, CancellationToken ct = default);

    /// <summary>Cantidad de equivalencias (código proveedor → código MARKET) cargadas.</summary>
    Task<int> ContarCodigosMarketAsync(CancellationToken ct = default);

    /// <summary>Importa equivalencias pegadas de la planilla (proveedor TAB market TAB descripción).</summary>
    Task<ImportarCodigosResultadoDto> ImportarCodigosMarketAsync(string texto, string usuario, CancellationToken ct = default);

    /// <summary>Carga/edita a mano el código Dragon de un artículo (override; vacío = vuelve a usar la planilla).</summary>
    Task GuardarCodigoDragonAsync(int idArticulo, string codigo, string usuario, CancellationToken ct = default);

    // ---- ABM ----
    Task<int> GuardarViajeAsync(ViajeSaveRequest req, string usuario, CancellationToken ct = default);
    Task EliminarViajeAsync(int id, string usuario, CancellationToken ct = default);
    Task<ContenedorEditorDto?> ObtenerContenedorAsync(int id, CancellationToken ct = default);
    Task GuardarContenedorAsync(ContenedorSaveRequest req, string usuario, CancellationToken ct = default);
    Task EliminarContenedorAsync(int id, string usuario, CancellationToken ct = default);
    Task<ProveedorEditorDto?> ObtenerProveedorAsync(int id, CancellationToken ct = default);
    Task GuardarProveedorAsync(ProveedorEditorDto req, string usuario, CancellationToken ct = default);
    Task EliminarProveedorAsync(int id, string usuario, CancellationToken ct = default);
    Task<ArticuloEditorDto?> ObtenerArticuloEditorAsync(int id, CancellationToken ct = default);
    Task<int> GuardarArticuloAsync(ArticuloEditorDto req, string usuario, CancellationToken ct = default);
    Task EliminarArticuloAsync(int id, string usuario, CancellationToken ct = default);

    /// <summary>Ruta física de una foto (para servirla). null si no existe.</summary>
    string? FotoFullPath(string archivo);

    /// <summary>Importa/espeja el .db de ViajePedidos a MARKET (preserva ids, upsert, copia fotos).</summary>
    Task<ImportarViajeResultadoDto> ImportarAsync(string usuario, CancellationToken ct = default);
}
