using MarketWeb.Application.Dragonfish;
using MarketWeb.Shared.Dragonfish;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Admin")]
[ApiController]
[Route("api/dragon")]
public sealed class DragonController : ControllerBase
{
    private readonly IDragonfishService _dragon;
    public DragonController(IDragonfishService dragon) => _dragon = dragon;

    [HttpGet("estado")]
    public IActionResult Estado() => Ok(new { configurado = _dragon.Configurado });

    /// <summary>Prueba: crea un remito de insumos en Dragon y devuelve la respuesta cruda (para validar auth/esquema).</summary>
    [HttpPost("remito-prueba")]
    public async Task<ActionResult<DragonRemitoResultDto>> RemitoPrueba([FromBody] DragonRemitoRequest req, CancellationToken ct)
        => Ok(await _dragon.CrearRemitoAsync(req, ct));
}
