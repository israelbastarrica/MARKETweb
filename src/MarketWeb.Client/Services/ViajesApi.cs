using System.Net.Http.Json;
using MarketWeb.Shared.Produccion;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del módulo Producción / Viajes.</summary>
public sealed class ViajesApi
{
    private readonly HttpClient _http;
    public ViajesApi(HttpClient http) => _http = http;

    public async Task<List<ViajeDto>> ListarAsync()
        => await _http.GetFromJsonAsync<List<ViajeDto>>("api/viajes") ?? new();

    public async Task<ViajeDto?> ViajeAsync(int id)
        => await _http.GetFromJsonAsync<ViajeDto>($"api/viajes/{id}");

    public async Task<List<ViajeArticuloDto>> ArticulosAsync(int id)
        => await _http.GetFromJsonAsync<List<ViajeArticuloDto>>($"api/viajes/{id}/articulos") ?? new();

    public async Task<List<ViajeProveedorDto>> ProveedoresAsync(int id)
        => await _http.GetFromJsonAsync<List<ViajeProveedorDto>>($"api/viajes/{id}/proveedores") ?? new();

    public async Task<List<ViajeContenedorDto>> ContenedoresAsync(int id)
        => await _http.GetFromJsonAsync<List<ViajeContenedorDto>>($"api/viajes/{id}/contenedores") ?? new();

    public async Task<ViajeArticuloFichaDto?> FichaAsync(int idArticulo)
        => await _http.GetFromJsonAsync<ViajeArticuloFichaDto>($"api/viajes/articulo/{idArticulo}");

    public async Task<bool> GuardarCodigoDragonAsync(int idArticulo, string codigo)
        => (await _http.PostAsJsonAsync($"api/viajes/articulo/{idArticulo}/codigo-dragon", new { codigo })).IsSuccessStatusCode;

    // ---- ABM ----
    public async Task<int> GuardarViajeAsync(ViajeSaveRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/viajes/guardar", req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<int>() : 0;
    }
    public async Task<bool> EliminarViajeAsync(int id)
        => (await _http.DeleteAsync($"api/viajes/{id}")).IsSuccessStatusCode;

    public async Task<ContenedorEditorDto?> ContenedorAsync(int id)
        => await _http.GetFromJsonAsync<ContenedorEditorDto>($"api/viajes/contenedor/{id}");
    public async Task<bool> GuardarContenedorAsync(ContenedorSaveRequest req)
        => (await _http.PostAsJsonAsync("api/viajes/contenedor", req)).IsSuccessStatusCode;
    public async Task<bool> EliminarContenedorAsync(int id)
        => (await _http.DeleteAsync($"api/viajes/contenedor/{id}")).IsSuccessStatusCode;

    public async Task<ProveedorEditorDto?> ProveedorAsync(int id)
        => await _http.GetFromJsonAsync<ProveedorEditorDto>($"api/viajes/proveedor/{id}");
    public async Task<bool> GuardarProveedorAsync(ProveedorEditorDto req)
        => (await _http.PostAsJsonAsync("api/viajes/proveedor", req)).IsSuccessStatusCode;
    public async Task<bool> EliminarProveedorAsync(int id)
        => (await _http.DeleteAsync($"api/viajes/proveedor/{id}")).IsSuccessStatusCode;

    public async Task<ArticuloEditorDto?> ArticuloEditorAsync(int id)
        => await _http.GetFromJsonAsync<ArticuloEditorDto>($"api/viajes/articulo/{id}/editor");
    public async Task<int> GuardarArticuloAsync(ArticuloEditorDto req)
    {
        var resp = await _http.PostAsJsonAsync("api/viajes/articulo", req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<int>() : 0;
    }
    public async Task<bool> EliminarArticuloAsync(int id)
        => (await _http.DeleteAsync($"api/viajes/articulo/{id}")).IsSuccessStatusCode;

    public async Task<int> ContarCodigosMarketAsync()
        => await _http.GetFromJsonAsync<int>("api/viajes/codigos-market/count");

    public async Task<ImportarCodigosResultadoDto?> ImportarCodigosMarketAsync(string texto)
    {
        var resp = await _http.PostAsJsonAsync("api/viajes/codigos-market", new { texto });
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<ImportarCodigosResultadoDto>() : null;
    }

    /// <summary>URL para mostrar una foto del viaje (nombre de archivo).</summary>
    public static string FotoUrl(string archivo)
        => string.IsNullOrWhiteSpace(archivo) ? "" : $"api/viajes/foto?archivo={Uri.EscapeDataString(archivo)}";

    public async Task<ImportarViajeResultadoDto?> ImportarAsync()
    {
        var resp = await _http.PostAsync("api/viajes/importar", null);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ImportarViajeResultadoDto>();
    }
}
