namespace MarketWeb.Shared.Produccion;

/// <summary>Una fila del listado de viajes (cabecera + conteos).</summary>
public sealed class ViajeDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public DateTime? Fecha { get; set; }
    public string Pais { get; set; } = "";
    public string Estado { get; set; } = "";
    public DateTime? FechaImportacion { get; set; }
    public bool ManejaContenedores { get; set; } = true;
    public string Moneda { get; set; } = "";               // moneda del viaje (RMB/USD/CLP/...), no se asume Yuan
    public string UnidadTransporte { get; set; } = "Contenedor";   // nombre de la unidad de carga: Contenedor (barco) / Camión
    public int CantArticulos { get; set; }
    public int CantProveedores { get; set; }
    public int CantContenedores { get; set; }
}

/// <summary>Un artículo del viaje (listado, agrupado por proveedor).</summary>
public sealed class ViajeArticuloDto
{
    public int Id { get; set; }
    public int? NumeroGeneral { get; set; }
    public string CodigoInterno { get; set; } = "";
    public string CodigoProveedor { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Genero { get; set; } = "";
    public string Proveedor { get; set; } = "";
    public string Contenedor { get; set; } = "";
    public string Talles { get; set; } = "";
    public string Colores { get; set; } = "";
    public int? CajasPedidas { get; set; }          // bultos
    public int? CantidadTotalPrendas { get; set; }
    public decimal? CostoUsdUnit { get; set; }       // FOB sin nacionalizar (USD/prenda)
    public decimal? CostoArsUnit { get; set; }       // nacionalizado + servicios del proveedor (AR$/prenda)
    public decimal? CbmCaja { get; set; }
    public string CodigoMarket { get; set; } = "";    // ARTCOD de Dragon (equivalencia por código de proveedor)
    public bool ExisteEnDragon { get; set; }          // el ARTCOD existe en DRAGONFISH_CENTRAL.ART
    public string FotoPrincipal { get; set; } = "";   // nombre de archivo (se sirve por api/viajes/foto)
}

/// <summary>Una foto de un artículo.</summary>
public sealed class ViajeFotoDto
{
    public string Archivo { get; set; } = "";
    public bool EsPrincipal { get; set; }
}

/// <summary>Ficha técnica completa de un artículo (réplica de detalle_articulo de la app).</summary>
public sealed class ViajeArticuloFichaDto
{
    public int Id { get; set; }
    public int IdViaje { get; set; }
    public int? IdProveedor { get; set; }
    public int? NumeroGeneral { get; set; }
    public string CodigoInterno { get; set; } = "";
    public string CodigoProveedor { get; set; } = "";   // "codigo" en la app
    public string Descripcion { get; set; } = "";
    public string Proveedor { get; set; } = "";
    public string Genero { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Material { get; set; } = "";
    public string Contenedor { get; set; } = "";
    // talles / colores
    public string Talles { get; set; } = "";
    public string CurvaTalles { get; set; } = "";
    public string TablaTalles { get; set; } = "";
    public string Colores { get; set; } = "";
    public string ColoresProveedor { get; set; } = "";
    // costo / combo (tasas para recálculo en vivo, como en la app)
    public decimal? PrecioYuanes { get; set; }
    public decimal? PDesc { get; set; }
    public decimal? PNac { get; set; }
    public decimal? TasaRmb { get; set; }
    public decimal? TasaArs { get; set; }
    public string TipoDolar { get; set; } = "";
    public string ComboGuardado { get; set; } = "";
    // volúmenes
    public decimal? CbmUnitario { get; set; }
    public decimal? CbmCaja { get; set; }
    public int? CajasPedidas { get; set; }
    public int? PacksPorCaja { get; set; }
    public int? CantidadTotalPrendas { get; set; }
    public string TipoBulto { get; set; } = "";
    public int? DiasEntrega { get; set; }
    public string Observaciones { get; set; } = "";
    public string PacksArmados { get; set; } = "";   // JSON
    public string CodigoMarket { get; set; } = "";   // ARTCOD de Dragon EFECTIVO (manual si hay, si no el de la planilla)
    public string CodigoMarketManual { get; set; } = ""; // override cargado a mano (vacío = usa la planilla)
    public bool ExisteEnDragon { get; set; }          // el ARTCOD existe en DRAGONFISH_CENTRAL.ART
    public string DescripcionMarket { get; set; } = "";
    public List<ViajeFotoDto> Fotos { get; set; } = new();
}

/// <summary>Resultado de importar la planilla de equivalencias (código proveedor → código MARKET).</summary>
public sealed class ImportarCodigosResultadoDto
{
    public bool Ok { get; set; }
    public int Procesados { get; set; }
    public int Total { get; set; }       // total en la tabla luego del import
    public string Mensaje { get; set; } = "";
}

/// <summary>Un proveedor (global, listado dentro del viaje).</summary>
public sealed class ViajeProveedorDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Ciudad { get; set; } = "";
    public string Pais { get; set; } = "";
    public string Celular { get; set; } = "";
    public string Email { get; set; } = "";
    public int? DiasEntrega { get; set; }
    public string FotoPrincipal { get; set; } = "";
    public int CantArticulos { get; set; }
}

/// <summary>Un contenedor del viaje.</summary>
public sealed class ViajeContenedorDto
{
    public int Id { get; set; }
    public string NombreContenedor { get; set; } = "";
    public string Tipo { get; set; } = "";
    public decimal? CapacidadMaxCbm { get; set; }
    public int CantArticulos { get; set; }
    public decimal CbmUsado { get; set; }
}

// ---- ABM (alta/baja/modificación desde la web) ----

/// <summary>Alta/edición de la cabecera del viaje.</summary>
public sealed class ViajeSaveRequest
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public DateTime? Fecha { get; set; }
    public string Pais { get; set; } = "";
    public string Estado { get; set; } = "ABIERTO";
    public bool ManejaContenedores { get; set; } = true;
    public string Moneda { get; set; } = "USD";
    public string UnidadTransporte { get; set; } = "Contenedor";
}

