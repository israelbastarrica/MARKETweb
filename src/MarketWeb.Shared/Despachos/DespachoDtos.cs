namespace MarketWeb.Shared.Despachos;

/// <summary>Un remito en el flujo de despacho hacia un local (espejo de frmRepoControlRemitosLocal).</summary>
public sealed class DespachoDto
{
    public string RemitoId { get; set; } = "";   // CODIGO Dragon del remito
    public string NroRemito { get; set; } = "";   // PTOV-NROCOMP
    public DateTime FechaRemito { get; set; }      // fecha de reposición (ventana 21→21, sin hora)
    public DateTime? FechaEmision { get; set; }     // fecha+hora de emisión del remito (para ver mañana/tarde)
    public string Origen { get; set; } = "";
    public string Destino { get; set; } = "";

    /// <summary>RECIBIDO | EN TRÁNSITO | PENDIENTE DESPACHO (derivado).</summary>
    public string EstadoVisual { get; set; } = "";

    public DateTime? FechaDespacho { get; set; }
    public DateTime? FechaRecepcion { get; set; }
    public string? UsuarioRecepcion { get; set; }
    public string? EstadoDragon { get; set; }      // NO ACEPTADO | ACEPTADO SIN MSTOCK | ACEPTADO COMPLETO
    public bool EsQRDePantalla { get; set; }

    /// <summary>El despacho fue a un local distinto del esperado.</summary>
    public bool Cruzado { get; set; }
    public string? DestinoReal { get; set; }
}

/// <summary>Local destino para el combo (admin).</summary>
public sealed class DespachoLocalDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

/// <summary>Pedido para regenerar el QR de un remito (etiqueta rota).</summary>
public sealed class QrRequest
{
    public string Remito { get; set; } = "";   // RemitoCODIGO
    public bool EsPc { get; set; } = true;       // agrega "-PC" al QR (escaneo por opción del sistema)
}

/// <summary>Resultado de generar el QR de pantalla de un remito.</summary>
public sealed class QrRemitoDto
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string NroRemito { get; set; } = "";
    public string Origen { get; set; } = "";
    public string Destino { get; set; } = "";
    public string CodigoQr { get; set; } = "";   // lo que codifica el QR (con o sin "-PC")
    public string? QrPngBase64 { get; set; }       // imagen PNG del QR (base64)
}

/// <summary>Un renglón del contenido de un remito (artículos).</summary>
public sealed class DespachoArticuloDto
{
    public string ArtCod { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? Color { get; set; }
    public string? Talle { get; set; }
    public decimal Cantidad { get; set; }
}
