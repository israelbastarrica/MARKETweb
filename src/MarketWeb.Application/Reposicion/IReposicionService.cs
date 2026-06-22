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

    /// <summary>"Explain" de un artículo en un local (SP_RepoExplicarArticulo): por qué el sistema repone lo que repone. Read-only.</summary>
    Task<ExplicarDto> ExplicarAsync(string local, string artCod, CancellationToken ct = default);

    /// <summary>Resetea un artículo DESDE un remito: re-ancla RepoResto a esa fecha/hora (Pendiente=0) y registra el reset. Idempotente.</summary>
    Task<ResetResultadoDto> ResetearDesdeRemitoAsync(ResetRemitoRequest req, string usuario, CancellationToken ct = default);

    /// <summary>Reset firmado desde un EVENTO de piso: ancla al último remito; packs con signo (FALTANTE+ / SOBRANTE−). Idempotente.</summary>
    Task<ResetResultadoDto> ResetearDesdeEventoAsync(int idEvento, string comentario, string usuario, CancellationToken ct = default);
}
