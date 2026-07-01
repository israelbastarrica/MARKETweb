namespace MarketWeb.Shared.Produccion;

/// <summary>Una fila del listado de catálogos (cabecera + conteo de ítems).</summary>
public sealed class CatalogoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int? Anio { get; set; }
    public string Temporada { get; set; } = "";
    public int CantItems { get; set; }
    public bool TienePdf { get; set; }
}

/// <summary>
/// Un ítem del catálogo, resuelto para la grilla. Espeja la fila de dgvReporte del .Net.
/// Tipo: "ARTÍCULO" | "TEXTO" | "OP {nroOrden}" | "DG {nroProforma}".
/// Codigo = ARTCOD mostrado (vacío en TEXTO). RefValor = valor a persistir cuando NO es el código
/// (en OP se guarda el ID de PedidosOrdenes; en el resto el propio código/texto).
/// </summary>
public sealed class CatalogoRenglonDto
{
    public string Tipo { get; set; } = "ARTÍCULO";
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Categoria { get; set; } = "";
    public int Orden { get; set; }
    public string? RefValor { get; set; }
    public bool ExisteEnDragon { get; set; }
}

/// <summary>Cabecera + ítems de un catálogo (pantalla de detalle / ABM).</summary>
public sealed class CatalogoDetalleDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int? Anio { get; set; }
    public string Temporada { get; set; } = "";
    public List<CatalogoRenglonDto> Items { get; set; } = new();
}

/// <summary>Alta/edición de un catálogo (cabecera + lista completa de ítems, en orden).</summary>
public sealed class CatalogoGuardarRequest
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int? Anio { get; set; }
    public string Temporada { get; set; } = "";
    public List<CatalogoRenglonDto> Items { get; set; } = new();
}

/// <summary>Combos de cabecera (Año / Temporada), traídos de Dragon (ART / TEMPORADA).</summary>
public sealed class CatalogoCombosDto
{
    public List<int> Anios { get; set; } = new();
    public List<string> Temporadas { get; set; } = new();
}

/// <summary>Fila para el selector de Órdenes de Pedido (PedidosOrdenes) — opción "Orden de Pedido" del botón Agregar.</summary>
public sealed class PedidoOrdenSelDto
{
    public int Id { get; set; }
    public int NroOrden { get; set; }
    public string ARTCOD { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Tipo { get; set; } = "";
}
