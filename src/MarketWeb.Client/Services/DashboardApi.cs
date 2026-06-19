using System.Net.Http.Json;
using MarketWeb.Shared.Dashboard;

namespace MarketWeb.Client.Services;

/// <summary>Cliente del dashboard de ventas (Locales). Lo usa la vista mobile;
/// en escritorio el dashboard sigue siendo el HTML de TV.</summary>
public sealed class DashboardApi
{
    private readonly HttpClient _http;
    public DashboardApi(HttpClient http) => _http = http;

    public Task<DashboardVentasMobileDto?> ResumenMobile()
        => _http.GetFromJsonAsync<DashboardVentasMobileDto>("api/dashboard/resumen-mobile");
}
