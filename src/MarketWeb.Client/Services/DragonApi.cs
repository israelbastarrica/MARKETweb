using System.Net.Http.Json;
using MarketWeb.Shared.Dragonfish;

namespace MarketWeb.Client.Services;

/// <summary>Cliente para probar la API Dragonfish (crear remito de insumos).</summary>
public sealed class DragonApi
{
    private readonly HttpClient _http;
    public DragonApi(HttpClient http) => _http = http;

    public async Task<bool> ConfiguradoAsync()
    {
        try
        {
            var r = await _http.GetFromJsonAsync<EstadoResp>("api/dragon/estado");
            return r?.Configurado ?? false;
        }
        catch { return false; }
    }

    public async Task<DragonRemitoResultDto> RemitoPruebaAsync(DragonRemitoRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/dragon/remito-prueba", req);
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<DragonRemitoResultDto>() ?? new() { Ok = false, Error = "Sin respuesta." };
        return new DragonRemitoResultDto { Ok = false, Error = $"HTTP {(int)resp.StatusCode}" };
    }

    // --- Alta real de remito desde la tablet de Logística (endpoint api/remitos, policy Aprobado) ---
    public async Task<bool> ConfiguradoRemitoAsync()
    {
        try
        {
            var r = await _http.GetFromJsonAsync<EstadoResp>("api/remitos/estado");
            return r?.Configurado ?? false;
        }
        catch { return false; }
    }

    public async Task<DragonRemitoResultDto> CrearRemitoAsync(DragonRemitoRequest req)
    {
        var resp = await _http.PostAsJsonAsync("api/remitos", req);
        if (resp.IsSuccessStatusCode)
            return await resp.Content.ReadFromJsonAsync<DragonRemitoResultDto>() ?? new() { Ok = false, Error = "Sin respuesta." };
        return new DragonRemitoResultDto { Ok = false, Error = $"HTTP {(int)resp.StatusCode}" };
    }

    public async Task<List<UltimaRepoItemDto>> UltimaRepoAsync(string local)
    {
        try { return await _http.GetFromJsonAsync<List<UltimaRepoItemDto>>($"api/remitos/ultima-repo?local={Uri.EscapeDataString(local)}") ?? new(); }
        catch { return new(); }
    }

    public async Task<ArticuloLookupDto?> BuscarArticuloAsync(string cod)
    {
        try
        {
            var r = await _http.GetAsync($"api/remitos/articulo/{Uri.EscapeDataString(cod)}");
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (r.IsSuccessStatusCode) return await r.Content.ReadFromJsonAsync<ArticuloLookupDto>();
            return null;
        }
        catch { return null; }
    }

    // Busca una bolsa del depósito por su código de barras (NroBolsa) y trae su detalle.
    public async Task<BolsaDto?> BuscarBolsaAsync(string nroBolsa)
    {
        try
        {
            var r = await _http.GetAsync($"api/remitos/bolsa/{Uri.EscapeDataString(nroBolsa)}");
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            if (r.IsSuccessStatusCode) return await r.Content.ReadFromJsonAsync<BolsaDto>();
            return null;
        }
        catch { return null; }
    }

    // El QR de la bolsa es el CODIGO del remito: lo busca en COMPROBANTEV de las 3 bases (avisa si está anulado).
    public async Task<RemitoPorCodigoDto> BuscarRemitoPorCodigoAsync(string codigo)
    {
        try
        {
            return await _http.GetFromJsonAsync<RemitoPorCodigoDto>(
                $"api/remitos/remito-codigo?codigo={Uri.EscapeDataString(codigo)}") ?? new();
        }
        catch { return new(); }
    }

    // Motivos de remito (excluye Insumos/13).
    public async Task<List<MotivoDto>> MotivosAsync()
    {
        try { return await _http.GetFromJsonAsync<List<MotivoDto>>("api/remitos/motivos") ?? new(); }
        catch { return new(); }
    }

    // Levanta el detalle de un remito existente de un local (LURO/PERALTA) por Punto-Número.
    public async Task<List<BolsaRenglonDto>> BuscarRemitoLocalAsync(string local, int punto, int numero)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<BolsaRenglonDto>>(
                $"api/remitos/remito-local?local={Uri.EscapeDataString(local)}&punto={punto}&numero={numero}") ?? new();
        }
        catch { return new(); }
    }

    private sealed class EstadoResp { public bool Configurado { get; set; } }
}
