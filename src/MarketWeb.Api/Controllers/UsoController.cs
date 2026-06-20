using System.Security.Claims;
using MarketWeb.Application.Uso;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/uso")]
public sealed class UsoController : ControllerBase
{
    private readonly IUsoService _service;
    public UsoController(IUsoService service) => _service = service;

    /// <summary>Registra una visita (ruta) del usuario actual.</summary>
    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] RegistrarUsoRequest req, CancellationToken ct)
    {
        var mail = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(mail) || string.IsNullOrWhiteSpace(req.Ruta)) return Ok();
        await _service.RegistrarAsync(mail, req.Ruta, ct);
        return Ok();
    }

    /// <summary>Rutas más usadas por el usuario actual.</summary>
    [HttpGet("top")]
    public async Task<IActionResult> Top([FromQuery] int n, CancellationToken ct)
    {
        var mail = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(mail)) return Ok(Array.Empty<string>());
        return Ok(await _service.TopAsync(mail, n <= 0 ? 6 : n, ct));
    }

    public sealed record RegistrarUsoRequest(string Ruta);
}
