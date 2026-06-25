using MarketWeb.Application.Dragonfish;
using MarketWeb.Application.Remitos;
using MarketWeb.Shared.Dragonfish;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

// Alta de remitos CENTRAL→local desde la tablet de Logística (vía API Dragonfish).
[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/remitos")]
public sealed class RemitosController : ControllerBase
{
    private readonly IDragonfishService _dragon;
    private readonly IRemitosLookupService _lookup;

    public RemitosController(IDragonfishService dragon, IRemitosLookupService lookup)
    {
        _dragon = dragon;
        _lookup = lookup;
    }

    [HttpGet("estado")]
    public IActionResult Estado() => Ok(new { configurado = _dragon.Configurado });

    [HttpGet("ultima-repo")]
    public async Task<IActionResult> UltimaRepo([FromQuery] string local, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(local)) return BadRequest("local requerido");
        return Ok(await _lookup.UltimaRepoAsync(local, ct));
    }

    [HttpGet("articulo/{cod}")]
    public async Task<IActionResult> BuscarArticulo(string cod, CancellationToken ct)
    {
        var res = await _lookup.BuscarArticuloAsync(cod, ct);
        if (res is null) return NotFound();
        return Ok(res);
    }

    [HttpGet("bolsa/{nro}")]
    public async Task<IActionResult> BuscarBolsa(string nro, CancellationToken ct)
    {
        var res = await _lookup.BuscarBolsaAsync(nro, ct);
        if (res is null) return NotFound();
        return Ok(res);
    }

    [HttpGet("remito-local")]
    public async Task<IActionResult> RemitoLocal([FromQuery] string local, [FromQuery] int punto, [FromQuery] int numero, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(local)) return BadRequest("local requerido");
        return Ok(await _lookup.BuscarRemitoLocalAsync(local, punto, numero, ct));
    }

    [HttpGet("motivos")]
    public async Task<IActionResult> Motivos(CancellationToken ct) => Ok(await _lookup.MotivosAsync(ct));

    [HttpPost]
    public async Task<ActionResult<DragonRemitoResultDto>> Crear([FromBody] DragonRemitoRequest req, CancellationToken ct)
    {
        var res = await _dragon.CrearRemitoAsync(req, ct);
        if (res.Ok)
        {
            // Dragon NO persiste InformacionAdicional/ZADSFW: guardamos el mapeo tablet→remito en tabla
            // propia para que el agente de impresión rutee la impresora por N° de remito. Best-effort:
            // el remito ya existe en Dragon, no rompemos el alta si esto falla.
            try
            {
                var pc = Request.Headers["X-Pc"].FirstOrDefault();
                var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
                await _lookup.RegistrarRemitoTabletAsync(
                    res.Numero, res.Codigo, req.Local ?? "", req.InformacionAdicional, pc, req.Motivo, usuario, ct);
            }
            catch { /* mapeo best-effort */ }
        }
        return Ok(res);
    }
}
