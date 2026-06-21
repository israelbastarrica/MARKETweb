using System.Net.Http.Json;
using MarketWeb.Shared.Reemplazos;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del feature Reemplazos (Logística).</summary>
public sealed class ReemplazosApi
{
    private readonly HttpClient _http;
    public ReemplazosApi(HttpClient http) => _http = http;

    public async Task<List<LocalReemplazoDto>> LocalesAsync()
        => await _http.GetFromJsonAsync<List<LocalReemplazoDto>>("api/reemplazos/locales") ?? new();

    public async Task<List<ReemplazoDto>> ListarAsync(int idUbicacion, bool verTodos)
        => await _http.GetFromJsonAsync<List<ReemplazoDto>>($"api/reemplazos?idUbicacion={idUbicacion}&verTodos={(verTodos ? "true" : "false")}") ?? new();

    public async Task<ReemplazoEditorDto?> ObtenerAsync(int id)
        => await _http.GetFromJsonAsync<ReemplazoEditorDto>($"api/reemplazos/{id}");

    public async Task<ArticuloDescDto?> ArticuloAsync(string cod)
    {
        try { return await _http.GetFromJsonAsync<ArticuloDescDto>($"api/reemplazos/articulo?cod={Uri.EscapeDataString(cod)}"); }
        catch { return null; }
    }

    public async Task<ValidacionReemplazoDto> ValidarAsync(int idUbicacion, string cod)
        => await _http.GetFromJsonAsync<ValidacionReemplazoDto>($"api/reemplazos/validar?idUbicacion={idUbicacion}&cod={Uri.EscapeDataString(cod)}") ?? new();

    public async Task<List<ReemplazoCandidatoDto>> CandidatosAsync(int idUbicacion, string cod)
        => await _http.GetFromJsonAsync<List<ReemplazoCandidatoDto>>($"api/reemplazos/candidatos?idUbicacion={idUbicacion}&cod={Uri.EscapeDataString(cod)}") ?? new();

    public async Task<List<ReemplazoCandidatoDto>> CandidatosPercheroAsync(int idUbicacion, string cod)
        => await _http.GetFromJsonAsync<List<ReemplazoCandidatoDto>>($"api/reemplazos/candidatos-perchero?idUbicacion={idUbicacion}&cod={Uri.EscapeDataString(cod)}") ?? new();

    public async Task<(bool Ok, string? Error)> GuardarAsync(ReemplazoSaveRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/reemplazos/guardar", req);
        if (resp.IsSuccessStatusCode) return (true, null);
        try { var e = await resp.Content.ReadFromJsonAsync<ErrorResp>(); return (false, e?.Mensaje ?? "No se pudo guardar."); }
        catch { return (false, "No se pudo guardar."); }
    }

    public async Task<MarcarProcesadosResultadoDto?> MarcarProcesadosAsync(int idUbicacion)
    {
        var resp = await _http.PostAsJsonAsync("api/reemplazos/marcar-procesados", new { idUbicacion });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<MarcarProcesadosResultadoDto>();
    }

    public async Task<bool> EliminarAsync(int id)
    {
        var resp = await _http.DeleteAsync($"api/reemplazos/{id}");
        return resp.IsSuccessStatusCode;
    }

    // ---- Reemplazo por Mueble (bloqueos) ----

    public async Task<List<string>> MobiliariosAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/reemplazos/mueble/mobiliarios") ?? new();

    public async Task<List<BloqueoMuebleDto>> ListarBloqueosAsync(string local, string mobiliario, string artCod)
        => await _http.GetFromJsonAsync<List<BloqueoMuebleDto>>(
            $"api/reemplazos/mueble?local={Uri.EscapeDataString(local)}&mobiliario={Uri.EscapeDataString(mobiliario)}&artCod={Uri.EscapeDataString(artCod)}") ?? new();

    public async Task<BloqueoMuebleEditorDto?> ObtenerBloqueoAsync(int id)
        => await _http.GetFromJsonAsync<BloqueoMuebleEditorDto>($"api/reemplazos/mueble/{id}");

    public async Task<(bool Ok, string? Error)> GuardarBloqueoAsync(BloqueoMuebleSaveRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/reemplazos/mueble/guardar", req);
        if (resp.IsSuccessStatusCode) return (true, null);
        try { var e = await resp.Content.ReadFromJsonAsync<ErrorResp>(); return (false, e?.Mensaje ?? "No se pudo guardar."); }
        catch { return (false, "No se pudo guardar."); }
    }

    public async Task<bool> EliminarBloqueoAsync(int id)
    {
        var resp = await _http.DeleteAsync($"api/reemplazos/mueble/{id}");
        return resp.IsSuccessStatusCode;
    }

    private sealed class ErrorResp { public string? Mensaje { get; set; } }
}
