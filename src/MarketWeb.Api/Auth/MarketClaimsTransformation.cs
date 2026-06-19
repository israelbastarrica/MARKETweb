using System.Security.Claims;
using MarketWeb.Application.UsuariosPc;
using Microsoft.AspNetCore.Authentication;

namespace MarketWeb.Api.Auth;

/// <summary>
/// Enriquece la identidad (mail de Google) con los claims de MARKET:
/// estado (onboarding/pendiente/ok), perfil y pc, resueltos desde UsuariosPC.
/// Corre en cada autenticación; idempotente (no re-agrega si ya están).
/// </summary>
public sealed class MarketClaimsTransformation : IClaimsTransformation
{
    private readonly IUsuariosPcService _usuarios;

    public MarketClaimsTransformation(IUsuariosPcService usuarios) => _usuarios = usuarios;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return principal;

        if (identity.HasClaim(c => c.Type == "estado"))
            return principal; // ya transformado en este request

        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
            return principal;

        var acceso = await _usuarios.ResolverAccesoAsync(email);
        identity.AddClaim(new Claim("estado", acceso.Estado));
        if (!string.IsNullOrEmpty(acceso.Perfil)) identity.AddClaim(new Claim("perfil", acceso.Perfil));
        if (!string.IsNullOrEmpty(acceso.Pc)) identity.AddClaim(new Claim("pc", acceso.Pc));

        return principal;
    }
}
