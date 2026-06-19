namespace MarketWeb.Shared.Articulos;

/// <summary>Ficha de consulta de un artículo (espejo de frmConsultaArticulos).</summary>
public sealed class ConsultaArticuloDto
{
    public string ArtCod { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Precio { get; set; } = "";       // CLASIFART (texto, ej "2X40000")
    public int CantPack { get; set; }
    public decimal StockLocal { get; set; }
    public decimal EnTransito { get; set; }
    public decimal StockDeposito { get; set; }
    public bool EnDeposito { get; set; }
    public List<UbicacionArtDto> Ubicaciones { get; set; } = new();
}

/// <summary>Una posición de góndola (mapeo) del artículo.</summary>
public sealed class UbicacionArtDto
{
    public string Ubicacion { get; set; } = "";
    public string Sector { get; set; } = "";
    public string Fila { get; set; } = "";
    public string Posicion { get; set; } = "";
    public bool EsDeposito { get; set; }
    public int? Palet { get; set; }   // si el artículo está dentro de un palet (depósito)
}
