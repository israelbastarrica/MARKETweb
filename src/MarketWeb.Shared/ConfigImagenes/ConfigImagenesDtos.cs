using System.ComponentModel.DataAnnotations;

namespace MarketWeb.Shared.ConfigImagenes;

/// <summary>
/// Tipos de imagen del catálogo (espejo de los combos de frmABMCatalogoConfigImagenes
/// y frmRepoCatalogosConfigImagenes). Los valores deben coincidir con los que consumen
/// el desktop: Globales.IDEquivalenciaTallePorDescripcion ('EQUIVALENCIAS TALLES') y
/// el matcheo de etiquetas de frmABMCatalogo ('ETIQUETAS PRENDAS').
/// </summary>
public static class TiposConfigImagen
{
    public const string EtiquetasPrendas = "ETIQUETAS PRENDAS";
    public const string EquivalenciasTalles = "EQUIVALENCIAS TALLES";

    /// <summary>Tipos válidos para el alta/modificación.</summary>
    public static readonly string[] Todos = { EtiquetasPrendas, EquivalenciasTalles };
}

/// <summary>Una tarjeta de la galería (espejo de ucTarjetaImagen). Sin los bytes de la imagen:
/// la imagen se trae aparte por <c>GET api/configimagenes/{id}/imagen</c>.</summary>
public sealed class ConfigImagenDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string? MatchArticulo { get; set; }
    public bool TieneImagen { get; set; }
}

/// <summary>Alta/modificación de una imagen del catálogo (espejo de frmABMCatalogoConfigImagenes).</summary>
public sealed class ConfigImagenSaveRequest
{
    [Required(ErrorMessage = "Debe ingresar el tipo de la imagen.")]
    [MaxLength(100)]
    public string Tipo { get; set; } = "";

    [Required(ErrorMessage = "Debe ingresar la descripción.")]
    [MaxLength(255)]
    public string Descripcion { get; set; } = "";

    /// <summary>Palabras clave separadas por coma (solo aplica a ETIQUETAS PRENDAS). Ej: REM,SW,CARD.</summary>
    [MaxLength(500)]
    public string? MatchArticulo { get; set; }

    /// <summary>Imagen nueva en base64 (sin el prefijo data:). En modificación, null = conservar la actual.
    /// En alta es obligatoria (se valida en el servicio).</summary>
    public string? ImagenBase64 { get; set; }
}
