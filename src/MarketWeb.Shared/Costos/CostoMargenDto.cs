namespace MarketWeb.Shared.Costos;

/// <summary>Una fila del reporte de margen (salida de sp_ConsultaMargenVentas).</summary>
public sealed class CostoMargenDto
{
    public string Periodo { get; set; } = "";
    public string Local { get; set; } = "";
    public decimal CantidadVendida { get; set; }
    public decimal TotalFacturado { get; set; }
    public decimal TotalCosto { get; set; }
    public decimal MargenDinero { get; set; }
    public decimal MargenPorcentaje { get; set; }
}
