using System.Net.Http.Json;
using MarketWeb.Shared.ConfigImagenes;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del feature Config Imágenes (Diseño). Habla con ConfigImagenesController.</summary>
public sealed class ConfigImagenesApi
{
    private readonly HttpClient _http;
    public ConfigImagenesApi(HttpClient http) => _http = http;

    public async Task<List<ConfigImagenDto>> ListarAsync(string? tipo, string? descripcion)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(tipo) && tipo != "TODOS") qs.Add($"tipo={Uri.EscapeDataString(tipo)}");
        if (!string.IsNullOrWhiteSpace(descripcion)) qs.Add($"descripcion={Uri.EscapeDataString(descripcion)}");
        var url = "api/configimagenes" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await _http.GetFromJsonAsync<List<ConfigImagenDto>>(url) ?? new();
    }

    public async Task<(bool Ok, string? Error)> CrearAsync(ConfigImagenSaveRequest req)
        => await Leer(await _http.PostAsJsonAsync("api/configimagenes", req));

    public async Task<(bool Ok, string? Error)> ModificarAsync(int id, ConfigImagenSaveRequest req)
        => await Leer(await _http.PutAsJsonAsync($"api/configimagenes/{id}", req));

    public async Task EliminarAsync(int id)
        => await _http.DeleteAsync($"api/configimagenes/{id}");

    /// <summary>URL del binario de la imagen. El parámetro v fuerza recarga tras modificar.</summary>
    public static string ImagenUrl(int id, object? v = null)
        => $"api/configimagenes/{id}/imagen" + (v is null ? "" : $"?v={Uri.EscapeDataString(v.ToString() ?? "")}");

    private static async Task<(bool, string?)> Leer(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return (true, null);
        try
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
            return (false, err?.Mensaje ?? "No se pudo completar la operación.");
        }
        catch { return (false, "No se pudo completar la operación."); }
    }

    private sealed class ErrorResponse { public string? Mensaje { get; set; } }
}
