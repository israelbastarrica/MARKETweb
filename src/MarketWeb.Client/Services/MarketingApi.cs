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

    public async Task<CalMesDto> CalendarioMesAsync(int anio, int mes)
        => await _http.GetFromJsonAsync<CalMesDto>($"api/marketing/calendario?anio={anio}&mes={mes}") ?? new();

    public async Task<int> GuardarAccionAsync(CalAccionSaveRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/marketing/calendario/accion", req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<int>() : 0;
    }

    public async Task<bool> EliminarAccionAsync(int id)
        => (await _http.DeleteAsync($"api/marketing/calendario/accion/{id}")).IsSuccessStatusCode;
}
