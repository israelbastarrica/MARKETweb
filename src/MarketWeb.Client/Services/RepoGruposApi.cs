using System.Net.Http.Json;
using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del ABM de grupos de artículos unificados (Reposición).</summary>
public sealed class RepoGruposApi
{
    private readonly HttpClient _http;
    public RepoGruposApi(HttpClient http) => _http = http;

    public async Task<List<GrupoUnificadoDto>> ListarAsync()
        => await _http.GetFromJsonAsync<List<GrupoUnificadoDto>>("api/repo-grupos") ?? new();

    public async Task<GrupoUnificadoDetalleDto?> DetalleAsync(int id)
        => await _http.GetFromJsonAsync<GrupoUnificadoDetalleDto>($"api/repo-grupos/{id}");

    public async Task<GrupoArticuloDto?> ArticuloAsync(string codigo)
    {
        var resp = await _http.GetAsync($"api/repo-grupos/articulo/{Uri.EscapeDataString(codigo)}");
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<GrupoArticuloDto>() : null;
    }

    public async Task<int> GuardarAsync(GrupoUnificadoGuardarRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/repo-grupos/guardar", req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<int>() : 0;
    }

    public async Task<bool> EliminarAsync(int id)
        => (await _http.DeleteAsync($"api/repo-grupos/{id}")).IsSuccessStatusCode;
}
