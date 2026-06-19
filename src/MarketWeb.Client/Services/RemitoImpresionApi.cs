using System.Net.Http.Json;
using MarketWeb.Shared.RemitoImpresion;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del feature Remito Impresión. Habla con RemitoImpresionController.</summary>
public sealed class RemitoImpresionApi
{
    private readonly HttpClient _http;
    public RemitoImpresionApi(HttpClient http) => _http = http;

    public async Task<List<string>> ListarLocalesAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/remitoimpresion/locales") ?? new();

    public async Task<List<ImpresoraColaDto>> ListarImpresorasAsync()
        => await _http.GetFromJsonAsync<List<ImpresoraColaDto>>("api/remitoimpresion/impresoras") ?? new();

    public async Task<List<RemitoColaDto>> ListarAsync(DateTime desde, DateTime hasta, string? local, string? estado, bool soloErrores, int? saltafw = null)
    {
        var url = $"api/remitoimpresion?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}&soloErrores={soloErrores.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(local) && local != "TODOS") url += $"&local={Uri.EscapeDataString(local)}";
        if (!string.IsNullOrWhiteSpace(estado) && estado != "TODOS") url += $"&estado={Uri.EscapeDataString(estado)}";
        if (saltafw is not null) url += $"&saltafw={saltafw.Value}";
        return await _http.GetFromJsonAsync<List<RemitoColaDto>>(url) ?? new();
    }

    public async Task<bool> ReimprimirAsync(int id)
    {
        var resp = await _http.PostAsync($"api/remitoimpresion/{id}/reimprimir", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<List<RemitoEstadoDto>> EstadoAsync(IEnumerable<int> ids)
        => await _http.GetFromJsonAsync<List<RemitoEstadoDto>>($"api/remitoimpresion/estado?ids={string.Join(",", ids)}") ?? new();
}
