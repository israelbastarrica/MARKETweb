using System.Net.Http.Json;
using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Client.Services;

/// <summary>Cliente de Control de Remitos.</summary>
public sealed class ControlRemitosApi
{
    private readonly HttpClient _http;
    public ControlRemitosApi(HttpClient http) => _http = http;

    public async Task<List<ControlEstadoDto>> EstadoAsync(DateTime desde, DateTime hasta)
        => await _http.GetFromJsonAsync<List<ControlEstadoDto>>(
            $"api/control-remitos/estado?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}") ?? new();

    public async Task<List<RemitoControlDto>> ListadoAsync(DateTime desde, DateTime hasta, int idDestino, string estado)
        => await _http.GetFromJsonAsync<List<RemitoControlDto>>(
            $"api/control-remitos/listado?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}&idDestino={idDestino}&estado={Uri.EscapeDataString(estado)}") ?? new();

    public async Task<List<EventoItemDto>> ContenidoAsync(string remitoId, string origen)
        => await _http.GetFromJsonAsync<List<EventoItemDto>>(
            $"api/control-remitos/contenido?remitoId={Uri.EscapeDataString(remitoId)}&origen={Uri.EscapeDataString(origen)}") ?? new();

    public async Task<bool> ReasignarAsync(int despachoId, int nuevoIdDestino)
        => (await _http.PostAsJsonAsync("api/control-remitos/reasignar", new { despachoId, nuevoIdDestino })).IsSuccessStatusCode;

    public async Task<bool> EliminarDespachoAsync(int despachoId)
        => (await _http.PostAsync($"api/control-remitos/eliminar-despacho/{despachoId}", null)).IsSuccessStatusCode;

    public async Task<List<QrLogDto>> LogQrAsync(DateTime desde, DateTime hasta)
        => await _http.GetFromJsonAsync<List<QrLogDto>>(
            $"api/control-remitos/log-qr?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}") ?? new();

    public async Task<QrFotoInfoDto?> QrFotoInfoAsync(string remitoId, int idLocal)
        => await _http.GetFromJsonAsync<QrFotoInfoDto>(
            $"api/control-remitos/qr-foto-info?remitoId={Uri.EscapeDataString(remitoId)}&idLocal={idLocal}");
}
