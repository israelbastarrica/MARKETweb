namespace MarketWeb.Shared.Sistemas;

/// <summary>Estado del servidor monitoreado (ping) + datos para Wake-on-LAN.</summary>
public sealed class ServidorEstadoDto
{
    public string Nombre { get; set; } = "";
    public string Host { get; set; } = "";     // IP/hostname para el ping
    public string Mac { get; set; } = "";       // para el WOL
    public bool Configurado { get; set; }       // hay Host para chequear
    public bool Online { get; set; }            // respondió el ping
    public long? Ms { get; set; }               // latencia del ping
}

/// <summary>Resultado de enviar el magic packet (WOL).</summary>
public sealed class WolResultadoDto
{
    public bool Ok { get; set; }
    public string Mensaje { get; set; } = "";
}
