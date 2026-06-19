namespace MarketWeb.Shared.Insumos;

/// <summary>
/// Consumo consolidado de un artículo en una ubicación (registros + pedidos enviados),
/// con proveedor/descripción de Dragonfish. Espejo de frmRepoLocalInsumosAdministracion.
/// </summary>
public sealed class InsumoConsumoDto
{
    public string Proveedor { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public string ArtCod { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int CantidadTotal { get; set; }

    /// <summary>Referencias "R|id,P|id,..." que componen este consumo (para marcar como procesado en fase 2).</summary>
    public string? AgrupacionIds { get; set; }
}
