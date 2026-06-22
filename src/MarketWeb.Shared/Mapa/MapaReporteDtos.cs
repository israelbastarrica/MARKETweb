namespace MarketWeb.Shared.Mapa;

/// <summary>
/// Filtros del reporte de Artículos (modo "Artículos" de frmRepoMapa → sp_ConsultaArticulos).
/// Fase 1: solo el modo Artículos. Los sub-reportes de Stock vienen en fases siguientes.
/// </summary>
public sealed class MapaReporteFiltro
{
    public string? CodArt { get; set; }
    public string? Descripcion { get; set; }
    public string? CodProveedor { get; set; }   // código del proveedor (texto)
    public string? Tipo { get; set; }
    public string? Combo { get; set; }
    public string? Familia { get; set; }
    public string? Temporada { get; set; }
    public int Anio { get; set; }                // 0 = todos
    public string? Categoria { get; set; }
    public bool SoloEnLocales { get; set; }
    public bool LocalesSinFotoGoogle { get; set; }
    public bool FiltraFechaAlta { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
}

/// <summary>Una fila del reporte (salida de sp_ConsultaArticulos).</summary>
public sealed class MapaReporteFila
{
    public int Orden { get; set; }
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? Proveedor { get; set; }
    public string? Combo { get; set; }
    public string? Familia { get; set; }
    public string? Tipo { get; set; }
    public string? Temporada { get; set; }
    public int Anio { get; set; }
    public string? Categoria { get; set; }
    public decimal StockCentral { get; set; }
    public decimal StockCCentral { get; set; }
    public decimal StockLuro { get; set; }
    public decimal StockPeralta { get; set; }
    public string? Posiciones { get; set; }      // "Pasillo|Fila;..." (alimenta el resaltado del mapa)
    public int CantUbis { get; set; }
}

/// <summary>Resultado del reporte: filas (grilla) + módulos a resaltar en el visor 3D.</summary>
public sealed class MapaReporteResultado
{
    public List<MapaReporteFila> Filas { get; set; } = new();
    public List<string> Modulos { get; set; } = new();   // nombres de módulo (mesh) para resaltar en el 3D
}

public sealed class MapaProveedorDto
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
}

/// <summary>Opciones de los combos de filtro (modo Artículos).</summary>
public sealed class MapaCombosDto
{
    public List<string> Tipos { get; set; } = new();
    public List<string> Combos { get; set; } = new();
    public List<string> Familias { get; set; } = new();
    public List<string> Temporadas { get; set; } = new();
    public List<string> Categorias { get; set; } = new();
    public List<int> Anios { get; set; } = new();
    public List<MapaProveedorDto> Proveedores { get; set; } = new();
}
