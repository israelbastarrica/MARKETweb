namespace MarketWeb.Shared.Reposicion;

/// <summary>Fila del estado de control (SP_RemitosControlEstado): totales por fecha + local destino.</summary>
public sealed class ControlEstadoDto
{
    public DateTime FechaRemito { get; set; }
    public string LocalDestino { get; set; } = "";
    public int Generados { get; set; }
    public int Despachados { get; set; }
    public int Recibidos { get; set; }
    public int NoRecibidos { get; set; }
    public int Aceptados { get; set; }
    public int Rechazados { get; set; }
}

/// <summary>Un remito en el detalle de control (SP_RemitosControlListado), con estados ya formateados.</summary>
public sealed class RemitoControlDto
{
    public string NroRemito { get; set; } = "";
    public DateTime? FechaRemito { get; set; }
    public string Origen { get; set; } = "";
    public string Destino { get; set; } = "";
    public string EstadoDespacho { get; set; } = "";    // ya incluye "CRUZADO → X" si corresponde
    public DateTime? FechaDespacho { get; set; }
    public string UsuarioDespacho { get; set; } = "";
    public string Estado { get; set; } = "";            // recepción, ya con "RECIBIDO POR QR PC" si corresponde
    public DateTime? FechaEscaneo { get; set; }
    public string UsuarioApp { get; set; } = "";
    public string RemitoId { get; set; } = "";
    public int DespachoId { get; set; }
    public int IdLocalDestino { get; set; }             // destino esperado (para la foto del QR de pantalla)
    public int IdLocalDestinoDespacho { get; set; }
    public bool EsQrPantalla { get; set; }              // recibido escaneando un QR regenerado en pantalla (-PC)
    public bool Rechazado { get; set; }                 // rechazo confirmado en RemitoRecepcion (Estado='RECHAZADO_OK')
    public string ColorHint { get; set; } = "normal";   // cruzado / rechazado / qrpc / aceptado / recibido / norecibido / normal
}

/// <summary>Una fila del log de QR regenerados en pantalla (RemitoQRGenerado_Log).</summary>
public sealed class QrLogDto
{
    public DateTime? Fecha { get; set; }
    public string MachineName { get; set; } = "";
    public string LocalUsuario { get; set; } = "";
    public string NroRemito { get; set; } = "";
    public string Origen { get; set; } = "";
    public string Destino { get; set; } = "";
    public string RemitoCodigo { get; set; } = "";
}

/// <summary>Motivo + indicador de foto de un QR de pantalla (RemitosEscaneados).</summary>
public sealed class QrFotoInfoDto
{
    public bool Existe { get; set; }
    public string Motivo { get; set; } = "";
    public bool TieneFoto { get; set; }
}
