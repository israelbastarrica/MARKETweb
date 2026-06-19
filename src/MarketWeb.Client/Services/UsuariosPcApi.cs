using System.Net.Http.Json;
using MarketWeb.Shared.UsuariosPc;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del feature Usuarios (tabla UsuariosPC).</summary>
public sealed class UsuariosPcApi
{
    private readonly HttpClient _http;

    public UsuariosPcApi(HttpClient http) => _http = http;

    public async Task<List<UsuarioPcDto>> ListarAsync(string? filtro = null)
    {
        var url = "api/usuariospc";
        if (!string.IsNullOrWhiteSpace(filtro))
            url += $"?filtro={Uri.EscapeDataString(filtro)}";
        return await _http.GetFromJsonAsync<List<UsuarioPcDto>>(url) ?? new();
    }

    public async Task<List<string>> ListarPerfilesAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/usuariospc/perfiles") ?? new();

    public async Task<(bool Ok, string? Error)> CrearAsync(UsuarioPcSaveRequest req)
        => await LeerResultadoAsync(await _http.PostAsJsonAsync("api/usuariospc", req));

    public async Task<(bool Ok, string? Error)> ModificarAsync(int id, UsuarioPcSaveRequest req)
        => await LeerResultadoAsync(await _http.PutAsJsonAsync($"api/usuariospc/{id}", req));

    public async Task EliminarAsync(int id)
        => await _http.DeleteAsync($"api/usuariospc/{id}");

    public async Task AprobarAsync(int id)
        => await _http.PutAsync($"api/usuariospc/{id}/aprobar", null);

    private static async Task<(bool, string?)> LeerResultadoAsync(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
            return (false, err?.Mensaje ?? "No se pudo completar la operación.");
        }
        catch
        {
            return (false, "No se pudo completar la operación.");
        }
    }

    private sealed class ErrorResponse
    {
        public string? Mensaje { get; set; }
    }
}
