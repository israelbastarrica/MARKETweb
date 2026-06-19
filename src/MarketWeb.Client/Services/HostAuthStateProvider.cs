using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace MarketWeb.Client.Services;

/// <summary>
/// Estado de autenticación de la SPA. Pregunta a la API (/api/auth/me) quién está
/// logueado. La identidad real vive en la cookie del servidor (login Google).
/// </summary>
public sealed class HostAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;

    public HostAuthStateProvider(HttpClient http) => _http = http;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var me = await _http.GetFromJsonAsync<MeDto>("api/auth/me");
            if (me is null || string.IsNullOrEmpty(me.Email))
                return Anonimo();

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, string.IsNullOrEmpty(me.Nombre) ? me.Email : me.Nombre),
                new(ClaimTypes.Email, me.Email),
                new("estado", me.Estado ?? "onboarding"),
            };
            if (!string.IsNullOrEmpty(me.Perfil)) claims.Add(new Claim("perfil", me.Perfil));
            if (!string.IsNullOrEmpty(me.Pc)) claims.Add(new Claim("pc", me.Pc));
            if (!string.IsNullOrEmpty(me.Picture)) claims.Add(new Claim("picture", me.Picture));

            var identity = new ClaimsIdentity(claims, authenticationType: "google");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            // 401 o error de red → anónimo.
            return Anonimo();
        }
    }

    private static AuthenticationState Anonimo()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));

    /// <summary>Re-consulta /me y avisa a la UI (tras reclamar PC o ser aprobado).</summary>
    public void Refrescar() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public sealed record MeDto(string Email, string? Nombre, string? Picture, string? Perfil, string? Pc, string? Estado);
}
