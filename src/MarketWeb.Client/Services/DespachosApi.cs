using System.Net.Http.Json;
using MarketWeb.Shared.Despachos;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del feature Despachos. Habla con DespachosController.</summary>
public sealed class DespachosApi
{
    private readonly HttpClient _http;
    public DespachosApi(HttpClient http) => _http = http;

    public async Task<List<DespachoLocalDto>> ListarLocalesAsync()
        => await _http.GetFromJsonAsync<List<DespachoLocalDto>>("api/despachos/locales") ?? new();

    public async Task<List<DespachoDto>> ListarAsync(DateTime desde, DateTime hasta, int? local)
    {
        var url = $"api/despachos?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
        if (local is > 0) url += $"&local={local}";
        return await _http.GetFromJsonAsync<List<DespachoDto>>(url) ?? new();
    }

    public async Task<List<DespachoArticuloDto>> ListarArticulosAsync(string remitoId, string origen)
        => await _http.GetFromJsonAsync<List<DespachoArticuloDto>>(
            $"api/despachos/articulos?remito={Uri.EscapeDataString(remitoId)}&origen={Uri.EscapeDataString(origen)}") ?? new();

    public async Task<QrRemitoDto?> GenerarQrAsync(string remitoCodigo, bool esPc)
    {
        var resp = await _http.PostAsJsonAsync("api/despachos/qr", new QrRequest { Remito = remitoCodigo, EsPc = esPc });
        return await resp.Content.ReadFromJsonAsync<QrRemitoDto>();
    }
}
