using System.ComponentModel.DataAnnotations;

namespace MarketWeb.Shared.UsuariosPc;

public sealed class UsuarioPcSaveRequest
{
    [Required(ErrorMessage = "Debe ingresar el nombre de la PC.")]
    [MaxLength(100)]
    public string Pc { get; set; } = "";

    [Required(ErrorMessage = "Debe ingresar un perfil.")]
    [MaxLength(100)]
    public string Perfil { get; set; } = "";

    // Opcional durante la transición; si se carga, debe ser una cuenta @marketarg.com.
    [MaxLength(200)]
    [RegularExpression(@"^$|^[^@\s]+@marketarg\.com$",
        ErrorMessage = "El mail debe ser una cuenta @marketarg.com.")]
    public string? Mail { get; set; }
}
