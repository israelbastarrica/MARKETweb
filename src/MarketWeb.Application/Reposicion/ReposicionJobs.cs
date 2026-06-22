using System.Collections.Concurrent;
using MarketWeb.Shared.Reposicion;
using Microsoft.Extensions.DependencyInjection;

namespace MarketWeb.Application.Reposicion;

/// <summary>
/// Corre la reposición (~2 min) en background y la expone por polling. El POST arranca el job
/// y devuelve un id; el cliente consulta el estado hasta que pasa a "done"/"error". Evita
/// depender de un HTTP de 2 minutos (timeouts de Caddy/Kestrel/navegador).
/// Singleton: usa IServiceScopeFactory para resolver un IReposicionService scoped por corrida.
/// </summary>
public sealed class ReposicionJobs
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ConcurrentDictionary<string, Job> _jobs = new();

    public ReposicionJobs(IServiceScopeFactory scopes) => _scopes = scopes;

    public string Start(ReposicionCalcularRequest req, string machineName)
    {
        Purgar();

        var id = Guid.NewGuid().ToString("N");
        var job = new Job();
        _jobs[id] = job;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IReposicionService>();
                job.Resultado = await svc.CalcularAsync(req, machineName, CancellationToken.None);
                job.Estado = "done";
            }
            catch (Exception ex)
            {
                job.Error = ex.Message;
                job.Estado = "error";
            }
        });

        return id;
    }

    public ReposicionJobDto? Get(string id)
    {
        if (!_jobs.TryGetValue(id, out var job)) return null;
        return new ReposicionJobDto
        {
            JobId = id,
            Estado = job.Estado,
            Error = job.Error,
            Resultado = job.Resultado
        };
    }

    // Saca jobs terminados con más de 1 hora para no crecer sin límite.
    private void Purgar()
    {
        var corte = DateTime.Now.AddHours(-1);
        foreach (var kv in _jobs)
            if (kv.Value.Estado != "running" && kv.Value.Creado < corte)
                _jobs.TryRemove(kv.Key, out _);
    }

    private sealed class Job
    {
        public DateTime Creado { get; } = DateTime.Now;
        public volatile string Estado = "running";
        public string? Error;
        public ReposicionResultadoDto? Resultado;
    }
}
