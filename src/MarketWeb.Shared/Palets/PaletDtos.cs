namespace MarketWeb.Shared.Palets;

/// <summary>Un palet en el reporte (espejo de frmRepoPalets / sp_ConsultaPalets).</summary>
public sealed class PaletDto
{
    public int Id { get; set; }
    public int NroPalet { get; set; }
    public string Remitos { get; set; } = "";       // RemitosDesc ("REMITOS: 10" o "DESARMADO")
    public string Ubicacion { get; set; } = "";      // UbicacionDesc (mapeo del depósito)
    public string Articulos { get; set; } = "";       // lista de códigos
    public string? Tipo { get; set; }
    public string? Categoria { get; set; }
    public bool Impreso { get; set; }
    public bool Desarmado { get; set; }
}

/// <summary>Un artículo dentro de un palet (detalle, resuelto contra Dragonfish).</summary>
public sealed class PaletArticuloDto
{
    public string Origen { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? Combo { get; set; }
    public string? Remito { get; set; }
    public decimal Cantidad { get; set; }
}
