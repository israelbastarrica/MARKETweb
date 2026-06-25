using System.ComponentModel.DataAnnotations;

namespace MarketWeb.Shared.Telas;

/// <summary>Una fila del catálogo de telas. El color sale de Dragonfish (DPCOLOR): se guarda el
/// código (ColorCod) y el nombre (Color) se resuelve por join. Gramos/m² lo calcula la base.</summary>
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
    public string? ColorCod { get; set; }   // DPCOLOR.CODCOL
    public string? Color { get; set; }       // DPCOLOR.DESCRIP (resuelto)
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

    [MaxLength(20)]
    public string? ColorCod { get; set; }   // código de color de Dragonfish
}

/// <summary>Un color de Dragonfish (DRAGONFISH_CENTRAL.Zoologic.DPCOLOR) para el combo.</summary>
public sealed class ColorDragonDto
{
    public string Cod { get; set; } = "";
    public string Nombre { get; set; } = "";
}

/// <summary>Tipos de catálogo editable propio de Telas (los colores NO: vienen de Dragonfish).</summary>
public static class CatalogoTela
{
    public const string Depositos = "depositos";
    public const string Textiles = "textiles";
}

/// <summary>Un ítem de un catálogo propio (depósito / textil).</summary>
public sealed class CatalogoItemDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

/// <summary>Alta/modificación de un ítem de catálogo.</summary>
public sealed class CatalogoSaveRequest
{
    [Required(ErrorMessage = "Debe ingresar el nombre.")]
    [MaxLength(100)]
    public string Nombre { get; set; } = "";
}
