namespace MarketWeb.Shared.Dragonfish;

/// <summary>Un renglón del remito a crear en Dragonfish.</summary>
public sealed class DragonRemitoItemDto
{
    public string Articulo { get; set; } = "";   // ARTCOD
    public string Color { get; set; } = "";       // vacío para artículos sin variante (ej. insumos ZZ)
    public string Talle { get; set; } = "";
    public int Cantidad { get; set; }
}

/// <summary>Pedido para crear un remito de venta CENTRAL→local en Dragonfish.</summary>
public sealed class DragonRemitoRequest
{
    public string Local { get; set; } = "";       // LURO / PERALTA (Cliente del remito)
    public List<DragonRemitoItemDto> Items { get; set; } = new();
    // Va al campo InformacionAdicional del remito: la licencia/terminal destino para que el
    // agente de impresión sepa a qué impresora mandarlo (el alta por API queda con SALTAFW del server).
    public string InformacionAdicional { get; set; } = "";
}

/// <summary>Artículo de la última repo (para autocomplete en la tablet).</summary>
public sealed class UltimaRepoItemDto
{
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
}

/// <summary>Datos de un artículo para el formulario de remito: descripción + variantes Color/Talle de COMB.</summary>
public sealed class ArticuloLookupDto
{
    public string Cod { get; set; } = "";
    public string Des { get; set; } = "";
    public List<string> Colores { get; set; } = new();
    public List<string> Talles { get; set; } = new();
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
