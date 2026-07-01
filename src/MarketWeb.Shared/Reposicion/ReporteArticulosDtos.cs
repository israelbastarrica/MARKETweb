namespace MarketWeb.Shared.Reposicion;

/// <summary>Una fila del Reporte de Artículos (sp_ConsultaArticulos), enfoque Logística (packs).</summary>
public sealed class ArticuloReporteDto
{
    public int Orden { get; set; }
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Proveedor { get; set; } = "";
    public string Combo { get; set; } = "";
    public string Familia { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Temporada { get; set; } = "";
    public string Anio { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Subfamilia { get; set; } = "";
    public int? CantPack { get; set; }
    public int? PacksReemplazo { get; set; }
    public DateTime? FechaPack { get; set; }
}

/// <summary>Filtros del reporte (espejo de los parámetros de sp_ConsultaArticulos).</summary>
public sealed class ReporteArticulosFiltro
{
    public string? CodArt { get; set; }
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = "TODOS";
    public string Combo { get; set; } = "TODOS";
    public string Familia { get; set; } = "TODOS";
    public string Temporada { get; set; } = "TODOS";
    public string Categoria { get; set; } = "TODOS";
    public string Subfamilia { get; set; } = "TODOS";   // Grupo (GRUPO.DESCRIP); se filtra en el servicio
    public string ProveedorCod { get; set; } = "";   // código del proveedor; "" = todos
    public int Anio { get; set; }                     // 0 = todos
    public int Stock { get; set; }                    // 0 ninguno,1 CENTRAL,2 LURO,3 PERALTA,4 LOCALES,5 CCENTRAL
    public bool SoloEnLocales { get; set; }
    public bool FiltraFechaAlta { get; set; }
    public DateTime FechaDesde { get; set; } = DateTime.Today.AddMonths(-3);
    public DateTime FechaHasta { get; set; } = DateTime.Today;
}

public sealed class ProveedorComboDto
{
    public string Cod { get; set; } = "";
    public string Nombre { get; set; } = "";
}

/// <summary>Listas para los combos de filtro.</summary>
public sealed class ReporteArticulosCombosDto
{
    public List<string> Tipos { get; set; } = new();
    public List<string> Combos { get; set; } = new();
    public List<string> Familias { get; set; } = new();
    public List<string> Temporadas { get; set; } = new();
    public List<string> Anios { get; set; } = new();
    public List<string> Categorias { get; set; } = new();
    public List<string> Subfamilias { get; set; } = new();
    public List<ProveedorComboDto> Proveedores { get; set; } = new();
}

/// <summary>Edición masiva de packs (ArticulosDatosAdiciones) sobre los artículos seleccionados.</summary>
public sealed class GuardarPacksRequest
{
    public List<string> ArtCods { get; set; } = new();
    public bool ModificarCantPack { get; set; } = true;
    public int CantPack { get; set; } = 60;
    public bool ModificarPacksReemplazo { get; set; }
    public int PacksReemplazo { get; set; } = 1;
}
