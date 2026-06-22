using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace MarketWeb.Application.Tareas;

/// <summary>
/// Lanza la ejecución de una tarea en background (no bloquea el request ni el loop del scheduler).
/// Garantiza que una misma tarea no corra dos veces en paralelo. Singleton: usa IServiceScopeFactory.
/// </summary>
public sealed class TareasRunner
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ConcurrentDictionary<int, byte> _corriendo = new();

    public TareasRunner(IServiceScopeFactory scopes) => _scopes = scopes;

    public bool EstaCorriendo(int id) => _corriendo.ContainsKey(id);

    /// <summary>Arranca la tarea. Devuelve false si ya estaba corriendo.</summary>
    public bool Lanzar(int id, string origen)
    {
        if (!_corriendo.TryAdd(id, 0)) return false;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ITareasService>();
                await svc.EjecutarAsync(id, origen, CancellationToken.None);
            }
            catch
            {
                // El propio EjecutarAsync registra el error en el log; acá solo evitamos tirar el hilo.
            }
            finally
            {
                _corriendo.TryRemove(id, out _);
            }
        });

        return true;
    }
}
