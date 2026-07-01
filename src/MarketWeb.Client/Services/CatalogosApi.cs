using System.Net.Http.Json;
using MarketWeb.Shared.Produccion;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del módulo Catálogos (Producción) — port de frmABMCatalogo, sin Canva.</summary>
public sealed class CatalogosApi
{
    private readonly HttpClient _http;
    public CatalogosApi(HttpClient http) => _http = http;

    public async Task<List<CatalogoDto>> ListarAsync()
        => await _http.GetFromJsonAsync<List<CatalogoDto>>("api/catalogos") ?? new();

    public async Task<CatalogoDetalleDto?> DetalleAsync(int id)
        => await _http.GetFromJsonAsync<CatalogoDetalleDto>($"api/catalogos/{id}");

    public async Task<CatalogoCombosDto> CombosAsync()
        => await _http.GetFromJsonAsync<CatalogoCombosDto>("api/catalogos/combos") ?? new();

    public async Task<CatalogoRenglonDto?> ArticuloAsync(string codigo)
    {
        var resp = await _http.GetAsync($"api/catalogos/articulo/{Uri.EscapeDataString(codigo)}");
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<CatalogoRenglonDto>() : null;
    }

    public async Task<List<PedidoOrdenSelDto>> PedidosOrdenesAsync(string? filtro)
        => await _http.GetFromJsonAsync<List<PedidoOrdenSelDto>>($"api/catalogos/pedidos-ordenes?filtro={Uri.EscapeDataString(filtro ?? "")}") ?? new();

    public async Task<List<CatalogoRenglonDto>> ProformaAsync(int nro)
        => await _http.GetFromJsonAsync<List<CatalogoRenglonDto>>($"api/catalogos/proforma/{nro}") ?? new();

    public async Task<int> GuardarAsync(CatalogoGuardarRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/catalogos/guardar", req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<int>() : 0;
    }

    public async Task<bool> EliminarAsync(int id)
        => (await _http.DeleteAsync($"api/catalogos/{id}")).IsSuccessStatusCode;

    public async Task<bool> GenerarPdfAsync(int id)
    {
        var resp = await _http.PostAsync($"api/catalogos/{id}/generar-pdf", null);
        return resp.IsSuccessStatusCode && await resp.Content.ReadFromJsonAsync<bool>();
    }
}
