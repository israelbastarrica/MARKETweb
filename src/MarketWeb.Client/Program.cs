using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MarketWeb.Client;
using MarketWeb.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Misma URL base que la API que hostea esta SPA.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// --- Clientes de API por feature ---
builder.Services.AddScoped<LocalesApi>();
builder.Services.AddScoped<TiposLocalApi>();
builder.Services.AddScoped<UsuariosPcApi>();
builder.Services.AddScoped<InsumosApi>();
builder.Services.AddScoped<CostosApi>();
builder.Services.AddScoped<VentasApi>();
builder.Services.AddScoped<ArticulosApi>();
builder.Services.AddScoped<MapeoApi>();
builder.Services.AddScoped<DespachosApi>();
builder.Services.AddScoped<RemitoImpresionApi>();
builder.Services.AddScoped<PaletsApi>();
builder.Services.AddScoped<LogisticaDashboardApi>();
builder.Services.AddScoped<DashboardApi>();
builder.Services.AddScoped<UsoApi>();
builder.Services.AddScoped<ReemplazosApi>();
builder.Services.AddSingleton<PageStateService>();

// --- Autenticación (estado en el cliente; identidad real en cookie del server) ---
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, HostAuthStateProvider>();
builder.Services.AddScoped<AuthApi>();

await builder.Build().RunAsync();
