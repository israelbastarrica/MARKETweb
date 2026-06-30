namespace MarketWeb.Shared.Produccion;

/// <summary>Una fila del listado de órdenes de pedido (cabecera + conteo).</summary>
public sealed class OrdenDto
{
    public int Id { get; set; }
    public int NroOrden { get; set; }
    public string Tipo { get; set; } = "";          // NACIONAL / IMPORTADO
    public string Estado { get; set; } = "";
    public string ProveedorNombre { get; set; } = "";
    public string Temporada { get; set; } = "";
    public int? Anio { get; set; }
    public DateTime? FechaLlegada { get; set; }
    public int CantRenglones { get; set; }
    public bool Finalizada { get; set; }
}

/// <summary>Cabecera + renglones de una orden (pantalla de detalle / ABM).</summary>
public sealed class OrdenDetalleDto
{
    public int Id { get; set; }
    public int NroOrden { get; set; }
    public string Tipo { get; set; } = "";
    public string Estado { get; set; } = "";
    public string ProveedorCod { get; set; } = "";
    public string ProveedorNombre { get; set; } = "";
    public int? IdViaje { get; set; }
    public string Moneda { get; set; } = "";
    public DateTime? FechaLlegada { get; set; }
    public string Etiquetador { get; set; } = "";
    public string Temporada { get; set; } = "";
    public int? Anio { get; set; }
    public string Material { get; set; } = "";
    public string Familia { get; set; } = "";
    public string Subfamilia { get; set; } = "";
    public bool Finalizada { get; set; }
    public List<OrdenRenglonDto> Renglones { get; set; } = new();
}

/// <summary>Un renglón (artículo) de la orden.</summary>
public sealed class OrdenRenglonDto
{
    public int Id { get; set; }
    public string ARTCOD { get; set; } = "";
    public string CodigoProveedor { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public bool ExisteEnDragon { get; set; }
    public bool TieneFicha { get; set; }
    public string EquiTalle { get; set; } = "";
    public string MobiliarioDestino { get; set; } = "";   // Armado
    public int? Cantidad { get; set; }
    public int? Packs { get; set; }
    public decimal? CostoUnit { get; set; }
    public decimal? PrecioVenta { get; set; }
    public string Origen { get; set; } = "";
    public string Estado { get; set; } = "";
    public bool Finalizada { get; set; }
    public int? NroItem { get; set; }
    // ---- Datos del "machete" (Nacional) ----
    public string Corte { get; set; } = "";            // de dónde sale la tela (ej "Retira de lo de Lito")
    public string Prioridad { get; set; } = "";        // ej Urgente / Normal
    public string Talles { get; set; } = "";           // ej "S M L XL"
    public string Curva { get; set; } = "";            // ej "1-1-2-2"
    public string FechaEntregaTexto { get; set; } = ""; // fecha o texto ("Consultar capacidad productiva")
}

/// <summary>Alta/edición de la cabecera de la orden.</summary>
public sealed class OrdenSaveRequest
{
    public int Id { get; set; }
    public int NroOrden { get; set; }
    public string Tipo { get; set; } = "IMPORTADO";
    public string Estado { get; set; } = "Borrador";
    public string ProveedorCod { get; set; } = "";
    public string ProveedorNombre { get; set; } = "";
    public int? IdViaje { get; set; }
    public string Moneda { get; set; } = "";
    public DateTime? FechaLlegada { get; set; }
    public string Etiquetador { get; set; } = "";
    public string Temporada { get; set; } = "";
    public int? Anio { get; set; }
    public string Material { get; set; } = "";
    public string Familia { get; set; } = "";
    public string Subfamilia { get; set; } = "";
}

/// <summary>Edición de un renglón (lo que se puede tocar en el ABM de Fase 1).</summary>
public sealed class OrdenRenglonSaveRequest
{
    public int Id { get; set; }
    public int IdOrden { get; set; }
    public string MobiliarioDestino { get; set; } = "";
    public int? Cantidad { get; set; }
    public decimal? CostoUnit { get; set; }
    public decimal? PrecioVenta { get; set; }
    public string Corte { get; set; } = "";
    public string Prioridad { get; set; } = "";
    public string Talles { get; set; } = "";
    public string Curva { get; set; } = "";
    public string FechaEntregaTexto { get; set; } = "";
}

/// <summary>Un rango de la tabla PreciosLista: si el Costo cae entre Desde y Hasta, el precio de venta es Combo.</summary>
public sealed class ComboRangoDto
{
    public decimal Desde { get; set; }
    public decimal Hasta { get; set; }
    public string Combo { get; set; } = "";
}

/// <summary>Combos de Dragon para la cabecera de la orden (Temporada/Material/Familia/Subfamilia=Grupo).</summary>
public sealed class OrdenCabeceraCombosDto
{
    public List<string> Temporadas { get; set; } = new();
    public List<string> Materiales { get; set; } = new();
    public List<string> Familias { get; set; } = new();
    public List<string> Subfamilias { get; set; } = new();
}

/// <summary>Un color del catálogo de Telas (para el combo de colores del renglón).</summary>
public sealed class TelaColorDto
{
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
}

/// <summary>Un color elegido para el artículo, con su cantidad de rollos (bloque "Cantidad/Código color" de la ficha).</summary>
public sealed class OrdenColorDto
{
    public int Id { get; set; }
    public string ColorCod { get; set; } = "";
    public string ColorNombre { get; set; } = "";
    public int? Rollos { get; set; }
}

/// <summary>Una celda de la grilla de producción color×talle: estimado (nuestro) + real (proveedor).</summary>
public sealed class OrdenProduccionCeldaDto
{
    public string ColorCod { get; set; } = "";
    public string Talle { get; set; } = "";
    public int? Estimada { get; set; }
    public int? Real { get; set; }
}

/// <summary>Resultado de importar las órdenes de muestra (una de cada tipo).</summary>
public sealed class ImportarOrdenesResultadoDto
{
    public bool Ok { get; set; }
    public string Mensaje { get; set; } = "";
    public int Ordenes { get; set; }
    public int Renglones { get; set; }
}
