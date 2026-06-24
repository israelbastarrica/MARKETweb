namespace MarketWeb.Shared.Packs;

/// <summary>Un renglón del reporte de packs (espejo de la grilla de FrmRepoPack):
/// agrupado por pedido + artículo + Pack. Los archivos PDF/TXT se traen aparte por endpoint.</summary>
public sealed class PackDto
{
    public int Id { get; set; }                 // Packs.ID
    public string NroPedido { get; set; } = "";
    public string? NroInterno { get; set; }
    public string ArtCod { get; set; } = "";
    public int CantPacks { get; set; }
    public int CantBolsas { get; set; }
    public int CantPrendas { get; set; }
    public bool TienePdf { get; set; }
    public bool TieneTxt { get; set; }
}
