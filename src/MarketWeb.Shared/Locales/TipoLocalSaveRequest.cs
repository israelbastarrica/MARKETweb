using System.ComponentModel.DataAnnotations;

namespace MarketWeb.Shared.Locales;

/// <summary>Datos para alta/modificación de un tipo de local (tabla UbicacionesTipo).</summary>
public sealed class TipoLocalSaveRequest
{
    [Required(ErrorMessage = "Debe ingresar una descripción.")]
    [MaxLength(200)]
    public string Descripcion { get; set; } = "";
}
