using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Application.Reposicion;

public interface IReposicionPdf
{
    /// <summary>
    /// Genera el PDF cuadernillo (una sección por local: reposición + reemplazos), igual que el desktop.
    /// fechaCorte = null para corrida real (hoy); fecha pasada para simulación (afecta "ventas día anterior").
    /// </summary>
    Task<byte[]> GenerarAsync(ReposicionResultadoDto datos, DateTime? fechaCorte, CancellationToken ct = default);
}
