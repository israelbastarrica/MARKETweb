using System.Net.Http.Json;
using MarketWeb.Shared.LogisticaDashboard;

namespace MarketWeb.Client.Services;

/// <summary>
/// Cliente de los paneles del dashboard de Logística. Lo usa la vista mobile
/// (LogisticaDashboardMobile); en escritorio el dashboard sigue siendo el HTML de TV.
/// </summary>
public sealed class LogisticaDashboardApi
{
    private readonly HttpClient _http;
    public LogisticaDashboardApi(HttpClient http) => _http = http;

    public Task<PanelDespachoRecepcionDto?> Panel1() => _http.GetFromJsonAsync<PanelDespachoRecepcionDto>("api/logistica-dashboard/panel1");
    public Task<PanelPendientesDto?> Panel2() => _http.GetFromJsonAsync<PanelPendientesDto>("api/logistica-dashboard/panel2");
    public Task<PanelMapeosDto?> Panel3() => _http.GetFromJsonAsync<PanelMapeosDto>("api/logistica-dashboard/panel3");
    public Task<PanelVaciasDto?> Panel4() => _http.GetFromJsonAsync<PanelVaciasDto>("api/logistica-dashboard/panel4");
    public Task<PanelEstancadosDto?> Panel5() => _http.GetFromJsonAsync<PanelEstancadosDto>("api/logistica-dashboard/panel5");
    public Task<PanelPickingDto?> Panel6() => _http.GetFromJsonAsync<PanelPickingDto>("api/logistica-dashboard/panel6");
    public Task<PanelReposicionDto?> Panel8() => _http.GetFromJsonAsync<PanelReposicionDto>("api/logistica-dashboard/panel8");
}
