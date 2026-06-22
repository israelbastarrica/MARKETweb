using System.Net.Http.Json;
using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del ABM de Reseteados (RepoReposicionArticulosReseteados).</summary>
public sealed class ReseteadosApi
{
    private readonly HttpClient _http;
    public ReseteadosApi(HttpClient http) => _http = http;

    public async Task<List<string>> MobiliariosAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/reseteados/mobiliarios") ?? new();

    public async Task<List<ReseteadoDto>> ListarAsync(string local, string mobiliario, string artCod)
        => await _http.GetFromJsonAsync<List<ReseteadoDto>>(
            $"api/reseteados?local={Uri.EscapeDataString(local)}&mobiliario={Uri.EscapeDataString(mobiliario)}&artCod={Uri.EscapeDataString(artCod)}") ?? new();

    public async Task<ReseteadoEditorDto?> ObtenerAsync(int id)
        => await _http.GetFromJsonAsync<ReseteadoEditorDto>($"api/reseteados/{id}");

    public async Task<bool> GuardarAsync(ReseteadoSaveRequest req)
        => (await _http.PostAsJsonAsync("api/reseteados/guardar", req)).IsSuccessStatusCode;

    public async Task<bool> EliminarAsync(int id)
        => (await _http.DeleteAsync($"api/reseteados/{id}")).IsSuccessStatusCode;
}
