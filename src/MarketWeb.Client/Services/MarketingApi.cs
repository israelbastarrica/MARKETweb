using System.Net.Http.Json;
using MarketWeb.Shared.Marketing;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del módulo Marketing / Redes sociales (lectura de MKT_Redes*).</summary>
public sealed class MarketingApi
{
    private readonly HttpClient _http;
    public MarketingApi(HttpClient http) => _http = http;

    public async Task<MktDashboardDto> DashboardAsync()
        => await _http.GetFromJsonAsync<MktDashboardDto>("api/marketing/dashboard") ?? new();

    public async Task<List<MktPerfilDto>> PerfilesAsync()
        => await _http.GetFromJsonAsync<List<MktPerfilDto>>("api/marketing/perfiles") ?? new();

    public async Task<List<MktPublicacionDto>> PublicacionesAsync(string? red = null, int top = 100)
    {
        var qs = $"?top={top}" + (string.IsNullOrWhiteSpace(red) || red == "TODOS" ? "" : $"&red={Uri.EscapeDataString(red)}");
        return await _http.GetFromJsonAsync<List<MktPublicacionDto>>("api/marketing/publicaciones" + qs) ?? new();
    }
}
