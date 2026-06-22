using MarketWeb.Shared.Mapa;

namespace MarketWeb.Application.Mapa;

/// <summary>
/// Datos del Mapa 3D del depósito (IDUbicacion=1). Porteo de los 4 endpoints del backend Node
/// de deposito-3d a la API del MarketWeb.
/// </summary>
public interface IMapaService
{
    Task<IReadOnlyList<MapaModuloDto>> ModulosAsync(CancellationToken ct = default);
    Task<MapaModuloDetalleDto> ModuloAsync(string modulo, CancellationToken ct = default);
    Task<IReadOnlyList<string>> VaciosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> BuscarAsync(string? q, CancellationToken ct = default);
}
