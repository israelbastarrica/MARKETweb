using System.Net.Http.Json;
using MarketWeb.Shared.UsuariosPc;

namespace MarketWeb.Client.Services;

/// <summary>Cliente para el onboarding (elegir/reclamar PC en el primer login).</summary>
public sealed class AuthApi
{
    private readonly HttpClient _http;

    public AuthApi(HttpClient http) => _http = http;

    public async Task<List<UsuarioPcDto>> PcsDisponiblesAsync()
        => await _http.GetFromJsonAsync<List<UsuarioPcDto>>("api/auth/pcs-disponibles") ?? new();

    public async Task<(bool Ok, string? Error)> ReclamarPcAsync(int pcId)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/reclamar-pc", new { pcId });
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
            return (false, err?.Mensaje ?? "No se pudo completar la operación.");
        }
        catch
        {
            return (false, "No se pudo completar la operación.");
        }
    }

    private sealed class ErrorResponse
    {
        public string? Mensaje { get; set; }
    }
}
