using System.Net.Http.Json;
using MarketWeb.Shared.Costos;
using MarketWeb.Shared.Insumos;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del feature Costos (reporte de margen).</summary>
public sealed class CostosApi
{
    private readonly HttpClient _http;

    public CostosApi(HttpClient http) => _http = http;

    public async Task<List<UbicacionDto>> ListarUbicacionesAsync()
        => await _http.GetFromJsonAsync<List<UbicacionDto>>("api/costos/ubicaciones") ?? new();

    public async Task<List<CostoMargenDto>> ListarMargenAsync(
        DateTime desde, DateTime hasta, string local, string agrupamiento)
    {
        var url = $"api/costos/margen?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}" +
                  $"&local={Uri.EscapeDataString(local)}&agrupamiento={Uri.EscapeDataString(agrupamiento)}";
        return await _http.GetFromJsonAsync<List<CostoMargenDto>>(url) ?? new();
    }

    public async Task<byte[]> ExportarMargenExcelAsync(
        DateTime desde, DateTime hasta, string local, string agrupamiento)
    {
        var url = $"api/costos/margen/excel?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}" +
                  $"&local={Uri.EscapeDataString(local)}&agrupamiento={Uri.EscapeDataString(agrupamiento)}";
        return await _http.GetByteArrayAsync(url);
    }
}
