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
    // tiene acceso al dashboard. verComo: SOLO ADMIN puede pedir ver el dashboard como
    // un local puntual (LURO/PERALTA); para cualquier otro perfil se ignora (seguridad).
    private (string rol, string? local)? Resolver(string? verComo = null)
    {
        var perfil = (User.FindFirst("perfil")?.Value ?? "").Trim().ToUpperInvariant();
        if (perfil == "ADMIN")
        {
            var vc = (verComo ?? "").Trim().ToUpperInvariant();
            if (LocalesValidos.Contains(vc)) return ("cajero", vc);   // ADMIN viendo como ese local
            return ("admin", null);
        }
        if (LocalesValidos.Contains(perfil)) return ("cajero", perfil);
        return null;
    }

    [HttpGet("ventas")]
    public async Task<IActionResult> Ventas([FromQuery] string? fecha, [FromQuery] string? verComo, CancellationToken ct)
    {
        var acc = Resolver(verComo);
        if (acc is null) return Forbid();
        var f = string.IsNullOrWhiteSpace(fecha) ? DateTime.Today.ToString("yyyyMMdd") : fecha;
        return Ok(await _service.GetVentasAsync(f, acc.Value.rol, acc.Value.local, ct));
    }

    [HttpGet("resumen-mobile")]
    public async Task<IActionResult> ResumenMobile([FromQuery] string? fecha, [FromQuery] string? verComo, CancellationToken ct)
    {
        var acc = Resolver(verComo);
        if (acc is null) return Forbid();
        var f = string.IsNullOrWhiteSpace(fecha) ? DateTime.Today.ToString("yyyyMMdd") : fecha;
        return Ok(await _service.GetResumenMobileAsync(f, acc.Value.rol, acc.Value.local, ct));
    }

    [HttpGet("fichadas")]
    public async Task<IActionResult> Fichadas([FromQuery] string? fecha, [FromQuery] string? verComo, CancellationToken ct)
    {
        var acc = Resolver(verComo);
        if (acc is null) return Forbid();
        var f = string.IsNullOrWhiteSpace(fecha) ? DateTime.Today.ToString("yyyyMMdd") : fecha;
        return Ok(await _service.GetFichadasAsync(f, acc.Value.rol, acc.Value.local, ct));
    }
}
