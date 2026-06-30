using System.Net.Http.Json;
using MarketWeb.Shared.Sistemas;

namespace MarketWeb.Client.Services;

/// <summary>Estado del servidor (ping) + Wake-on-LAN.</summary>
public sealed class ServidorApi
{
    private readonly HttpClient _http;
    public ServidorApi(HttpClient http) => _http = http;

    public async Task<ServidorEstadoDto?> EstadoAsync()
    {
        try { return await _http.GetFromJsonAsync<ServidorEstadoDto>("api/servidor/estado"); }
        catch { return null; }
    }

    public async Task<WolResultadoDto?> WolAsync()
    {
        var resp = await _http.PostAsync("api/servidor/wol", null);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<WolResultadoDto>() : null;
    }
}
