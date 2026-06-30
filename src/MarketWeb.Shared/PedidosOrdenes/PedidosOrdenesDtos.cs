using System.ComponentModel.DataAnnotations;

namespace MarketWeb.Shared.PedidosOrdenes;

/// <summary>Fila del listado/reporte de Órdenes de Pedido (tabla PedidosOrdenes + cruces a Dragonfish).</summary>
public sealed class PedidoOrdenListaDto
{
    public int Id { get; set; }
    public int NroOrden { get; set; }
    public string? Tipo { get; set; }
    public string? CodArt { get; set; }
    public string? Descripcion { get; set; }     // ARTDES de Dragon, o DescripcionALT, o "NO EXISTE EN DRAGON"
    public string Ficha { get; set; } = "NO";     // "SI"/"NO" (tiene ficha técnica cargada)
    public string? Estado { get; set; }
    public string Finalizada { get; set; } = "NO"; // "SI"/"NO"
    public string? EquiTalle { get; set; }
    public bool ExisteEnDragon => Descripcion != "NO EXISTE EN DRAGON";
}

/// <summary>Filtros del listado. Campos vacíos/null = sin filtrar (equivale a "TODOS").</summary>
public sealed class PedidoOrdenFiltro
{
    public int? NroOrden { get; set; }
    public string? CodArt { get; set; }
    public string? Tipo { get; set; }
    public string? Estado { get; set; }
    public string? Ficha { get; set; }        // "SI"/"NO"
    public string? ArtDragon { get; set; }    // "SI"/"NO" (existe en Dragon)
    public string? CodProveedor { get; set; } // 3 dígitos
    public string? Finalizada { get; set; }   // "SI"/"NO"
}

/// <summary>Datos de una orden para edición (sin la ficha binaria; sólo si la tiene).</summary>
public sealed class PedidoOrdenDto
{
    public int Id { get; set; }
    public int NroOrden { get; set; }
    public string? ArtCod { get; set; }
    public string? Tipo { get; set; }
    public string? DescripcionALT { get; set; }
    public int? IdEquiTalle { get; set; }
    public string? AsanaTaskID { get; set; }
    public bool TieneFicha { get; set; }
}

/// <summary>Alta/modificación de una orden de pedido.</summary>
public sealed class PedidoOrdenSaveRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Ingresá el N° de orden.")]
    public int NroOrden { get; set; }

    [Required(ErrorMessage = "Ingresá el código de artículo.")]
    [MaxLength(50)] public string ArtCod { get; set; } = "";

    [MaxLength(50)] public string? Tipo { get; set; }
    [MaxLength(500)] public string? DescripcionALT { get; set; }
    public int? IdEquiTalle { get; set; }
}

/// <summary>Resolución de un ARTCOD contra Dragonfish (descripción + proveedor).</summary>
public sealed class ArticuloDragonDto
{
    public string ArtCod { get; set; } = "";
    public bool ExisteEnDragon { get; set; }
    public string? Descripcion { get; set; }
    public string? CodProveedor { get; set; }
    public string? Proveedor { get; set; }
}

public sealed class EquivalenciaTalleDto
{
    public int Id { get; set; }
    public string Descripcion { get; set; } = "";
}

public sealed class ProveedorOrdenDto
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
}

/// <summary>Tipos (proyectos) de orden de pedido.</summary>
public static class TipoOrden
{
    public const string Nacional = "NACIONAL";
    public static readonly string[] Todos =
    {
        "NACIONAL", "IMPORTADO", "IMPORTADO CHINA",
        "IMPORTADO ACCESORIOS", "IMPORTADO LENCERIA", "IMPORTADO BLANQUERIA"
    };
}
