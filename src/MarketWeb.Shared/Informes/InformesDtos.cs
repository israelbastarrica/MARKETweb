namespace MarketWeb.Shared.Informes;

/// <summary>Filtro del informe de ventas (comprado vs vendido por proveedor/temporada/año + rango de venta).</summary>
public sealed class InformeVentaFiltro
{
    public string? ProveedorCod { get; set; }     // ARTFAB; null/"" = todos
    public string? Temporada { get; set; }         // TDES; null/"TODOS" = todas
    public int Anio { get; set; }                  // 0 = todos
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
}

/// <summary>Una fila del informe (por ARTCOD).</summary>
public sealed class InformeVentaFila
{
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public string Temporada { get; set; } = "";
    public string Anio { get; set; } = "";
    public decimal Comprado { get; set; }          // remitos de ingreso; si no hay, lo enviado a locales
    public string FuenteStock { get; set; } = "";  // "Compra" / "Envío"
    public decimal PrecioInicial { get; set; }     // LISTA1 de lanzamiento (más antiguo)
    public decimal PrecioVenta { get; set; }       // LISTA1 vigente al fin del período
    public bool Forzada { get; set; }              // el precio LISTA1 bajó respecto del inicial
    public decimal Vendido { get; set; }
    public decimal Facturado { get; set; }
    public decimal? PrecioVentaProm { get; set; }  // facturado / vendido
    public decimal Costo { get; set; }
    public decimal MargenPesos { get; set; }
    public decimal? MargenPct { get; set; }
}

public sealed class InformeVentaCombosDto
{
    public List<ProveedorItemDto> Proveedores { get; set; } = new();
    public List<string> Temporadas { get; set; } = new();
    public List<int> Anios { get; set; } = new();
}

public sealed class ProveedorItemDto
{
    public string Cod { get; set; } = "";
    public string Nombre { get; set; } = "";
}

/// <summary>Una fila de la serie temporal del gráfico (período × grupo de la dimensión elegida).</summary>
public sealed class InformeSerieFila
{
    public DateTime Periodo { get; set; }   // inicio del período (día / lunes de la semana / 1° del mes)
    public string Grupo { get; set; } = ""; // valor de la dimensión (Familia/Tipo/Categoría/Combo)
    public decimal Unidades { get; set; }
    public decimal Facturado { get; set; }
    public decimal Margen { get; set; }
}
