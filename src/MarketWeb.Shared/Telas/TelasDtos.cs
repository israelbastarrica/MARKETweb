using System.ComponentModel.DataAnnotations;

namespace MarketWeb.Shared.Telas;

/// <summary>Una fila del catálogo de telas (con nombres resueltos y Gramos/m² calculado por la base).</summary>
public sealed class TelaDto
{
    public int Id { get; set; }
    public int IdDeposito { get; set; }
    public string Deposito { get; set; } = "";
    public string Material { get; set; } = "";
    public int? IdTextil { get; set; }
    public string? Textil { get; set; }
    public decimal? AnchoCm { get; set; }
    public string? Composicion { get; set; }
    public decimal? RindeMKg { get; set; }
    public int? IdColor { get; set; }
    public string? Color { get; set; }
    public string? ColorCodigo { get; set; }
    public decimal? GramosM2 { get; set; }   // calculado en la base: 1000/(Rinde*Ancho_m)
}

/// <summary>Alta/modificación de una tela.</summary>
public sealed class TelaSaveRequest
{
    [Required(ErrorMessage = "Debe elegir el depósito.")]
    [Range(1, int.MaxValue, ErrorMessage = "Debe elegir el depósito.")]
    public int IdDeposito { get; set; }

    [Required(ErrorMessage = "Debe ingresar el material.")]
    [MaxLength(200)]
    public string Material { get; set; } = "";

    public int? IdTextil { get; set; }
    public decimal? AnchoCm { get; set; }

    [MaxLength(300)]
    public string? Composicion { get; set; }

    public decimal? RindeMKg { get; set; }
    public int? IdColor { get; set; }
}

/// <summary>Tipos de catálogo editable de Telas.</summary>
public static class CatalogoTela
{
    public const string Depositos = "depositos";
    public const string Textiles = "textiles";
    public const string Colores = "colores";
}

/// <summary>Un ítem de un catálogo (depósito / textil / color). Codigo solo aplica a colores (hex).</summary>
public sealed class CatalogoItemDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Codigo { get; set; }
}

/// <summary>Alta/modificación de un ítem de catálogo.</summary>
public sealed class CatalogoSaveRequest
{
    [Required(ErrorMessage = "Debe ingresar el nombre.")]
    [MaxLength(100)]
    public string Nombre { get; set; } = "";

    [MaxLength(20)]
    public string? Codigo { get; set; }   // hex #RRGGBB (solo colores)
}
