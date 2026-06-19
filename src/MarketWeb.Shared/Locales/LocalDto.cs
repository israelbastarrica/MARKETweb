namespace MarketWeb.Shared.Locales;

/// <summary>
/// Una ubicación/local. Equivale a una fila de la grilla de frmRepoLocales
/// (Ubicaciones + su tipo desde UbicacionesTipo).
/// </summary>
public sealed class LocalDto
{
    public int Id { get; set; }
    public string Local { get; set; } = "";
    public string Tipo { get; set; } = "";
    public int IdTipo { get; set; }
}
