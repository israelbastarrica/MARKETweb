using MarketWeb.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

// Dashboard de ventas (port de NoblexTV). ADMIN ve gerencial (LURO+PERALTA);
// un local (perfil LURO/PERALTA) se ve a sí mismo. El rol se deriva del perfil,
// nunca del cliente: así un cajero no puede pedir el agregado de otro local.
[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service) => _service = service;

    private static readonly string[] LocalesValidos = { "LURO", "PERALTA" };

    // Devuelve (rol, local) según el perfil del usuario, o null si el perfil no
    // tiene acceso al dashboard.
    private (string rol, string? local)? Resolver()
    {
        var perfil = (User.FindFirst("perfil")?.Value ?? "").Trim().ToUpperInvariant();
        if (perfil == "ADMIN") return ("admin", null);
        if (LocalesValidos.Contains(perfil)) return ("cajero", perfil);
        return null;
    }

    [HttpGet("ventas")]
    public async Task<IActionResult> Ventas([FromQuery] string? fecha, CancellationToken ct)
    {
        var acc = Resolver();
        if (acc is null) return Forbid();
        var f = string.IsNullOrWhiteSpace(fecha) ? DateTime.Today.ToString("yyyyMMdd") : fecha;
        return Ok(await _service.GetVentasAsync(f, acc.Value.rol, acc.Value.local, ct));
    }

    [HttpGet("fichadas")]
    public async Task<IActionResult> Fichadas([FromQuery] string? fecha, CancellationToken ct)
    {
        var acc = Resolver();
        if (acc is null) return Forbid();
        var f = string.IsNullOrWhiteSpace(fecha) ? DateTime.Today.ToString("yyyyMMdd") : fecha;
        return Ok(await _service.GetFichadasAsync(f, acc.Value.rol, acc.Value.local, ct));
    }
}
