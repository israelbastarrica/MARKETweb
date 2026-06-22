namespace MarketWeb.Shared.Dragonfish;

/// <summary>Un renglón del remito de insumos a crear en Dragonfish.</summary>
public sealed class DragonRemitoItemDto
{
    public string Articulo { get; set; } = "";   // ARTCOD (insumos empiezan con ZZ)
    public int Cantidad { get; set; }
}

/// <summary>Pedido para crear un remito de venta CENTRAL→local en Dragonfish.</summary>
public sealed class DragonRemitoRequest
{
    public string Local { get; set; } = "";       // LURO / PERALTA (Cliente del remito)
    public List<DragonRemitoItemDto> Items { get; set; } = new();
}

/// <summary>Resultado del POST a Dragonfish (incluye lo enviado y la respuesta cruda para diagnosticar).</summary>
public sealed class DragonRemitoResultDto
{
    public bool Ok { get; set; }
    public int? HttpStatus { get; set; }
    public string Respuesta { get; set; } = "";   // cuerpo crudo de Dragon
    public string JsonEnviado { get; set; } = "";
    public string? Codigo { get; set; }
    public long? Numero { get; set; }
    public string? Error { get; set; }
}
