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

    /// <summary>Guarda la acción decidida por el encargado (EventosReposicion.Accion). Vacío = NULL.</summary>
    Task GuardarAccionAsync(int id, string accion, CancellationToken ct = default);

    /// <summary>Soft-delete (Eliminado=1) — para cargas erradas, distinto de procesar.</summary>
    Task EliminarAsync(int id, CancellationToken ct = default);

    /// <summary>Catálogo de motivos normalizados (activos) para clasificar la causa del evento.</summary>
    Task<IReadOnlyList<MotivoEventoDto>> ListarMotivosAsync(CancellationToken ct = default);

    /// <summary>Alta de un motivo en el catálogo (idempotente por nombre). Devuelve el motivo (nuevo o existente).</summary>
    Task<MotivoEventoDto> CrearMotivoAsync(string nombre, string usuario, CancellationToken ct = default);

    /// <summary>Asigna (o limpia con 0) el motivo normalizado del evento (EventosReposicion.IDMotivoEvento).</summary>
    Task GuardarMotivoAsync(int idEvento, int idMotivo, CancellationToken ct = default);

    /// <summary>Reporte de motivos (Sistemas): dona con % por motivo + registro de eventos clasificados.</summary>
    Task<MotivosReporteDto> MotivosReporteAsync(DateTime desde, DateTime hasta, string local, CancellationToken ct = default);
}
