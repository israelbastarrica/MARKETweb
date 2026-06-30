using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using MarketWeb.Shared.Sistemas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/servidor")]
public sealed class ServidorController : ControllerBase
{
    private readonly IConfiguration _cfg;
    public ServidorController(IConfiguration cfg) => _cfg = cfg;

    // Defaults en código (el server no tiene appsettings.Production.json). Configurable por Servidor:* / env.
    private const string DefaultHost = "192.168.130.23";  // marketcentral (SQL Server), IP de LAN para el ping
    private const string DefaultMac = "D8-5E-D3-2F-C9-A3";
    private const string DefaultNombre = "marketcentral · SQL Server";

    private string Host => _cfg["Servidor:Host"] ?? DefaultHost;
    private string Mac => _cfg["Servidor:Mac"] ?? DefaultMac;
    private string Nombre => _cfg["Servidor:Nombre"] ?? DefaultNombre;

    [HttpGet("estado")]
    public async Task<ActionResult<ServidorEstadoDto>> Estado(CancellationToken ct)
    {
        var dto = new ServidorEstadoDto
        {
            Nombre = Nombre, Host = Host, Mac = Mac,
            Configurado = !string.IsNullOrWhiteSpace(Host)
        };
        if (dto.Configurado)
        {
            try
            {
                using var ping = new Ping();
                var r = await ping.SendPingAsync(Host, 1500);
                dto.Online = r.Status == IPStatus.Success;
                if (dto.Online) dto.Ms = r.RoundtripTime;
            }
            catch { dto.Online = false; }
        }
        return Ok(dto);
    }

    [HttpPost("wol")]
    [Authorize(Policy = "Admin")]
    public ActionResult<WolResultadoDto> Wol()
    {
        var mac = (Mac ?? "").Trim();
        byte[] macBytes;
        try
        {
            macBytes = mac.Split('-', ':').Select(h => Convert.ToByte(h, 16)).ToArray();
        }
        catch { return Ok(new WolResultadoDto { Ok = false, Mensaje = $"MAC inválida: {mac}" }); }
        if (macBytes.Length != 6) return Ok(new WolResultadoDto { Ok = false, Mensaje = $"MAC inválida: {mac}" });

        // Magic packet: 6 bytes 0xFF + MAC repetida 16 veces.
        var packet = new byte[102];
        for (int i = 0; i < 6; i++) packet[i] = 0xFF;
        for (int i = 1; i <= 16; i++) Array.Copy(macBytes, 0, packet, i * 6, 6);
        try
        {
            using var udp = new UdpClient { EnableBroadcast = true };
            udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));
        }
        catch (Exception ex) { return Ok(new WolResultadoDto { Ok = false, Mensaje = "No se pudo enviar: " + ex.Message }); }
        return Ok(new WolResultadoDto { Ok = true, Mensaje = $"Magic packet enviado a {mac}. Puede tardar ~1 min en levantar." });
    }
}
