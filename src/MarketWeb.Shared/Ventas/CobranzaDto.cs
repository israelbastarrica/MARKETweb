namespace MarketWeb.Shared.Ventas;

/// <summary>Cobranza por medio de pago (salida de sp_ConsultaCobranzas).</summary>
public sealed class CobranzaDto
{
    public string Periodo { get; set; } = "";
    public string Local { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Medio { get; set; } = "";
    public bool PasaPorPayway { get; set; }
    public int CantidadOperaciones { get; set; }
    public decimal Total { get; set; }
}
