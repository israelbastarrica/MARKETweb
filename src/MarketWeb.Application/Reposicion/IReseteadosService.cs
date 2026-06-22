using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Application.Reposicion;

public interface IReseteadosService
{
    Task<IReadOnlyList<string>> ListarMobiliariosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReseteadoDto>> ListarAsync(string local, string mobiliario, string artCod, CancellationToken ct = default);
    Task<ReseteadoEditorDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<int> GuardarAsync(ReseteadoSaveRequest req, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
}
