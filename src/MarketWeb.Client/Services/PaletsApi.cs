using System.Net.Http.Json;
using MarketWeb.Shared.Palets;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del feature Palets. Habla con PaletsController.</summary>
public sealed class PaletsApi
{
    private readonly HttpClient _http;
    public PaletsApi(HttpClient http) => _http = http;

    public async Task<List<PaletDto>> ListarAsync(string? nroPalet, string? codArticulo, string? tipo, string? categoria, bool verDesarmados, DateTime desde)
    {
        var url = $"api/palets?verDesarmados={verDesarmados.ToString().ToLowerInvariant()}&desde={desde:yyyy-MM-dd}";
        if (!string.IsNullOrWhiteSpace(nroPalet)) url += $"&nroPalet={Uri.EscapeDataString(nroPalet)}";
        if (!string.IsNullOrWhiteSpace(codArticulo)) url += $"&codArticulo={Uri.EscapeDataString(codArticulo)}";
        if (!string.IsNullOrWhiteSpace(tipo) && tipo != "TODOS") url += $"&tipo={Uri.EscapeDataString(tipo)}";
        if (!string.IsNullOrWhiteSpace(categoria) && categoria != "TODOS") url += $"&categoria={Uri.EscapeDataString(categoria)}";
        return await _http.GetFromJsonAsync<List<PaletDto>>(url) ?? new();
    }

    public async Task<List<string>> ListarTiposAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/palets/tipos") ?? new();

    public async Task<List<string>> ListarCategoriasAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/palets/categorias") ?? new();

    public async Task<List<PaletArticuloDto>> ListarArticulosAsync(int id)
        => await _http.GetFromJsonAsync<List<PaletArticuloDto>>($"api/palets/{id}/articulos") ?? new();

    public async Task<bool> DesarmarAsync(int id)
    {
        var resp = await _http.PostAsync($"api/palets/{id}/desarmar", null);
        return resp.IsSuccessStatusCode;
    }
}
