using MarketWeb.Shared.Locales;

namespace MarketWeb.Application.TiposLocal;

public interface ITiposLocalService
{
    Task<IReadOnlyList<LocalTipoDto>> ListarAsync(string? filtro, CancellationToken ct = default);
    Task<LocalTipoDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<int> CrearAsync(TipoLocalSaveRequest req, CancellationToken ct = default);
    Task ModificarAsync(int id, TipoLocalSaveRequest req, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
}
