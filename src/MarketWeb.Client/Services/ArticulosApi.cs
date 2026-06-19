using System.Net;
using System.Net.Http.Json;
using MarketWeb.Shared.Articulos;
using MarketWeb.Shared.Insumos;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del feature Consulta de Artículos.</summary>
public sealed class ArticulosApi
{
    private readonly HttpClient _http;

    public ArticulosApi(HttpClient http) => _http = http;

    public async Task<List<UbicacionDto>> ListarUbicacionesAsync()
        => await _http.GetFromJsonAsync<List<UbicacionDto>>("api/articulos/ubicaciones") ?? new();

    /// <summary>Devuelve la ficha, o null si no se encontró el artículo (404).</summary>
    public async Task<ConsultaArticuloDto?> ConsultarAsync(string codigo, string ubicacion)
    {
        var url = $"api/articulos/consultar?codigo={Uri.EscapeDataString(codigo)}&ubicacion={Uri.EscapeDataString(ubicacion)}";
        var resp = await _http.GetAsync(url);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ConsultaArticuloDto>();
    }

    /// <summary>Palets del depósito que contienen el artículo (búsqueda lenta).</summary>
    public async Task<List<UbicacionArtDto>> BuscarEnPaletsAsync(string codigo)
        => await _http.GetFromJsonAsync<List<UbicacionArtDto>>($"api/articulos/palets?codigo={Uri.EscapeDataString(codigo)}") ?? new();
}
