using System.Net.Http.Json;
using MarketWeb.Shared.Mapeo;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del feature Mapeo (estructura). Habla con MapeoController.</summary>
public sealed class MapeoApi
{
    private readonly HttpClient _http;
    public MapeoApi(HttpClient http) => _http = http;

    public async Task<List<MapeoUbicacionDto>> ListarUbicacionesAsync()
        => await _http.GetFromJsonAsync<List<MapeoUbicacionDto>>("api/mapeo/ubicaciones") ?? new();

    public async Task<List<string>> ListarSectoresAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/mapeo/sectores") ?? new();

    public async Task<List<string>> ListarMobiliariosAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/mapeo/mobiliarios") ?? new();

    public async Task<List<MapeoPosicionDto>> ListarPosicionesAsync(int idUbicacion)
        => await _http.GetFromJsonAsync<List<MapeoPosicionDto>>($"api/mapeo/posiciones?idUbicacion={idUbicacion}") ?? new();

    public async Task<(bool Ok, string? Error)> CrearPosicionAsync(MapeoPosicionSaveRequest req)
        => await Leer(await _http.PostAsJsonAsync("api/mapeo/posiciones", req));

    public async Task<(bool Ok, string? Error)> ModificarPosicionAsync(int id, MapeoPosicionSaveRequest req)
        => await Leer(await _http.PutAsJsonAsync($"api/mapeo/posiciones/{id}", req));

    public async Task EliminarPosicionAsync(int id)
        => await _http.DeleteAsync($"api/mapeo/posiciones/{id}");

    public async Task<List<MapeoArticuloDto>> ListarArticulosAsync(int idMapeo)
        => await _http.GetFromJsonAsync<List<MapeoArticuloDto>>($"api/mapeo/articulos?idMapeo={idMapeo}") ?? new();

    public async Task<(bool Ok, string? Error)> CrearArticuloAsync(MapeoArticuloSaveRequest req)
        => await Leer(await _http.PostAsJsonAsync("api/mapeo/articulos", req));

    public async Task<(bool Ok, string? Error)> ModificarArticuloAsync(int id, MapeoArticuloSaveRequest req)
        => await Leer(await _http.PutAsJsonAsync($"api/mapeo/articulos/{id}", req));

    public async Task EliminarArticuloAsync(int id)
        => await _http.DeleteAsync($"api/mapeo/articulos/{id}");

    // ---- Reporte (Logística) ----
    public async Task<List<MapeoUbicacionDto>> ListarUbicacionesReporteAsync()
        => await _http.GetFromJsonAsync<List<MapeoUbicacionDto>>("api/mapeo/reporte/ubicaciones") ?? new();

    public async Task<List<string>> ListarTiposAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/mapeo/reporte/tipos") ?? new();

    public async Task<List<string>> ListarCategoriasAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/mapeo/reporte/categorias") ?? new();

    public async Task<List<MapeoReporteDto>> ReporteAsync(MapeoReporteRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/mapeo/reporte", req);
        return resp.IsSuccessStatusCode ? (await resp.Content.ReadFromJsonAsync<List<MapeoReporteDto>>() ?? new()) : new();
    }

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
