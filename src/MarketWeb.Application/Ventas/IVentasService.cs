using MarketWeb.Shared.Ventas;

namespace MarketWeb.Application.Ventas;

public interface IVentasService
{
    /// <summary>Resumen mensual (sp_ResumenVentasMensual).</summary>
    Task<IReadOnlyList<VentaResumenDto>> ListarResumenAsync(DateTime desde, DateTime hasta, CancellationToken ct = default);

    /// <summary>Detalle de cobranzas por medio de pago (sp_ConsultaCobranzas).</summary>
    Task<IReadOnlyList<CobranzaDto>> ListarCobranzasAsync(
        DateTime desde, DateTime hasta, string local, string agrupamiento,
        string detalle, string categoria, string medio, CancellationToken ct = default);
}
