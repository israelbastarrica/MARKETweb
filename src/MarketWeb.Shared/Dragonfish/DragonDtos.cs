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
    public string Motivo { get; set; } = "13";     // 02=REPOSICION, 13=insumos. Default 13 por compat.
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
    /// <summary>Variantes (color+talle) reales del artículo, de COMB. Para armar la grilla de talles por color.</summary>
    public List<ComboVarianteDto> Variantes { get; set; } = new();
}

/// <summary>Motivo de remito (Dragon ZooLogic.MOTIVO): código + descripción.</summary>
public sealed class MotivoDto
{
    public string Cod { get; set; } = "";
    public string Des { get; set; } = "";
}

/// <summary>Una variante color+talle de un artículo (fila de COMB).</summary>
public sealed class ComboVarianteDto
{
    public string Color { get; set; } = "";
    public string Talle { get; set; } = "";
}

/// <summary>Una bolsa del depósito (PacksBolsas) con su detalle, leída por código de barras (NroBolsa).</summary>
public sealed class BolsaDto
{
    public string NroBolsa { get; set; } = "";
    public int IdPackBolsa { get; set; }
    public bool Eliminada { get; set; }   // se encontró solo cayendo al fallback sin filtro (bolsa con baja lógica)
    public List<BolsaRenglonDto> Renglones { get; set; } = new();
}

/// <summary>Un renglón de una bolsa (PacksBolsasDetalle): artículo + color + talle + cantidad.</summary>
public sealed class BolsaRenglonDto
{
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public string Color { get; set; } = "";
    public string Talle { get; set; } = "";
    public int Cantidad { get; set; }
}

/// <summary>Resultado de buscar un remito por su CODIGO (QR de la bolsa) en las 3 bases.</summary>
public sealed class RemitoPorCodigoDto
{
    public bool Encontrado { get; set; }
    public bool Anulado { get; set; }
    public string? Origen { get; set; }   // CENTRAL / LURO / PERALTA donde se encontró
    public List<BolsaRenglonDto> Renglones { get; set; } = new();   // vacío si está anulado
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
