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

    /// <summary>Todas las PCs físicas, para el selector "Esta PC" por dispositivo.</summary>
    public async Task<List<UsuarioPcDto>> PcsTodasAsync()
        => await _http.GetFromJsonAsync<List<UsuarioPcDto>>("api/auth/pcs") ?? new();

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

    public async Task<List<string>> PerfilesAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/auth/perfiles") ?? new();

    public async Task<(bool Ok, string? Error)> SolicitarAccesoAsync(string perfil)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/solicitar-acceso", new { perfil });
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
