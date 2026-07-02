using MarketWeb.Application.Marketing;
using MarketWeb.Shared.Marketing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/marketing")]
public sealed class MarketingController : ControllerBase
{
    private readonly IMarketingService _service;
    public MarketingController(IMarketingService service) => _service = service;

    [HttpGet("thumb/{red}/{postId}")]
    public async Task<IActionResult> Thumb(string red, string postId, CancellationToken ct)
    {
        var url = await _service.ThumbUrlAsync(red, postId, ct);
        return string.IsNullOrEmpty(url) ? NotFound() : Redirect(url);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<MktDashboardDto>> Dashboard(CancellationToken ct)
        => Ok(await _service.DashboardAsync(ct));

    [HttpGet("perfiles")]
    public async Task<ActionResult<IReadOnlyList<MktPerfilDto>>> Perfiles(CancellationToken ct)
        => Ok(await _service.PerfilesAsync(ct));

    [HttpGet("publicaciones")]
    public async Task<ActionResult<IReadOnlyList<MktPublicacionDto>>> Publicaciones(
        [FromQuery] string? red = null, [FromQuery] int top = 100, CancellationToken ct = default)
        => Ok(await _service.PublicacionesAsync(red, top, ct));

    private string Usuario => User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
    private string Aud => $"Marketing web | {Usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}";

    [HttpGet("calendario")]
    public async Task<ActionResult<CalMesDto>> Calendario([FromQuery] int anio, [FromQuery] int mes, CancellationToken ct)
        => Ok(await _service.CalendarioMesAsync(anio, mes, ct));

    [HttpPost("calendario/accion")]
    public async Task<ActionResult<int>> GuardarAccion([FromBody] CalAccionSaveRequest req, CancellationToken ct)
        => Ok(await _service.GuardarAccionAsync(req, Aud, ct));

    [HttpDelete("calendario/accion/{id:int}")]
    public async Task<ActionResult<bool>> EliminarAccion(int id, CancellationToken ct)
        => Ok(await _service.EliminarAccionAsync(id, Aud, ct));
}
