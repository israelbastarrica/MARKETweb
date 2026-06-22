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
    public int IdLocalDestinoDespacho { get; set; }
    public string ColorHint { get; set; } = "normal";   // cruzado / qrpc / aceptado / recibido / norecibido / normal
}
