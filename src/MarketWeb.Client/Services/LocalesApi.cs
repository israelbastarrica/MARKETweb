using System.Net.Http.Json;
using MarketWeb.Shared.Locales;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del feature Locales. Habla con LocalesController.</summary>
public sealed class LocalesApi
{
    private readonly HttpClient _http;

    public LocalesApi(HttpClient http) => _http = http;

    public async Task<List<LocalDto>> ListarAsync(string? filtro = null)
    {
        var url = "api/locales";
        if (!string.IsNullOrWhiteSpace(filtro))
            url += $"?filtro={Uri.EscapeDataString(filtro)}";
        return await _http.GetFromJsonAsync<List<LocalDto>>(url) ?? new();
    }

    public async Task<List<LocalTipoDto>> ListarTiposAsync()
        => await _http.GetFromJsonAsync<List<LocalTipoDto>>("api/locales/tipos") ?? new();

    public async Task<(bool Ok, string? Error)> CrearAsync(LocalSaveRequest req)
        => await LeerResultadoAsync(await _http.PostAsJsonAsync("api/locales", req));

    public async Task<(bool Ok, string? Error)> ModificarAsync(int id, LocalSaveRequest req)
        => await LeerResultadoAsync(await _http.PutAsJsonAsync($"api/locales/{id}", req));

    public async Task EliminarAsync(int id)
        => await _http.DeleteAsync($"api/locales/{id}");

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
