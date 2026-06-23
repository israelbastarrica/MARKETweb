using System.Net.Http.Json;
using MarketWeb.Shared.Dragonfish;

namespace MarketWeb.Client.Services;

/// <summary>Cliente para probar la API Dragonfish (crear remito de insumos).</summary>
public sealed class DragonApi
{
    private readonly HttpClient _http;
    public DragonApi(HttpClient http) => _http = http;

    public async Task<bool> ConfiguradoAsync()
    {
        try
        {
            var r = await _http.GetFromJsonAsync<EstadoResp>("api/dragon/estado");
            return r?.Configurado ?? false;
        }
        catch { return false; }
    }

    public async Task<DragonRemitoResultDto> RemitoPruebaAsync(DragonRemitoRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/dragon/remito-prueba", req);
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<DragonRemitoResultDto>() ?? new() { Ok = false, Error = "Sin respuesta." };
        return new DragonRemitoResultDto { Ok = false, Error = $"HTTP {(int)resp.StatusCode}" };
    }

    // --- Alta real de remito desde la tablet de Logística (endpoint api/remitos, policy Aprobado) ---
    public async Task<bool> ConfiguradoRemitoAsync()
    {
        try
        {
            var r = await _http.GetFromJsonAsync<EstadoResp>("api/remitos/estado");
            return r?.Configurado ?? false;
        }
        catch { return false; }
    }

    public async Task<DragonRemitoResultDto> CrearRemitoAsync(DragonRemitoRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/remitos", req);
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<DragonRemitoResultDto>() ?? new() { Ok = false, Error = "Sin respuesta." };
        return new DragonRemitoResultDto { Ok = false, Error = $"HTTP {(int)resp.StatusCode}" };
    }

    private sealed class EstadoResp { public bool Configurado { get; set; } }
}
