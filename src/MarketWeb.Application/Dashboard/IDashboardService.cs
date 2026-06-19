using MarketWeb.Shared.Dashboard;

namespace MarketWeb.Application.Dashboard;

public interface IDashboardService
{
    /// <summary>Resumen tipado de ventas del día para la vista mobile (KPIs + top productos + cajeros).</summary>
    Task<DashboardVentasMobileDto> GetResumenMobileAsync(string fecha, string rol, string? local, CancellationToken ct = default);

    /// <summary>Datos de ventas del día para el dashboard (forma JSON que consume el HTML).</summary>
    /// <param name="fecha">YYYYMMDD.</param>
    /// <param name="rol">"admin" (ve LURO+PERALTA) o "cajero" (un local).</param>
    /// <param name="local">Local del cajero (LURO/PERALTA) cuando rol=cajero.</param>
    Task<object> GetVentasAsync(string fecha, string rol, string? local, CancellationToken ct = default);

    /// <summary>Fichadas del día (Fase 2 — por ahora stub vacío).</summary>
    Task<object> GetFichadasAsync(string fecha, string rol, string? local, CancellationToken ct = default);
}
