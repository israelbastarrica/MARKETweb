using MarketWeb.Application.Articulos;
using MarketWeb.Application.ConfigImagenes;
using MarketWeb.Application.Costos;
using MarketWeb.Application.Dashboard;
using MarketWeb.Application.Data;
using MarketWeb.Application.Despachos;
using MarketWeb.Application.Insumos;
using MarketWeb.Application.Locales;
using MarketWeb.Application.LogisticaDashboard;
using MarketWeb.Application.Mapa;
using MarketWeb.Application.Mapeo;
using MarketWeb.Application.Packs;
using MarketWeb.Application.Palets;
using MarketWeb.Application.Reemplazos;
using MarketWeb.Application.RemitoImpresion;
using MarketWeb.Application.Informes;
using MarketWeb.Application.Remitos;
using MarketWeb.Application.Reposicion;
using MarketWeb.Application.Tareas;
using MarketWeb.Application.TiposLocal;
using MarketWeb.Application.Uso;
using MarketWeb.Application.UsuariosPc;
using MarketWeb.Application.Ventas;
using Microsoft.Extensions.DependencyInjection;

namespace MarketWeb.Application;

/// <summary>
/// Registro de la capa de aplicación. Cada feature nuevo agrega aquí su servicio.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddMarketApplication(this IServiceCollection services)
    {
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

        // --- Features ---
        services.AddScoped<ILocalesService, LocalesService>();
        services.AddScoped<ITiposLocalService, TiposLocalService>();
        services.AddScoped<IUsuariosPcService, UsuariosPcService>();
        services.AddScoped<IInsumosService, InsumosService>();
        services.AddScoped<ICostosService, CostosService>();
        services.AddScoped<IVentasService, VentasService>();
        services.AddScoped<IArticulosService, ArticulosService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IMapeoService, MapeoService>();
        services.AddScoped<IMapaService, MapaService>();
        services.AddScoped<IConfigImagenesService, ConfigImagenesService>();
        services.AddScoped<IDespachosService, DespachosService>();
        services.AddScoped<IRemitoImpresionService, RemitoImpresionService>();
        services.AddScoped<IPaletsService, PaletsService>();
        services.AddScoped<IPacksService, PacksService>();
        services.AddSingleton<EstancadosCache>();
        services.AddSingleton<BackgroundCache<List<MarketWeb.Shared.LogisticaDashboard.ArticuloUbicacionesDto>>>();
        services.AddSingleton(new BackgroundCache<MarketWeb.Application.LogisticaDashboard.ReposFast> { Ttl = TimeSpan.FromSeconds(60) });
        services.AddSingleton(new BackgroundCache<Dictionary<string, MarketWeb.Shared.LogisticaDashboard.RepoAbastDto>> { Ttl = TimeSpan.FromHours(1) });
        services.AddScoped<ILogisticaDashboardService, LogisticaDashboardService>();
        services.AddScoped<IUsoService, UsoService>();
        services.AddScoped<IReemplazosService, ReemplazosService>();
        services.AddScoped<IRemitosLookupService, RemitosLookupService>();
        services.AddScoped<IInformesService, InformesService>();
        services.AddScoped<IReposicionService, ReposicionService>();
        services.AddScoped<IReposicionPdf, ReposicionPdf>();
        services.AddScoped<IEventosService, EventosService>();
        services.AddScoped<IReseteadosService, ReseteadosService>();
        services.AddScoped<IControlRemitosService, ControlRemitosService>();
        services.AddScoped<IReporteArticulosService, ReporteArticulosService>();
        services.AddSingleton<ReposicionJobs>();
        services.AddScoped<ITareasService, TareasService>();
        services.AddSingleton<TareasRunner>();
        services.AddScoped<Common.ISmtpSender, Common.SmtpSender>();

        return services;
    }
}
