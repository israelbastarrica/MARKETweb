using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Application.Reposicion;

public interface IReposicionService
{
    /// <summary>
    /// Corre SP_RepoCalcularPacks (~2 min) y devuelve filas + totales. OJO: PERSISTE en producción
    /// (RepoReemplazos, snapshot Reposicion/ReposicionDetalle, resets, eventos). machineName identifica la corrida.
    /// </summary>
    Task<ReposicionResultadoDto> CalcularAsync(ReposicionCalcularRequest req, string machineName, CancellationToken ct = default);

    /// <summary>Historial de corridas reales guardadas (MARKET.dbo.Reposicion), más recientes primero.</summary>
    Task<IReadOnlyList<CorridaDto>> ListarCorridasAsync(CancellationToken ct = default);

    /// <summary>Reconstruye una corrida guardada (ReposicionDetalle + huérfanos de RepoReemplazos del día) para reimprimir. null si no existe. NO corre el SP ni persiste.</summary>
    Task<ReposicionResultadoDto?> ReconstruirCorridaAsync(int idReposicion, CancellationToken ct = default);
}
