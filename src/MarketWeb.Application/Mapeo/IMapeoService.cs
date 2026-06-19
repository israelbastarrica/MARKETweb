using MarketWeb.Shared.Mapeo;

namespace MarketWeb.Application.Mapeo;

/// <summary>
/// Estructura de mapeo (espejo de frmRepoMapeo + frmABMMapeo + frmABMMapeoDetalle
/// + frmABMMapeoPosicion + frmABMMapeoRegistro). Tres niveles:
/// ubicación → posiciones (tabla Mapeo) → artículos por posición (MapeoRegistro).
/// </summary>
public interface IMapeoService
{
    Task<IReadOnlyList<MapeoUbicacionDto>> ListarUbicacionesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MapeoPosicionDto>> ListarPosicionesAsync(int idUbicacion, CancellationToken ct = default);
    Task<MapeoPosicionDto?> ObtenerPosicionAsync(int id, CancellationToken ct = default);
    Task<int> CrearPosicionAsync(MapeoPosicionSaveRequest req, CancellationToken ct = default);
    Task ModificarPosicionAsync(int id, MapeoPosicionSaveRequest req, CancellationToken ct = default);
    Task EliminarPosicionAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListarSectoresAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListarMobiliariosAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MapeoArticuloDto>> ListarArticulosAsync(int idMapeo, CancellationToken ct = default);
    Task<int> CrearArticuloAsync(MapeoArticuloSaveRequest req, CancellationToken ct = default);
    Task ModificarArticuloAsync(int id, MapeoArticuloSaveRequest req, CancellationToken ct = default);
    Task EliminarArticuloAsync(int id, CancellationToken ct = default);

    // ---- Reporte (Logística, frmRepoMapeoRegistros / SP_ReporteMapeo_Generar) ----
    Task<IReadOnlyList<MapeoUbicacionDto>> ListarUbicacionesReporteAsync(CancellationToken ct = default); // incluye DEPÓSITO
    Task<IReadOnlyList<string>> ListarTiposAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListarCategoriasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MapeoReporteDto>> ReporteAsync(MapeoReporteRequest req, CancellationToken ct = default);
}
