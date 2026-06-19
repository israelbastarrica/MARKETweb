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

    public string NroRemito => $"{Punto:D4}-{NroComp:D8}";
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
