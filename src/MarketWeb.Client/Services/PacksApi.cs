using System.Net.Http.Json;
using MarketWeb.Shared.Packs;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP del reporte de Packs (Producción). Habla con PacksController.</summary>
public sealed class PacksApi
{
    private readonly HttpClient _http;
    public PacksApi(HttpClient http) => _http = http;

    public async Task<List<PackDto>> ListarAsync(string? nroPedido, string? codArt, bool verDesarmados)
    {
        var qs = new List<string> { $"verDesarmados={(verDesarmados ? "true" : "false")}" };
        if (!string.IsNullOrWhiteSpace(nroPedido)) qs.Add($"nroPedido={Uri.EscapeDataString(nroPedido)}");
        if (!string.IsNullOrWhiteSpace(codArt)) qs.Add($"codArt={Uri.EscapeDataString(codArt)}");
        var url = "api/packs?" + string.Join("&", qs);
        return await _http.GetFromJsonAsync<List<PackDto>>(url) ?? new();
    }

    public async Task EliminarAsync(string nroPedido)
        => await _http.DeleteAsync($"api/packs?nroPedido={Uri.EscapeDataString(nroPedido)}");

    public static string PdfUrl(int id) => $"api/packs/{id}/pdf";
    public static string TxtUrl(int id) => $"api/packs/{id}/txt";
}
