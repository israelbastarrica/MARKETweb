namespace MarketWeb.Shared.Insumos;

/// <summary>Cabecera de un pedido de insumos (editor).</summary>
public sealed class PedidoCabeceraDto
{
    public int Id { get; set; }
    public int NroPedido { get; set; }
    public DateTime FechaPedido { get; set; }
    public int IDLocal { get; set; }
    public string LocalNombre { get; set; } = "";
    public string Estado { get; set; } = "";
    public bool Enviado { get; set; }
    /// <summary>Cerrado para el local: DEPÓSITO ya lo imprimió (EN ARMADO) o ya está ENVIADO.</summary>
    public bool Cerrado { get; set; }
}

/// <summary>Un renglón (artículo + cantidad) de un pedido de insumos.</summary>
public sealed class PedidoRenglonDto
{
    public int Id { get; set; }
    public string ArtCod { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int Cantidad { get; set; }
    /// <summary>Depósito: hay stock para enviar (1) o no (0). Default sí.</summary>
    public bool Existencia { get; set; } = true;
    /// <summary>Depósito: cantidad realmente enviada (null = igual a la pedida).</summary>
    public int? CantidadEnviada { get; set; }
    /// <summary>Depósito: el renglón no requiere consumo (no va a Administración).</summary>
    public bool NoRequiereConsumo { get; set; }
}

/// <summary>Pedido completo para el editor (cabecera + renglones).</summary>
public sealed class PedidoEditorDto
{
    public PedidoCabeceraDto Cabecera { get; set; } = new();
    public List<PedidoRenglonDto> Renglones { get; set; } = new();
}

/// <summary>Artículo de insumo (TIPOARTI='IS') para el buscador.</summary>
public sealed class ArticuloInsumoDto
{
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public int Stock { get; set; }
    public string? UnidadMedida { get; set; }
}

/// <summary>Un renglón a guardar. Id&gt;0 = renglón existente (lo usa el depósito para
/// actualizar por ID). Existencia/CantidadEnviada/NoRequiereConsumo solo las usa el depósito.</summary>
public sealed class RenglonInput
{
    public int Id { get; set; }
    public string ArtCod { get; set; } = "";
    public int Cantidad { get; set; }
    public bool Existencia { get; set; } = true;
    public int? CantidadEnviada { get; set; }
    public bool NoRequiereConsumo { get; set; }
}

/// <summary>
/// Guardado COMPLETO de un pedido (cabecera + todos los renglones) en una transacción.
/// Id=0 → alta (crea la cabecera); Id&gt;0 → reemplaza los renglones del pedido existente.
/// Nada se escribe a la base hasta este guardado (el pedido a medio armar no existe para Logística).
/// </summary>
public sealed class GuardarPedidoRequest
{
    public int Id { get; set; }
    public int IdLocal { get; set; }
    public List<RenglonInput> Renglones { get; set; } = new();
}

/// <summary>Resultado de validar si el local puede pedir hoy (regla 1/15 + pase de gracia).</summary>
public sealed class ValidacionFechaDto
{
    /// <summary>OK | AVISO (deja pasar con advertencia) | BLOQUEADO.</summary>
    public string Resultado { get; set; } = "OK";
    public string Titulo { get; set; } = "";
    public string Mensaje { get; set; } = "";
}

/// <summary>Resultado de crear un pedido nuevo.</summary>
public sealed class CrearPedidoResultado
{
    public int Id { get; set; }
    public int NroPedido { get; set; }
}
