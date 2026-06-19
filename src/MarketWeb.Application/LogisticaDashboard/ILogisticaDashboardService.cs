using MarketWeb.Shared.LogisticaDashboard;

namespace MarketWeb.Application.LogisticaDashboard;

/// <summary>
/// Dashboard de Logística (port de LogisticaDashboard/Dash). Panel 1: Despachos + Recepción.
/// Ventana = jornada actual (último 21:00 → ahora).
/// </summary>
public interface ILogisticaDashboardService
{
    Task<PanelDespachoRecepcionDto> GetPanelDespachoRecepcionAsync(CancellationToken ct = default);

    /// <summary>Panel 2: pendientes en tránsito (cruzados, doble despacho, sin escanear, sin salida, recepción).</summary>
    Task<PanelPendientesDto> GetPanelPendientesAsync(CancellationToken ct = default);

    /// <summary>Panel 3: últimos mapeos en logística (CENTRAL, por escaneo más reciente).</summary>
    Task<PanelMapeosDto> GetPanelMapeosAsync(CancellationToken ct = default);

    /// <summary>Panel 4: ubicaciones libres en logística (CENTRAL, sin artículo asignado).</summary>
    Task<PanelVaciasDto> GetPanelVaciasAsync(CancellationToken ct = default);

    /// <summary>Panel 5: artículos estancados en logística (cache 5 min + refresh background).</summary>
    Task<PanelEstancadosDto> GetPanelEstancadosAsync(CancellationToken ct = default);

    /// <summary>Panel 6: picking nocturno — corrida pedido vs armado por local.</summary>
    Task<PanelPickingDto> GetPanelPickingAsync(CancellationToken ct = default);

    /// <summary>Panel 7: artículos con más ubicaciones en CENTRAL (incluye palets).</summary>
    Task<PanelMasUbicDto> GetPanelMasUbicAsync(CancellationToken ct = default);

    /// <summary>Panel 8: panel de reposición · inteligencia (abastecimiento, día operativo, cobertura en vivo).</summary>
    Task<PanelReposicionDto> GetPanelReposicionAsync(CancellationToken ct = default);

    /// <summary>Panel 9: detalle de artículos a reponer (rojos ≥100%), reusa el cache de cobertura.</summary>
    Task<PanelRojosDto> GetPanelRojosAsync(CancellationToken ct = default);
}
