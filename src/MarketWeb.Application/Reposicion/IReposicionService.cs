using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Application.Reposicion;

public interface IReposicionService
{
    /// <summary>
    /// Corre SP_RepoCalcularPacks (~2 min) y devuelve filas + totales. OJO: PERSISTE en producción
    /// (RepoReemplazos, snapshot Reposicion/ReposicionDetalle, resets, eventos). machineName identifica la corrida.
    /// </summary>
    Task<ReposicionResultadoDto> CalcularAsync(ReposicionCalcularRequest req, string machineName, CancellationToken ct = default);
}