/// <summary>Contenedor para el editor.</summary>
public sealed class ContenedorEditorDto
{
    public int Id { get; set; }
    public int IdViaje { get; set; }
    public string NombreContenedor { get; set; } = "";
    public string Tipo { get; set; } = "";
    public decimal? CapacidadMaxCbm { get; set; }
}

/// <summary>Alta/edición de contenedor.</summary>
public sealed class ContenedorSaveRequest
{
    public int Id { get; set; }
    public int IdViaje { get; set; }
    public string NombreContenedor { get; set; } = "";
    public string Tipo { get; set; } = "";
    public decimal? CapacidadMaxCbm { get; set; }
}

/// <summary>Proveedor para el editor / alta-edición.</summary>
public sealed class ProveedorEditorDto
{
    public int Id { get; set; }
    public int IdViaje { get; set; }   // proveedor del viaje (los nuevos pertenecen al viaje)
    public string Nombre { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Ciudad { get; set; } = "";
    public string Pais { get; set; } = "";
    public string Celular { get; set; } = "";
    public string Email { get; set; } = "";
    public string Broker { get; set; } = "";
    public int? DiasEntrega { get; set; }
    public string Observaciones { get; set; } = "";
}

/// <summary>Alta/edición de artículo (datos; la curva/packs/costos/fotos se completan en su propia sección).</summary>
public sealed class ArticuloEditorDto
{
    public int Id { get; set; }
    public int IdViaje { get; set; }
    public int? IdProveedor { get; set; }
    public int? IdContenedor { get; set; }
    public string CodigoInterno { get; set; } = "";
    public string CodigoProveedor { get; set; } = "";
    public int? NumeroGeneral { get; set; }
    public string Descripcion { get; set; } = "";
    public string Genero { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Material { get; set; } = "";
    public string Talles { get; set; } = "";
    public string Colores { get; set; } = "";
    public int? PrendasPorPack { get; set; }
    public int? PacksPorCaja { get; set; }
    public int? CajasPedidas { get; set; }
    public int? CantidadTotalPrendas { get; set; }
    public decimal? CbmCaja { get; set; }
    public decimal? PrecioYuanes { get; set; }   // precio en la moneda del viaje
    public decimal? PDesc { get; set; }
    public decimal? PNac { get; set; }
    public decimal? TasaRmb { get; set; }
    public decimal? TasaArs { get; set; }
    public string TipoDolar { get; set; } = "";
    public int? MoqUnidades { get; set; }
    public int? MoqColores { get; set; }
    public int? DiasEntrega { get; set; }
    public string Observaciones { get; set; } = "";
}

/// <summary>Resultado del import del .db (espejo a MARKET).</summary>
public sealed class ImportarViajeResultadoDto
{
    public bool Ok { get; set; }
    public string Mensaje { get; set; } = "";
    public int Viajes { get; set; }
    public int Proveedores { get; set; }
    public int Contenedores { get; set; }
    public int Articulos { get; set; }
    public int Fotos { get; set; }
    public int FotosCopiadas { get; set; }
    public int Catalogos { get; set; }
}
