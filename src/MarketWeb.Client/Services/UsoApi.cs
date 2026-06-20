using System.Net.Http.Json;
using MarketWeb.Shared.Uso;

namespace MarketWeb.Client.Services;

/// <summary>Cliente de la estadística de uso (pantallas + registros del usuario).</summary>
public sealed class UsoApi
{
    private readonly HttpClient _http;
    public UsoApi(HttpClient http) => _http = http;

    public async Task RegistrarAsync(string ruta)
    {
        try { await _http.PostAsJsonAsync("api/uso", new { ruta }); } catch { /* best-effort */ }
    }

    public async Task<List<string>> TopAsync(int n = 6)
    {
        try { return await _http.GetFromJsonAsync<List<string>>($"api/uso/top?n={n}") ?? new(); }
        catch { return new(); }
    }

    public async Task RegistrarRegistroAsync(string ruta, string titulo)
    {
        try { await _http.PostAsJsonAsync("api/uso/registro", new { ruta, titulo }); } catch { /* best-effort */ }
    }

    public async Task<List<RegistroRecienteDto>> RecientesAsync(int n = 5)
    {
        try { return await _http.GetFromJsonAsync<List<RegistroRecienteDto>>($"api/uso/recientes?n={n}") ?? new(); }
        catch { return new(); }
    }
}
