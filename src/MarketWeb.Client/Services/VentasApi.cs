using System.Net.Http.Json;
using MarketWeb.Shared.Ventas;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del feature Ventas (resumen + cobranzas por medio de pago).</summary>
public sealed class VentasApi
{
    private readonly HttpClient _http;

    public VentasApi(HttpClient http) => _http = http;

    public async Task<List<VentaResumenDto>> ResumenAsync(DateTime desde, DateTime hasta)
        => await _http.GetFromJsonAsync<List<VentaResumenDto>>(
               $"api/ventas/resumen?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}") ?? new();

    public async Task<List<CobranzaDto>> CobranzasAsync(
        DateTime desde, DateTime hasta, string local, string agrupamiento,
        string detalle, string categoria, string medio)
    {
        var url = $"api/ventas/cobranzas?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}"
                + $"&local={Uri.EscapeDataString(local)}&agrupamiento={Uri.EscapeDataString(agrupamiento)}"
                + $"&detalle={Uri.EscapeDataString(detalle)}&categoria={Uri.EscapeDataString(categoria)}"
                + $"&medio={Uri.EscapeDataString(medio)}";
        return await _http.GetFromJsonAsync<List<CobranzaDto>>(url) ?? new();
    }

    public async Task<byte[]> ExportarExcelAsync(
        DateTime desde, DateTime hasta, string local, string agrupamiento,
        string detalle, string categoria, string medio)
    {
        var url = $"api/ventas/excel?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}"
                + $"&local={Uri.EscapeDataString(local)}&agrupamiento={Uri.EscapeDataString(agrupamiento)}"
                + $"&detalle={Uri.EscapeDataString(detalle)}&categoria={Uri.EscapeDataString(categoria)}"
                + $"&medio={Uri.EscapeDataString(medio)}";
        return await _http.GetByteArrayAsync(url);
    }
}
