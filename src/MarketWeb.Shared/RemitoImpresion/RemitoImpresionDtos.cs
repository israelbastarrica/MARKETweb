namespace MarketWeb.Shared.RemitoImpresion;

/// <summary>Un remito en la cola de impresión (ImpresorRemito_Cola).</summary>
public sealed class RemitoColaDto
{
    public int Id { get; set; }
    public string RemitoCodigo { get; set; } = "";
    public string LocalOrigen { get; set; } = "";
    public string LocalDestino { get; set; } = "";
    public int Punto { get; set; }       // FPTOVEN
    public int NroComp { get; set; }      // FNUMCOMP
    public DateTime FechaEmision { get; set; }
    public string Estado { get; set; } = "";   // PENDIENTE | IMPRESO | ERROR
    public int Intentos { get; set; }
    public string? ErrorMsg { get; set; }
    public DateTime FechaDetectado { get; set; }
    public DateTime? FechaImpreso { get; set; }
    public string? IpImpresora { get; set; }
    public int Reimpresiones { get; set; }
    public int? Saltafw { get; set; }     // impresora asignada (filtro "por PC" en logística)

    // Anulación: existe un pedido de rechazo en RemitoRecepcion (Accion='RECHAZAR') para
    // este remito hacia su LocalDestino. Los anulados salen de la cola principal y se ven
    // en la lista "Remitos Anulados".
    public bool Anulado { get; set; }
    public string? EstadoRechazo { get; set; }   // PENDIENTE_API | RECHAZADO_OK | ... (RemitoRecepcion.Estado)
    public DateTime? FechaAnulado { get; set; }   // RemitoRecepcion.FechaRecepcion del pedido de rechazo

    public string NroRemito => $"{Punto:D4}-{NroComp:D8}";
}

/// <summary>Una impresora de la cola (SALTAFW + su IP). Para el selector "esta PC" en logística.</summary>
public sealed class ImpresoraColaDto
{
    public int Saltafw { get; set; }
    public string? Ip { get; set; }
}

/// <summary>Estado liviano de un remito para el polling tras reimprimir.</summary>
public sealed class RemitoEstadoDto
{
    public int Id { get; set; }
    public string Estado { get; set; } = "";
    public int Intentos { get; set; }
    public DateTime? FechaImpreso { get; set; }
    public string? ErrorMsg { get; set; }
}
