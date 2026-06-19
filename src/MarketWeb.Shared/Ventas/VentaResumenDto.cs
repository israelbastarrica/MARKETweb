namespace MarketWeb.Shared.Ventas;

/// <summary>Resumen mensual de ventas (salida de sp_ResumenVentasMensual).</summary>
public sealed class VentaResumenDto
{
    public string Periodo { get; set; } = "";
    public decimal Ventas { get; set; }
    public decimal Costos { get; set; }
    // Coincide con la columna IVA_VTAS del SP (Dapper mapea por nombre, ignora mayúsc.).
    public decimal Iva_Vtas { get; set; }
}
