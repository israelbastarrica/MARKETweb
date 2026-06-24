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

    [HttpPost]
    public async Task<ActionResult<DragonRemitoResultDto>> Crear([FromBody] DragonRemitoRequest req, CancellationToken ct)
        => Ok(await _dragon.CrearRemitoAsync(req, ct));
}
