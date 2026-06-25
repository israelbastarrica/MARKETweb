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

    /// <summary>
    /// Diagnóstico: crea UN remito de prueba mandando varios nombres de campo "observaciones" a la vez,
    /// cada uno con un valor marcado (DIAG-...). Después se busca en COMPROBANTEV cuál persistió.
    /// OJO: genera un remito REAL (movimiento de stock) — usar local de prueba y anular si hace falta.
    /// </summary>
    [HttpPost("remito-diagnostico")]
    public async Task<ActionResult<DragonRemitoResultDto>> RemitoDiagnostico([FromBody] DragonRemitoRequest req, CancellationToken ct)
    {
        var extras = new Dictionary<string, object?>
        {
            ["Observacion"]          = "DIAG-OBSERVACION",
            ["Observaciones"]        = "DIAG-OBSERVACIONES",
            ["ObservacionComercial"] = "DIAG-OBSCOMERCIAL",
            ["ObservacionContable"]  = "DIAG-OBSCONTABLE",
            ["Leyenda"]              = "DIAG-LEYENDA",
            ["Nota"]                 = "DIAG-NOTA",
            ["Comentario"]           = "DIAG-COMENTARIO",
            ["FOBS"]                 = "DIAG-FOBS",          // nombre de columna cruda, por si la API lo acepta
            ["Obs"]                  = "DIAG-OBS",
        };
        return Ok(await _dragon.CrearRemitoConExtrasAsync(req, extras, ct));
    }
}
