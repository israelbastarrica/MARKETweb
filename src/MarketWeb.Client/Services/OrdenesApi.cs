using System.Net.Http.Json;
using MarketWeb.Shared.Produccion;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del módulo Diseño / Órdenes de Pedido (Fase 1).</summary>
public sealed class OrdenesApi
{
    private readonly HttpClient _http;
    public OrdenesApi(HttpClient http) => _http = http;

    public async Task<List<OrdenDto>> ListarAsync()
        => await _http.GetFromJsonAsync<List<OrdenDto>>("api/ordenes") ?? new();

    public async Task<OrdenDetalleDto?> DetalleAsync(int id)
        => await _http.GetFromJsonAsync<OrdenDetalleDto>($"api/ordenes/{id}");

    public async Task<ImportarOrdenesResultadoDto?> ImportarMuestraAsync()
    {
        var resp = await _http.PostAsync("api/ordenes/importar-muestra", null);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<ImportarOrdenesResultadoDto>() : null;
    }

    public async Task<ImportarOrdenesResultadoDto?> ImportarOrdenAsync(int nroOrden)
    {
        var resp = await _http.PostAsync($"api/ordenes/importar/{nroOrden}", null);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<ImportarOrdenesResultadoDto>() : null;
    }

    public async Task<List<ComboRangoDto>> CombosAsync()
        => await _http.GetFromJsonAsync<List<ComboRangoDto>>("api/ordenes/combos") ?? new();

    public async Task<OrdenCabeceraCombosDto> CombosCabeceraAsync()
        => await _http.GetFromJsonAsync<OrdenCabeceraCombosDto>("api/ordenes/combos-cabecera") ?? new();

    public async Task<List<TelaColorDto>> ColoresTelaAsync()
        => await _http.GetFromJsonAsync<List<TelaColorDto>>("api/ordenes/colores-tela") ?? new();

    public async Task<List<OrdenColorDto>> ColoresAsync(int idRenglon)
        => await _http.GetFromJsonAsync<List<OrdenColorDto>>($"api/ordenes/renglon/{idRenglon}/colores") ?? new();

    public async Task<bool> GuardarColoresAsync(int idRenglon, List<OrdenColorDto> colores)
        => (await _http.PostAsJsonAsync($"api/ordenes/renglon/{idRenglon}/colores", colores)).IsSuccessStatusCode;

    public async Task<List<OrdenProduccionCeldaDto>> ProduccionAsync(int idRenglon)
        => await _http.GetFromJsonAsync<List<OrdenProduccionCeldaDto>>($"api/ordenes/renglon/{idRenglon}/produccion") ?? new();

    public async Task<bool> GuardarProduccionAsync(int idRenglon, List<OrdenProduccionCeldaDto> celdas)
        => (await _http.PostAsJsonAsync($"api/ordenes/renglon/{idRenglon}/produccion", celdas)).IsSuccessStatusCode;

    public async Task<int> GuardarCabeceraAsync(OrdenSaveRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/ordenes/guardar", req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<int>() : 0;
    }

    public async Task<bool> GuardarRenglonAsync(OrdenRenglonSaveRequest req)
        => (await _http.PostAsJsonAsync("api/ordenes/renglon", req)).IsSuccessStatusCode;

    public async Task<bool> EliminarRenglonAsync(int id)
        => (await _http.DeleteAsync($"api/ordenes/renglon/{id}")).IsSuccessStatusCode;

    public async Task<bool> EliminarOrdenAsync(int id)
        => (await _http.DeleteAsync($"api/ordenes/{id}")).IsSuccessStatusCode;
}
