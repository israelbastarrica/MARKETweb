using MarketWeb.Application.Reposicion;
using MarketWeb.Shared.Reposicion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/reporte-articulos")]
public sealed class ReporteArticulosController : ControllerBase
{
    private readonly IReporteArticulosService _service;
    public ReporteArticulosController(IReporteArticulosService service) => _service = service;

    [HttpGet("combos")]
    public async Task<ActionResult<ReporteArticulosCombosDto>> Combos(CancellationToken ct)
        => Ok(await _service.CombosAsync(ct));

    [HttpPost("listar")]
    public async Task<ActionResult<IReadOnlyList<ArticuloReporteDto>>> Listar([FromBody] ReporteArticulosFiltro filtro, CancellationToken ct)
        => Ok(await _service.ListarAsync(filtro, ct));

    [HttpPost("packs")]
    public async Task<ActionResult<int>> GuardarPacks([FromBody] GuardarPacksRequest req, CancellationToken ct)
    {
        var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
        return Ok(await _service.GuardarPacksAsync(req, usuario, ct));
    }
}
