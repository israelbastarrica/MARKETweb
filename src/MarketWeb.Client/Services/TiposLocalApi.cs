using System.Net.Http.Json;
using MarketWeb.Shared.Locales;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del feature Tipos de Locales. Habla con TiposLocalesController.</summary>
public sealed class TiposLocalApi
{
    private readonly HttpClient _http;

    public TiposLocalApi(HttpClient http) => _http = http;

    public async Task<List<LocalTipoDto>> ListarAsync(string? filtro = null)
    {
        var url = "api/tiposlocales";
        if (!string.IsNullOrWhiteSpace(filtro))
            url += $"?filtro={Uri.EscapeDataString(filtro)}";
        return await _http.GetFromJsonAsync<List<LocalTipoDto>>(url) ?? new();
    }

    public async Task<(bool Ok, string? Error)> CrearAsync(TipoLocalSaveRequest req)
        => await LeerResultadoAsync(await _http.PostAsJsonAsync("api/tiposlocales", req));

    public async Task<(bool Ok, string? Error)> ModificarAsync(int id, TipoLocalSaveRequest req)
        => await LeerResultadoAsync(await _http.PutAsJsonAsync($"api/tiposlocales/{id}", req));

    public async Task EliminarAsync(int id)
        => await _http.DeleteAsync($"api/tiposlocales/{id}");

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
