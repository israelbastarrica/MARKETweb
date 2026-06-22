using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Application.Reposicion;

public interface IEventosService
{
    /// <summary>Listado de eventos. local "TODOS"/"" = sin filtro; verTodos incluye procesados (nunca eliminados).</summary>
    Task<IReadOnlyList<EventoDto>> ListarAsync(string local, DateTime desde, DateTime hasta, bool verTodos, CancellationToken ct = default);

    /// <summary>Detalle de un evento + items del remito asociado (si hay).</summary>
    Task<EventoDetalleDto?> DetalleAsync(int id, CancellationToken ct = default);

    /// <summary>Foto adjunta del evento (VARBINARY). null si no tiene.</summary>
    Task<byte[]?> FotoAsync(int id, CancellationToken ct = default);

    /// <summary>Marca el evento como atendido (Procesado=1). NO es baja.</summary>
    Task MarcarProcesadoAsync(int id, CancellationToken ct = default);

    /// <summary>Soft-delete (Eliminado=1) — para cargas erradas, distinto de procesar.</summary>
    Task EliminarAsync(int id, CancellationToken ct = default);
}
