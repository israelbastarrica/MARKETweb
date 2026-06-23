using MarketWeb.Shared.ConfigImagenes;

namespace MarketWeb.Application.ConfigImagenes;

public interface IConfigImagenesService
{
    Task<IReadOnlyList<ConfigImagenDto>> ListarAsync(string? tipo, string? descripcion, CancellationToken ct = default);
    Task<ConfigImagenDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<byte[]?> ObtenerImagenAsync(int id, CancellationToken ct = default);
    Task<int> CrearAsync(ConfigImagenSaveRequest req, string usuario, CancellationToken ct = default);
    Task ModificarAsync(int id, ConfigImagenSaveRequest req, string usuario, CancellationToken ct = default);
    Task EliminarAsync(int id, string usuario, CancellationToken ct = default);
}
