using System.Net.Http.Json;
using MarketWeb.Shared.Telas;

namespace MarketWeb.Client.Services;

/// <summary>Cliente HTTP de Telas (stock por rollo + tablero). Habla con TelasController.</summary>
public sealed class TelasApi
{
    private readonly HttpClient _http;
    public TelasApi(HttpClient http) => _http = http;

    // Catálogos
    public async Task<List<CatalogoItemDto>> CatalogoAsync(string tipo)
        => await _http.GetFromJsonAsync<List<CatalogoItemDto>>($"api/telas/catalogos/{tipo}") ?? new();

    public async Task<(bool Ok, string? Error, int Id)> CrearCatalogoAsync(string tipo, string? codigo, string nombre)
    {
        var resp = await _http.PostAsJsonAsync($"api/telas/catalogos/{tipo}", new CatalogoSaveRequest { Codigo = codigo, Nombre = nombre });
        if (resp.IsSuccessStatusCode)
        {
            var creado = await resp.Content.ReadFromJsonAsync<CreatedId>();
            return (true, null, creado?.Id ?? 0);
        }
        try { var e = await resp.Content.ReadFromJsonAsync<Err>(); return (false, e?.Mensaje ?? "No se pudo agregar.", 0); }
        catch { return (false, "No se pudo agregar.", 0); }
    }
    private sealed class CreatedId { public int Id { get; set; } }

    // Tablero
    public async Task<List<DepoStockDto>> StockDepositosAsync()
        => await _http.GetFromJsonAsync<List<DepoStockDto>>("api/telas/stock-depositos") ?? new();

    public async Task<List<PedidoBarraDto>> ResumenPedidosAsync(int? idDeposito, int top = 20)
    {
        var url = $"api/telas/resumen-pedidos?top={top}";
        if (idDeposito is > 0) url += $"&idDeposito={idDeposito}";
        return await _http.GetFromJsonAsync<List<PedidoBarraDto>>(url) ?? new();
    }

    public async Task<List<DepoMaterialDto>> MaterialesDepositoAsync(int idDeposito)
        => await _http.GetFromJsonAsync<List<DepoMaterialDto>>($"api/telas/deposito/{idDeposito}/materiales") ?? new();

    public async Task<List<ColorStockDto>> ColoresStockAsync(int idDeposito, int idMaterial)
        => await _http.GetFromJsonAsync<List<ColorStockDto>>($"api/telas/deposito/{idDeposito}/material/{idMaterial}/colores") ?? new();

    // ABM rollos
    public async Task<List<TelaRolloDto>> RollosAsync(int? idDeposito, int? idMaterial, int? idColor, int? idTelera, string? numPedido)
    {
        var qs = new List<string>();
        if (idDeposito is > 0) qs.Add($"idDeposito={idDeposito}");
        if (idMaterial is > 0) qs.Add($"idMaterial={idMaterial}");
        if (idColor == -1) qs.Add("sinColor=true");            // fuera de carta (sin color de paleta)
        else if (idColor is > 0) qs.Add($"idColor={idColor}");
        if (idTelera is > 0) qs.Add($"idTelera={idTelera}");
        if (!string.IsNullOrWhiteSpace(numPedido)) qs.Add($"numPedido={Uri.EscapeDataString(numPedido)}");
        var url = "api/telas/rollos" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await _http.GetFromJsonAsync<List<TelaRolloDto>>(url) ?? new();
    }

    public async Task<(bool Ok, string? Error)> CrearRolloAsync(RolloSaveRequest req)
        => await Leer(await _http.PostAsJsonAsync("api/telas/rollos", req));

    public async Task<(bool Ok, string? Error)> CrearLoteRollosAsync(RolloSaveRequest req)
        => await Leer(await _http.PostAsJsonAsync("api/telas/rollos/lote", req));

    public async Task<(bool Ok, string? Error)> ModificarRolloAsync(int id, RolloSaveRequest req)
        => await Leer(await _http.PutAsJsonAsync($"api/telas/rollos/{id}", req));

    public async Task EliminarRolloAsync(int id)
        => await _http.DeleteAsync($"api/telas/rollos/{id}");

    private static async Task<(bool, string?)> Leer(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return (true, null);
        try { var e = await resp.Content.ReadFromJsonAsync<Err>(); return (false, e?.Mensaje ?? "No se pudo completar la operación."); }
        catch { return (false, "No se pudo completar la operación."); }
    }
    private sealed class Err { public string? Mensaje { get; set; } }
}
