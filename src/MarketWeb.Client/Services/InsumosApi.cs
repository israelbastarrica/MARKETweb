using System.Net.Http.Json;
using MarketWeb.Shared.Insumos;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del feature Insumos (pedidos por local).</summary>
public sealed class InsumosApi
{
    private readonly HttpClient _http;

    public InsumosApi(HttpClient http) => _http = http;

    public async Task<List<UbicacionDto>> ListarUbicacionesAsync()
        => await _http.GetFromJsonAsync<List<UbicacionDto>>("api/insumos/ubicaciones") ?? new();

    public async Task<List<PedidoInsumoDto>> ListarPedidosAsync(int? ubicacionId, string estado)
    {
        var url = $"api/insumos/pedidos?estado={Uri.EscapeDataString(estado)}";
        if (ubicacionId is not null) url += $"&ubicacionId={ubicacionId.Value}";
        return await _http.GetFromJsonAsync<List<PedidoInsumoDto>>(url) ?? new();
    }

    public async Task<List<InsumoConsumoDto>> ListarConsumosAsync(int? ubicacionId, string estado)
    {
        var url = $"api/insumos/consumos?estado={Uri.EscapeDataString(estado)}";
        if (ubicacionId is not null) url += $"&ubicacionId={ubicacionId.Value}";
        return await _http.GetFromJsonAsync<List<InsumoConsumoDto>>(url) ?? new();
    }

    /// <summary>Marca consumos como procesados. Devuelve la cantidad insertada.</summary>
    public async Task<int> MarcarAsync(List<string> refs)
    {
        var resp = await _http.PostAsJsonAsync("api/insumos/marcar", new MarcarInsumosRequest { Refs = refs });
        resp.EnsureSuccessStatusCode();
        var r = await resp.Content.ReadFromJsonAsync<MarcarResultado>();
        return r?.Marcados ?? 0;
    }

    private sealed record MarcarResultado(int Marcados);

    // ---- ABM de pedidos (LOCALES) ----

    public async Task<ValidacionFechaDto> ValidarFechaAsync(int idLocal)
        => await _http.GetFromJsonAsync<ValidacionFechaDto>($"api/insumos/pedido/validar-fecha?idLocal={idLocal}") ?? new();

    public async Task<PedidoEditorDto?> ObtenerPedidoAsync(int id)
        => await _http.GetFromJsonAsync<PedidoEditorDto>($"api/insumos/pedido/{id}");

    public async Task<List<ArticuloInsumoDto>> BuscarArticulosInsumoAsync(string q)
        => await _http.GetFromJsonAsync<List<ArticuloInsumoDto>>($"api/insumos/articulos-insumo?q={Uri.EscapeDataString(q)}") ?? new();

    /// <summary>Guarda el pedido completo (cabecera + renglones). Devuelve (id, nro) o lanza con el mensaje del server.</summary>
    public async Task<CrearPedidoResultado> GuardarPedidoAsync(GuardarPedidoRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/insumos/pedido/guardar", req);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<CrearPedidoResultado>())!;
    }

    /// <summary>Elimina el pedido. Devuelve true si se borró; false si está enviado (no permitido).</summary>
    public async Task<bool> EliminarPedidoAsync(int idPedido)
    {
        var resp = await _http.DeleteAsync($"api/insumos/pedido/{idPedido}");
        return resp.IsSuccessStatusCode;
    }

    // ---- DEPÓSITO / LOGÍSTICA ----

    /// <summary>Imprime el armado: marca EN ARMADO los pendientes del filtro y devuelve su detalle.</summary>
    public async Task<ArmadoInsumosDto> ImprimirArmadoAsync(int? ubicacionId)
    {
        var resp = await _http.PostAsJsonAsync("api/insumos/pedido/imprimir-armado", new { ubicacionId });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ArmadoInsumosDto>()) ?? new();
    }

    /// <summary>Genera los remitos de insumos (uno por local) en Dragonfish y marca los pedidos como ENVIADOS.</summary>
    public async Task<GenerarRemitosResultado> GenerarRemitosAsync(int? ubicacionId)
    {
        var resp = await _http.PostAsJsonAsync("api/insumos/generar-remitos", new { ubicacionId });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<GenerarRemitosResultado>()) ?? new();
    }
}
