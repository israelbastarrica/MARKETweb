using MarketWeb.Application.Dragonfish;
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
    public RemitosController(IDragonfishService dragon) => _dragon = dragon;

    [HttpGet("estado")]
    public IActionResult Estado() => Ok(new { configurado = _dragon.Configurado });

    [HttpPost]
    public async Task<ActionResult<DragonRemitoResultDto>> Crear([FromBody] DragonRemitoRequest req, CancellationToken ct)
        => Ok(await _dragon.CrearRemitoAsync(req, ct));
}
