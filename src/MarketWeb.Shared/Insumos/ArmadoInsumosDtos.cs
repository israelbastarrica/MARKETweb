namespace MarketWeb.Shared.Insumos;

/// <summary>Hoja de ruta de armado: los pedidos pendientes con su detalle (para imprimir).</summary>
public sealed class ArmadoInsumosDto
{
    public List<ArmadoPedidoDto> Pedidos { get; set; } = new();
}

public sealed class ArmadoPedidoDto
{
    public int Id { get; set; }
    public string Local { get; set; } = "";
    public int NroPedido { get; set; }
    public List<ArmadoRenglonDto> Renglones { get; set; } = new();
}

public sealed class ArmadoRenglonDto
{
    public string ArtCod { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int Cantidad { get; set; }
}
