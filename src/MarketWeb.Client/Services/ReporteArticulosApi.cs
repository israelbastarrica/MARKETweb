using System.Net.Http.Json;
using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del Reporte de Artículos (Logística: reporte + edición de packs).</summary>
public sealed class ReporteArticulosApi
{
    private readonly HttpClient _http;
    public ReporteArticulosApi(HttpClient http) => _http = http;

    public async Task<ReporteArticulosCombosDto> CombosAsync()
        => await _http.GetFromJsonAsync<ReporteArticulosCombosDto>("api/reporte-articulos/combos") ?? new();

    public async Task<List<ArticuloReporteDto>> ListarAsync(ReporteArticulosFiltro filtro)
    {
        var resp = await _http.PostAsJsonAsync("api/reporte-articulos/listar", filtro);
        if (!resp.IsSuccessStatusCode) return new();
        return await resp.Content.ReadFromJsonAsync<List<ArticuloReporteDto>>() ?? new();
    }

    /// <summary>Edición masiva de packs. Devuelve cantidad de registros afectados.</summary>
    public async Task<int> GuardarPacksAsync(GuardarPacksRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/reporte-articulos/packs", req);
        if (!resp.IsSuccessStatusCode) return -1;
        return await resp.Content.ReadFromJsonAsync<int>();
    }
}
