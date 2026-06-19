using MarketWeb.Application.Despachos;
using MarketWeb.Shared.Despachos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace MarketWeb.Api.Controllers;

// Despachos hacia el local. El alcance se deriva del perfil: un local (LURO/PERALTA)
// ve SOLO lo suyo; ADMIN puede ver todos o filtrar por local.
[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class DespachosController : ControllerBase
{
    private readonly IDespachosService _service;
    public DespachosController(IDespachosService service) => _service = service;

    [HttpGet("locales")]
    public async Task<ActionResult<IReadOnlyList<DespachoLocalDto>>> Locales(CancellationToken ct)
        => Ok(await _service.ListarLocalesAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DespachoDto>>> Listar(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta, [FromQuery] int? local, CancellationToken ct)
    {
        var perfil = (User.FindFirst("perfil")?.Value ?? "").Trim();
        // Alcance forzado por perfil: local → su local; LOGISTICA → DEPÓSITO; ADMIN → lo pedido (o todos).
        var scopeName = perfil.Equals("LOGISTICA", StringComparison.OrdinalIgnoreCase) ? "DEPÓSITO" : perfil;
        var idPropio = await _service.ResolverIdLocalAsync(scopeName, ct);
        int? idLocal = idPropio > 0 ? idPropio : local;

        return Ok(await _service.ListarAsync(desde, hasta, idLocal, ct));
    }

    [HttpGet("articulos")]
    public async Task<ActionResult<IReadOnlyList<DespachoArticuloDto>>> Articulos(
        [FromQuery] string remito, [FromQuery] string origen, CancellationToken ct)
        => Ok(await _service.ListarArticulosAsync(remito, origen, ct));

    // Regenera el QR de pantalla de un remito (etiqueta rota). Valida + loguea + devuelve el PNG.
    [HttpPost("qr")]
    public async Task<ActionResult<QrRemitoDto>> Qr([FromBody] QrRequest req, CancellationToken ct)
    {
        var perfil = (User.FindFirst("perfil")?.Value ?? "").Trim();
        var pc = (User.FindFirst("pc")?.Value ?? "WEB").Trim();
        var idLocal = await _service.ResolverIdLocalAsync(perfil, ct);

        // El sufijo "-PC" solo aplica a usuarios de un local (LURO/PERALTA).
        var esPc = req.EsPc && idLocal > 0;
        var res = await _service.PrepararQrAsync(req.Remito, esPc, idLocal, perfil, pc, ct);
        if (res.Ok)
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(res.CodigoQr, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data).GetGraphic(10);
            res.QrPngBase64 = Convert.ToBase64String(png);
        }
        return Ok(res);
    }
}
