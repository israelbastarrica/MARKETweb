using System.Net.Http.Json;
using MarketWeb.Shared.Tareas;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del programador de tareas (Sistemas, solo ADMIN).</summary>
public sealed class TareasApi
{
    private readonly HttpClient _http;
    public TareasApi(HttpClient http) => _http = http;

    public async Task<List<TareaProgramadaDto>> ListarAsync()
        => await _http.GetFromJsonAsync<List<TareaProgramadaDto>>("api/tareas") ?? new();

    public async Task<TareaProgramadaEditorDto?> ObtenerAsync(int id)
        => await _http.GetFromJsonAsync<TareaProgramadaEditorDto>($"api/tareas/{id}");

    public async Task<bool> GuardarAsync(TareaSaveRequest req)
        => (await _http.PostAsJsonAsync("api/tareas/guardar", req)).IsSuccessStatusCode;

    public async Task<bool> EliminarAsync(int id)
        => (await _http.DeleteAsync($"api/tareas/{id}")).IsSuccessStatusCode;

    public async Task<List<TareaLogDto>> HistorialAsync(int id)
        => await _http.GetFromJsonAsync<List<TareaLogDto>>($"api/tareas/{id}/historial") ?? new();

    /// <summary>Dispara la tarea ahora. Devuelve true si arrancó (false si ya estaba corriendo).</summary>
    public async Task<bool> EjecutarAsync(int id)
    {
        var resp = await _http.PostAsync($"api/tareas/{id}/ejecutar", null);
        if (!resp.IsSuccessStatusCode) return false;
        var r = await resp.Content.ReadFromJsonAsync<EjecResp>();
        return r?.Arrancada ?? false;
    }

    /// <summary>Reenvía solo el PDF + mail de la última corrida (sin re-correr el SP). Espera el resultado.</summary>
    public async Task<(bool Ok, string Resultado)> ReenviarAsync(int id)
    {
        var resp = await _http.PostAsync($"api/tareas/{id}/reenviar", null);
        if (!resp.IsSuccessStatusCode) return (false, $"HTTP {(int)resp.StatusCode}");
        var r = await resp.Content.ReadFromJsonAsync<ReenvResp>();
        return (r?.Ok ?? false, r?.Resultado ?? "");
    }

    private sealed class EjecResp { public bool Arrancada { get; set; } }
    private sealed class ReenvResp { public bool Ok { get; set; } public string Resultado { get; set; } = ""; }
}
