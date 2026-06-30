using System.Net.Http.Json;
using MarketWeb.Shared.PedidosOrdenes;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP de Órdenes de Pedido (Diseño). Habla con PedidosOrdenesController.</summary>
public sealed class PedidosOrdenesApi
{
    private readonly HttpClient _http;
    public PedidosOrdenesApi(HttpClient http) => _http = http;

    public async Task<List<PedidoOrdenListaDto>> ListarAsync(PedidoOrdenFiltro f)
    {
        var qs = new List<string>();
        if (f.NroOrden is > 0) qs.Add($"nroOrden={f.NroOrden}");
        if (!string.IsNullOrWhiteSpace(f.CodArt)) qs.Add($"codArt={Uri.EscapeDataString(f.CodArt)}");
        if (!string.IsNullOrWhiteSpace(f.Tipo)) qs.Add($"tipo={Uri.EscapeDataString(f.Tipo)}");
        if (!string.IsNullOrWhiteSpace(f.Estado)) qs.Add($"estado={Uri.EscapeDataString(f.Estado)}");
        if (!string.IsNullOrWhiteSpace(f.Ficha)) qs.Add($"ficha={f.Ficha}");
        if (!string.IsNullOrWhiteSpace(f.ArtDragon)) qs.Add($"artDragon={f.ArtDragon}");
        if (!string.IsNullOrWhiteSpace(f.CodProveedor)) qs.Add($"codProveedor={Uri.EscapeDataString(f.CodProveedor)}");
        if (!string.IsNullOrWhiteSpace(f.Finalizada)) qs.Add($"finalizada={f.Finalizada}");
        var url = "api/pedidos-ordenes" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await _http.GetFromJsonAsync<List<PedidoOrdenListaDto>>(url) ?? new();
    }

    public async Task<PedidoOrdenDto?> ObtenerAsync(int id)
        => await _http.GetFromJsonAsync<PedidoOrdenDto>($"api/pedidos-ordenes/{id}");

    public async Task<ArticuloDragonDto> ResolverArticuloAsync(string artCod)
        => await _http.GetFromJsonAsync<ArticuloDragonDto>($"api/pedidos-ordenes/resolver-articulo?artcod={Uri.EscapeDataString(artCod)}") ?? new() { ArtCod = artCod };

    public async Task<List<string>> EstadosAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/pedidos-ordenes/estados") ?? new();

    public async Task<List<EquivalenciaTalleDto>> EquivalenciasTallesAsync()
        => await _http.GetFromJsonAsync<List<EquivalenciaTalleDto>>("api/pedidos-ordenes/equivalencias-talles") ?? new();

    public async Task<List<ProveedorOrdenDto>> ProveedoresAsync()
        => await _http.GetFromJsonAsync<List<ProveedorOrdenDto>>("api/pedidos-ordenes/proveedores") ?? new();

    public async Task<(bool Ok, string? Error)> CrearAsync(PedidoOrdenSaveRequest req)
        => await Leer(await _http.PostAsJsonAsync("api/pedidos-ordenes", req));

    public async Task<(bool Ok, string? Error)> ModificarAsync(int id, PedidoOrdenSaveRequest req)
        => await Leer(await _http.PutAsJsonAsync($"api/pedidos-ordenes/{id}", req));

    public async Task<(bool Ok, string? Error)> EliminarAsync(int id)
        => await Leer(await _http.DeleteAsync($"api/pedidos-ordenes/{id}"));

    private static async Task<(bool, string?)> Leer(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return (true, null);
        try { var e = await resp.Content.ReadFromJsonAsync<Err>(); return (false, e?.Mensaje ?? "No se pudo completar la operación."); }
        catch { return (false, "No se pudo completar la operación."); }
    }
    private sealed class Err { public string? Mensaje { get; set; } }
}
