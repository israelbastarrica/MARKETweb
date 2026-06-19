using System.ComponentModel.DataAnnotations;

namespace MarketWeb.Shared.Locales;

/// <summary>Datos para alta o modificación de un local (frmABMLocales).</summary>
public sealed class LocalSaveRequest
{
    [Required(ErrorMessage = "Debe seleccionar un tipo.")]
    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un tipo.")]
    public int IdTipo { get; set; }

    [Required(ErrorMessage = "Debe ingresar una descripción.")]
    [MaxLength(200)]
    public string Descripcion { get; set; } = "";
}
