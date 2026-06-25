using System.Net.Http.Json;
using MarketWeb.Shared.Informes;

namespace MarketWeb.Client.Services;

public sealed class InformesApi
{
    private readonly HttpClient _http;
    public InformesApi(HttpClient http) => _http = http;

    public async Task<InformeVentaCombosDto> CombosVentasAsync()
    {
        try { return await _http.GetFromJsonAsync<InformeVentaCombosDto>("api/informes/ventas/combos") ?? new(); }
        catch { return new(); }
    }

    public async Task<List<InformeVentaFila>> VentasAsync(InformeVentaFiltro f)
    {
        var resp = await _http.PostAsJsonAsync("api/informes/ventas", f);
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<List<InformeVentaFila>>() ?? new();
        return new();
    }

    public async Task<List<InformeSerieFila>> VentasSerieAsync(InformeVentaFiltro f, string dimension)
    {
        var resp = await _http.PostAsJsonAsync($"api/informes/ventas/serie?dimension={Uri.EscapeDataString(dimension)}", f);
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<List<InformeSerieFila>>() ?? new();
        return new();
    }
}
