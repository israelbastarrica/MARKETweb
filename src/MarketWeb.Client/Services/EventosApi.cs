using System.Net.Http.Json;
using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Client.Services;

/// <summary>Cliente de Eventos de reposición (sobrante/faltante).</summary>
public sealed class EventosApi
{
    private readonly HttpClient _http;
    public EventosApi(HttpClient http) => _http = http;

    public async Task<List<EventoDto>> ListarAsync(string local, DateTime desde, DateTime hasta, bool verTodos)
    {
        var url = $"api/eventos?local={Uri.EscapeDataString(local)}&desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}&verTodos={(verTodos ? "true" : "false")}";
        return await _http.GetFromJsonAsync<List<EventoDto>>(url) ?? new();
    }

    public async Task<EventoDetalleDto?> DetalleAsync(int id)
        => await _http.GetFromJsonAsync<EventoDetalleDto>($"api/eventos/{id}");

    public async Task<bool> MarcarProcesadoAsync(int id)
        => (await _http.PostAsync($"api/eventos/{id}/procesar", null)).IsSuccessStatusCode;

    public async Task<bool> GuardarAccionAsync(int id, string accion)
        => (await _http.PostAsJsonAsync($"api/eventos/{id}/accion", new { accion })).IsSuccessStatusCode;

    public async Task<bool> EliminarAsync(int id)
        => (await _http.DeleteAsync($"api/eventos/{id}")).IsSuccessStatusCode;
}
