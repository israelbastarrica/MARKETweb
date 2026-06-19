using System.ComponentModel.DataAnnotations;

namespace MarketWeb.Shared.Mapeo;

/// <summary>Una ubicación que tiene mapeo (lista principal, espejo de frmRepoMapeo).</summary>
public sealed class MapeoUbicacionDto
{
    public int IdUbicacion { get; set; }
    public string Ubicacion { get; set; } = "";
    public string Tipo { get; set; } = "";
    public int Posiciones { get; set; }
}

/// <summary>Una posición física del mapeo (una fila de la tabla Mapeo).</summary>
public sealed class MapeoPosicionDto
{
    public int Id { get; set; }
    public int IdUbicacion { get; set; }
    public string? Sector { get; set; }
    public string? Modulo { get; set; }
    public string? Mobiliario { get; set; }
    public int? Fila { get; set; }
    public int? Posicion { get; set; }
    public string? Panel { get; set; }
    public int? OrdenPasillo { get; set; }
    public int? FilaOrden { get; set; }
    public bool NoReposicion { get; set; }
    public int Articulos { get; set; }
    public string? ArtCods { get; set; }   // códigos activos concatenados, para filtrar por artículo

    // Coordenadas para el mapa/impresión (avanzado).
    public double CoordX { get; set; }
    public double CoordY { get; set; }
    public double CoordXLenceria { get; set; }
    public double CoordYLenceria { get; set; }
    public double CoordXCodigo { get; set; }
    public double CoordYCodigo { get; set; }
    public double CoordXDesc { get; set; }
    public double CoordYDesc { get; set; }
}

/// <summary>Alta/modificación de una posición de mapeo (espejo de frmABMMapeoDetalle).</summary>
public sealed class MapeoPosicionSaveRequest
{
    public int IdUbicacion { get; set; }

    [Required(ErrorMessage = "Debe ingresar el sector.")]
    [MaxLength(100)]
    public string Sector { get; set; } = "";

    [Required(ErrorMessage = "Debe ingresar el módulo.")]
    [MaxLength(100)]
    public string Modulo { get; set; } = "";

    [MaxLength(100)]
    public string? Mobiliario { get; set; }

    public int? Fila { get; set; }
    public int? Posicion { get; set; }

    [MaxLength(100)]
    public string? Panel { get; set; }

    public int? OrdenPasillo { get; set; }
    public int? FilaOrden { get; set; }
    public bool NoReposicion { get; set; }

    public double CoordX { get; set; }
    public double CoordY { get; set; }
    public double CoordXLenceria { get; set; }
    public double CoordYLenceria { get; set; }
    public double CoordXCodigo { get; set; }
    public double CoordYCodigo { get; set; }
    public double CoordXDesc { get; set; }
    public double CoordYDesc { get; set; }
}

/// <summary>Un artículo asignado a una posición (una fila de MapeoRegistro).</summary>
public sealed class MapeoArticuloDto
{
    public int Id { get; set; }
    public string ArtCod { get; set; } = "";
    public string? Descripcion { get; set; }
}

/// <summary>Filtros del reporte de mapeo (Logística, SP_ReporteMapeo_Generar).</summary>
public sealed class MapeoReporteRequest
{
    public string FiltroUbicacion { get; set; } = "LOCALES";  // TODOS | LOCALES | ESPECIFICO
    public int? IdUbicacion { get; set; }
    public string? Sector { get; set; }
    public string? Mobiliario { get; set; }
    public int? Fila { get; set; }
    public int? Posicion { get; set; }
    public string? CodArt { get; set; }
    public bool SoloVacios { get; set; }
    public bool CalculaStock { get; set; }
    public string? Tipo { get; set; }
    public string? Categoria { get; set; }
    public int Anio { get; set; }
    public string? Descripcion { get; set; }
}

/// <summary>Un renglón del reporte de mapeo (posición + artículo/palet).</summary>
public sealed class MapeoReporteDto
{
    public string Ubicacion { get; set; } = "";
    public string? Pasillo { get; set; }
    public int Fila { get; set; }
    public int Posicion { get; set; }
    public string? Modulo { get; set; }
    public string? Mobiliario { get; set; }   // o NroPalet si es depósito
    public string? ArtCod { get; set; }
    public int? RegId { get; set; }
    public string? DescripcionFinal { get; set; }
    public string? PrecioFinal { get; set; }
    public decimal Stock { get; set; }
}

/// <summary>Alta de un artículo en una posición (espejo de frmABMMapeoRegistro).</summary>
public sealed class MapeoArticuloSaveRequest
{
    public int IdMapeo { get; set; }

    [Required(ErrorMessage = "Debe ingresar el código de artículo.")]
    [MaxLength(100)]
    public string ArtCod { get; set; } = "";
}
