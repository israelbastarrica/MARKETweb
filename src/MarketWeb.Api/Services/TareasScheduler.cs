using MarketWeb.Application.Tareas;

namespace MarketWeb.Api.Services;

/// <summary>
/// Programador in-app: corre mientras el MarketWeb está activo (hosteado como servicio siempre encendido).
/// Cada minuto busca tareas vencidas (día + hora + no corridas hoy) y las dispara vía TareasRunner.
/// Reemplaza al Programador de Windows + MARKET.exe -AUTOREPO.
/// </summary>
public sealed class TareasScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TareasRunner _runner;
    private readonly ILogger<TareasScheduler> _log;

    public TareasScheduler(IServiceScopeFactory scopes, TareasRunner runner, ILogger<TareasScheduler> log)
    {
        _scopes = scopes;
        _runner = runner;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Crea las tablas si faltan (tabla aditiva, idempotente).
        try
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ITareasService>().EnsureSchemaAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo asegurar el esquema de tareas programadas.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ITareasService>();
                var pendientes = await svc.PendientesAsync(DateTime.Now, stoppingToken);
                foreach (var id in pendientes)
                    _runner.Lanzar(id, "AUTO");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error en el ciclo del programador de tareas.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
