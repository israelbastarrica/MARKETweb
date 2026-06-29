using System.ComponentModel.DataAnnotations;

namespace MarketWeb.Shared.Telas;

/// <summary>Un ítem de catálogo (material/color/depósito/telera) para combos y selectores.</summary>
public sealed class CatalogoItemDto
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = "";   // descripción/nombre según el catálogo
}

/// <summary>Una barra del gráfico: rollos y cantidad por N° de pedido.</summary>
public sealed class PedidoBarraDto
{
    public string Pedido { get; set; } = "";
    public int CantRollos { get; set; }
    public decimal Cantidad { get; set; }
}

/// <summary>Telas (materiales) de un depósito, segregadas por tipo (material) + N° de pedido + telera.
/// En la UI cada material es una barra apilada y cada segmento es un pedido.</summary>
public sealed class DepoMaterialDto
{
    public int IdMaterial { get; set; }
    public string Material { get; set; } = "";
    public string Pedido { get; set; } = "";
    public string? Telera { get; set; }
    public string? Unidad { get; set; }
    public int CantRollos { get; set; }
    public decimal Cantidad { get; set; }
}

/// <summary>Stock total por depósito (barra superior del tablero).</summary>
public sealed class DepoStockDto
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public string? Deposito { get; set; }    // nombre (puede ser null/vacío)
    public int CantRollos { get; set; }
    public decimal Cantidad { get; set; }
}

/// <summary>Stock por color (nuestros colores) de una tela en un depósito, separado por N° de pedido.
/// En la UI cada color es una barra apilada y cada segmento es un pedido.</summary>
public sealed class ColorStockDto
{
    public string? ColorCod { get; set; }      // código de nuestro color (null = sin color cargado)
    public string Color { get; set; } = "";    // descripción ("(sin color)" si no tiene)
    public string Pedido { get; set; } = "";
    public int CantRollos { get; set; }
    public decimal Cantidad { get; set; }
}

/// <summary>Un rollo físico (fila de stock) para la grilla/ABM.</summary>
public sealed class TelaRolloDto
{
    public int Id { get; set; }
    public int IdMaterial { get; set; }
    public string Material { get; set; } = "";
    public int? IdColor { get; set; }
    public string? CodColor { get; set; }
    public string? Color { get; set; }
    public string? ColorTelera { get; set; }
    public int IdDeposito { get; set; }
    public string? CodDeposito { get; set; }
    public string Deposito { get; set; } = "";
    public int? IdTelera { get; set; }
    public string? CodTelera { get; set; }
    public string? Telera { get; set; }
    public string? NumPedido { get; set; }
    public string? NumRemito { get; set; }
    public decimal? Cantidad { get; set; }
    public string? Unidad { get; set; }
}

/// <summary>Alta/modificación de un rollo (ABM de stock).</summary>
public sealed class RolloSaveRequest
{
    [Required(ErrorMessage = "Debe elegir el material.")]
    [Range(1, int.MaxValue, ErrorMessage = "Debe elegir el material.")]
    public int IdMaterial { get; set; }

    [Required(ErrorMessage = "Debe elegir el depósito.")]
    [Range(1, int.MaxValue, ErrorMessage = "Debe elegir el depósito.")]
    public int IdDeposito { get; set; }

    public int? IdColor { get; set; }
    public int? IdTelera { get; set; }

    [MaxLength(150)] public string? ColorTelera { get; set; }
    [MaxLength(50)] public string? NumPedido { get; set; }
    [MaxLength(50)] public string? NumRemito { get; set; }
    public decimal? Cantidad { get; set; }
    [MaxLength(10)] public string? Unidad { get; set; }
}

/// <summary>Alta de un ítem de catálogo (material: solo nombre; depósito/telera: código + nombre).</summary>
public sealed class CatalogoSaveRequest
{
    [MaxLength(20)] public string? Codigo { get; set; }
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(150)] public string Nombre { get; set; } = "";
}

/// <summary>Tipos de catálogo editable de Telas.</summary>
public static class CatalogoTela
{
    public const string Materiales = "materiales";
    public const string Colores = "colores";
    public const string Depositos = "depositos";
    public const string Teleras = "teleras";
}
