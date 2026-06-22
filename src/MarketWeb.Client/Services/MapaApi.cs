using System.Net.Http.Json;
using MarketWeb.Shared.Mapa;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del reporte de Mapa. Habla con MapaController (api/mapa).</summary>
public sealed class MapaApi
{
    private readonly HttpClient _http;
    public MapaApi(HttpClient http) => _http = http;

    public async Task<MapaCombosDto> CombosAsync()
        => await _http.GetFromJsonAsync<MapaCombosDto>("api/mapa/combos") ?? new();

    public async Task<MapaReporteResultado> ReporteAsync(MapaReporteFiltro filtro)
    {
        var resp = await _http.PostAsJsonAsync("api/mapa/reporte", filtro);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<MapaReporteResultado>() ?? new();
    }
}
