using System.Security.Claims;
using MarketWeb.Application.Common;
using MarketWeb.Application.UsuariosPc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Inicia el login con Google (navegación de página completa).</summary>
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = "/")
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl
        };
        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    /// <summary>Usuario actual. 401 si no está logueado. estado/perfil/pc vienen de los claims (ClaimsTransformation).</summary>
    [HttpGet("me")]
    public IActionResult Me()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        return Ok(new
        {
            email = User.FindFirst(ClaimTypes.Email)?.Value ?? "",
            nombre = User.Identity?.Name ?? "",
            picture = User.FindFirst("picture")?.Value,
            perfil = User.FindFirst("perfil")?.Value,
            pc = User.FindFirst("pc")?.Value,
            estado = User.FindFirst("estado")?.Value ?? "onboarding"
        });
    }

    /// <summary>PCs disponibles (sin mail) para que el usuario elija la suya en el onboarding.</summary>
    [Authorize]
    [HttpGet("pcs-disponibles")]
    public async Task<IActionResult> PcsDisponibles([FromServices] IUsuariosPcService usuarios, CancellationToken ct)
        => Ok(await usuarios.ListarDisponiblesAsync(ct));

    /// <summary>El usuario reclama una PC. Queda pendiente de aprobación.</summary>
    [Authorize]
    [HttpPost("reclamar-pc")]
    public async Task<IActionResult> ReclamarPc(
        [FromServices] IUsuariosPcService usuarios, [FromBody] ReclamarPcRequest req, CancellationToken ct)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized();
        try
        {
            await usuarios.ReclamarPcAsync(req.PcId, email, ct);
            return Ok();
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    public sealed record ReclamarPcRequest(int PcId);

    /// <summary>Perfiles existentes (áreas/locales) para que un usuario nuevo elija el suyo.</summary>
    [Authorize]
    [HttpGet("perfiles")]
    public async Task<IActionResult> Perfiles([FromServices] IUsuariosPcService usuarios, CancellationToken ct)
        => Ok(await usuarios.ListarPerfilesAsync(ct));

    /// <summary>Usuario sin PC: pide acceso eligiendo su perfil. Queda pendiente de aprobación.</summary>
    [Authorize]
    [HttpPost("solicitar-acceso")]
    public async Task<IActionResult> SolicitarAcceso(
        [FromServices] IUsuariosPcService usuarios, [FromBody] SolicitarAccesoRequest req, CancellationToken ct)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email)) return Unauthorized();
        try
        {
            await usuarios.SolicitarAccesoAsync(email, req.Perfil, ct);
            return Ok();
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    public sealed record SolicitarAccesoRequest(string Perfil);
}
