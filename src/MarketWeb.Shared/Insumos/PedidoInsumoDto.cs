namespace MarketWeb.Shared.Insumos;

/// <summary>Un pedido de insumos (cabecera) con sus totales. Espejo de frmRepoLocalInsumos.</summary>
public sealed class PedidoInsumoDto
{
    public int Id { get; set; }
    public string Ubicacion { get; set; } = "";
    public string NroPedido { get; set; } = "";
    public string Estado { get; set; } = "";
    public int CantArt { get; set; }
    public int CantidadTotal { get; set; }
    public int CantArtEnviada { get; set; }
    public int CantidadTotalEnviada { get; set; }
    public bool Enviado { get; set; }

    /// <summary>Cerrado para el local: DEPÓSITO ya lo imprimió (EN ARMADO) o ya está ENVIADO.</summary>
    public bool Cerrado { get; set; }

    /// <summary>EN ARMADO (impreso) y todavía no enviado.</summary>
    public bool EnArmado { get; set; }

    /// <summary>El depósito ya lo guardó/procesó (acomodó según stock): habilita generar el remito.</summary>
    public bool Procesado { get; set; }
}
