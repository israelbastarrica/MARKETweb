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

/// <summary>Un ítem del catálogo (ARTICULO / TEXTO / OP), resuelto para mostrar en la grilla.</summary>
public sealed class CatalogoRenglonDto
{
    public string Tipo { get; set; } = "ARTICULO";   // ARTICULO / TEXTO / OP
    public string Valor { get; set; } = "";           // ARTCOD, texto libre, o id de PedidosOrdenes
    public string Descripcion { get; set; } = "";     // desc de Dragon (ARTICULO) o el texto (TEXTO)
    public string Categoria { get; set; } = "";
    public int Orden { get; set; }
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
