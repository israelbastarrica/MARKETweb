using System.Net.Http.Json;
using MarketWeb.Shared.Telas;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del feature Telas (Producción). Habla con TelasController.</summary>
public sealed class TelasApi
{
    private readonly HttpClient _http;
    public TelasApi(HttpClient http) => _http = http;

    // ---- Telas ----
    public async Task<List<TelaDto>> ListarAsync(int? idDeposito, string? material, int? idTextil)
    {
        var qs = new List<string>();
        if (idDeposito is > 0) qs.Add($"idDeposito={idDeposito}");
        if (idTextil is > 0) qs.Add($"idTextil={idTextil}");
        if (!string.IsNullOrWhiteSpace(material)) qs.Add($"material={Uri.EscapeDataString(material)}");
        var url = "api/telas" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await _http.GetFromJsonAsync<List<TelaDto>>(url) ?? new();
    }

    public async Task<(bool Ok, string? Error)> CrearAsync(TelaSaveRequest req)
        => await Leer(await _http.PostAsJsonAsync("api/telas", req));

    public async Task<(bool Ok, string? Error)> ModificarAsync(int id, TelaSaveRequest req)
        => await Leer(await _http.PutAsJsonAsync($"api/telas/{id}", req));

    public async Task EliminarAsync(int id)
        => await _http.DeleteAsync($"api/telas/{id}");

    // ---- Colores (Dragonfish) ----
    public async Task<List<ColorDragonDto>> ListarColoresAsync()
        => await _http.GetFromJsonAsync<List<ColorDragonDto>>("api/telas/colores") ?? new();

    // ---- Catálogos propios ----
    public async Task<List<CatalogoItemDto>> ListarCatalogoAsync(string tipo)
        => await _http.GetFromJsonAsync<List<CatalogoItemDto>>($"api/telas/catalogos/{tipo}") ?? new();

    public async Task<(bool Ok, string? Error)> CrearCatalogoAsync(string tipo, CatalogoSaveRequest req)
        => await Leer(await _http.PostAsJsonAsync($"api/telas/catalogos/{tipo}", req));

    public async Task<(bool Ok, string? Error)> ModificarCatalogoAsync(string tipo, int id, CatalogoSaveRequest req)
        => await Leer(await _http.PutAsJsonAsync($"api/telas/catalogos/{tipo}/{id}", req));

    public async Task<(bool Ok, string? Error)> EliminarCatalogoAsync(string tipo, int id)
        => await Leer(await _http.DeleteAsync($"api/telas/catalogos/{tipo}/{id}"));

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
