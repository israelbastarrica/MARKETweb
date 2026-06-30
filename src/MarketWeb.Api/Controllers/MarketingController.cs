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

    [HttpGet("perfiles")]
    public async Task<ActionResult<IReadOnlyList<MktPerfilDto>>> Perfiles(CancellationToken ct)
        => Ok(await _service.PerfilesAsync(ct));

    [HttpGet("publicaciones")]
    public async Task<ActionResult<IReadOnlyList<MktPublicacionDto>>> Publicaciones(
        [FromQuery] string? red = null, [FromQuery] int top = 100, CancellationToken ct = default)
        => Ok(await _service.PublicacionesAsync(red, top, ct));
}
