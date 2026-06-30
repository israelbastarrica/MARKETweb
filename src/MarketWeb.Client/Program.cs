using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MarketWeb.Client;
using MarketWeb.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Identidad del equipo físico (por navegador) + header X-Pc en cada request.
builder.Services.AddSingleton<DevicePcState>();

// Misma URL base que la API que hostea esta SPA. El handler agrega el header X-Pc.
builder.Services.AddScoped(sp =>
{
    var handler = new DevicePcHandler(sp.GetRequiredService<DevicePcState>())
    {
        InnerHandler = new HttpClientHandler()
    };
    return new HttpClient(handler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
});

// --- Clientes de API por feature ---
builder.Services.AddScoped<LocalesApi>();
builder.Services.AddScoped<TiposLocalApi>();
builder.Services.AddScoped<UsuariosPcApi>();
builder.Services.AddScoped<InsumosApi>();
builder.Services.AddScoped<CostosApi>();
builder.Services.AddScoped<VentasApi>();
builder.Services.AddScoped<ArticulosApi>();
builder.Services.AddScoped<MapeoApi>();
builder.Services.AddScoped<ConfigImagenesApi>();
builder.Services.AddScoped<PacksApi>();
builder.Services.AddScoped<TelasApi>();
builder.Services.AddScoped<DespachosApi>();
builder.Services.AddScoped<RemitoImpresionApi>();
builder.Services.AddScoped<PaletsApi>();
builder.Services.AddScoped<LogisticaDashboardApi>();
builder.Services.AddScoped<DashboardApi>();
builder.Services.AddScoped<UsoApi>();
builder.Services.AddScoped<ReemplazosApi>();
builder.Services.AddScoped<ReposicionApi>();
builder.Services.AddScoped<EventosApi>();
builder.Services.AddScoped<ReseteadosApi>();
builder.Services.AddScoped<ControlRemitosApi>();
builder.Services.AddScoped<ReporteArticulosApi>();
builder.Services.AddScoped<TareasApi>();
builder.Services.AddScoped<DragonApi>();
builder.Services.AddScoped<MapaApi>();
builder.Services.AddScoped<InformesApi>();
builder.Services.AddSingleton<PageStateService>();
builder.Services.AddSingleton<UiService>();
builder.Services.AddSingleton<LayoutState>();

// --- Autenticación (estado en el cliente; identidad real en cookie del server) ---
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, HostAuthStateProvider>();
builder.Services.AddScoped<AuthApi>();

await builder.Build().RunAsync();
