using MarketWeb.Shared.Locales;

namespace MarketWeb.Application.Locales;

public interface ILocalesService
{
    /// <summary>Listado de locales activos (Eliminado=0), filtrable por descripción.</summary>
    Task<IReadOnlyList<LocalDto>> ListarAsync(string? filtro, CancellationToken ct = default);

    /// <summary>Tipos de local activos, para el combo del ABM.</summary>
    Task<IReadOnlyList<LocalTipoDto>> ListarTiposAsync(CancellationToken ct = default);

    /// <summary>Un local por id (para modificar). Null si no existe o está eliminado.</summary>
    Task<LocalDto?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Alta. Devuelve el id nuevo. Lanza BusinessException si la descripción ya existe.</summary>
    Task<int> CrearAsync(LocalSaveRequest req, CancellationToken ct = default);

    /// <summary>Modificación. Lanza BusinessException si la descripción ya existe en otro local.</summary>
    Task ModificarAsync(int id, LocalSaveRequest req, CancellationToken ct = default);

    /// <summary>Baja lógica (Eliminado=1). Nunca DELETE físico.</summary>
    Task EliminarAsync(int id, CancellationToken ct = default);
}
