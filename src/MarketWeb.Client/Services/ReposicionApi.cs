using System.Net.Http.Json;
using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del feature Reposición (Logística). La corrida tarda ~2 min → arranca un job y se consulta por polling.</summary>
public sealed class ReposicionApi
{
    private readonly HttpClient _http;
    public ReposicionApi(HttpClient http) => _http = http;

    /// <summary>Arranca la corrida. Devuelve el jobId, o null si falló el arranque.</summary>
    public async Task<string?> CalcularAsync(ReposicionCalcularRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/reposicion/calcular", req);
        if (!resp.IsSuccessStatusCode) return null;
        var r = await resp.Content.ReadFromJsonAsync<JobStart>();
        return r?.JobId;
    }

    /// <summary>Estado/resultado de la corrida. null si el job no existe (purga / reinicio del server).</summary>
    public async Task<ReposicionJobDto?> EstadoAsync(string jobId)
    {
        try { return await _http.GetFromJsonAsync<ReposicionJobDto>($"api/reposicion/calcular/{jobId}"); }
        catch { return null; }
    }

    /// <summary>Historial de corridas guardadas (para reimprimir).</summary>
    public async Task<List<CorridaDto>> ListarCorridasAsync()
        => await _http.GetFromJsonAsync<List<CorridaDto>>("api/reposicion/historial") ?? new();

    /// <summary>"Explain" de un artículo en un local (por qué repone lo que repone).</summary>
    public async Task<ExplicarDto?> ExplicarAsync(string local, string artCod)
        => await _http.GetFromJsonAsync<ExplicarDto>(
            $"api/reposicion/explicar?local={Uri.EscapeDataString(local)}&artCod={Uri.EscapeDataString(artCod)}");

    /// <summary>Resetea un artículo desde un remito. Devuelve Ok + mensaje (idempotente).</summary>
    public async Task<ResetResultadoDto> ResetearDesdeRemitoAsync(ResetRemitoRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/reposicion/resetear", req);
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<ResetResultadoDto>() ?? new ResetResultadoDto { Ok = false, Mensaje = "Sin respuesta." };
        return new ResetResultadoDto { Ok = false, Mensaje = "No se pudo resetear." };
    }

    /// <summary>Reset firmado desde un evento de piso (packs con signo, ancla al último remito).</summary>
    public async Task<ResetResultadoDto> ResetearDesdeEventoAsync(int idEvento, string comentario)
    {
        var resp = await _http.PostAsJsonAsync("api/reposicion/resetear-evento", new { idEvento, comentario });
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<ResetResultadoDto>() ?? new ResetResultadoDto { Ok = false, Mensaje = "Sin respuesta." };
        return new ResetResultadoDto { Ok = false, Mensaje = "No se pudo resetear." };
    }

    private sealed class JobStart { public string JobId { get; set; } = ""; }
}
