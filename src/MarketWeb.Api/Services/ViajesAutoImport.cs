using MarketWeb.Application.Produccion;

namespace MarketWeb.Api.Services;

/// <summary>
/// Importa el viaje (.db de ViajePedidos) una sola vez, automáticamente, al arrancar la app:
/// si ProdViajes está vacío y el .db existe en la ruta configurada, lo espeja a MARKET.
/// Idempotente: una vez que hay datos, no vuelve a correr. Sin botón ni intervención.
/// </summary>
public sealed class ViajesAutoImport : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ViajesAutoImport> _log;

    public ViajesAutoImport(IServiceScopeFactory scopes, ILogger<ViajesAutoImport> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Pequeña espera para que termine de levantar la app.
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

            using var scope = _scopes.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IViajesService>();

            var viajes = await svc.ListarViajesAsync(stoppingToken);
            if (viajes.Count > 0)
            {
                _log.LogInformation("ViajesAutoImport: ya hay {n} viaje(s), no se importa.", viajes.Count);
                return;
            }

            var r = await svc.ImportarAsync("AUTO-IMPORT", stoppingToken);
            if (r.Ok) _log.LogInformation("ViajesAutoImport OK: {msg}", r.Mensaje);
            else _log.LogWarning("ViajesAutoImport no importó: {msg}", r.Mensaje);
        }
        catch (OperationCanceledException) { /* app cerrando */ }
        catch (Exception ex)
        {
            _log.LogError(ex, "ViajesAutoImport falló (se reintenta en el próximo arranque).");
        }
    }
}
