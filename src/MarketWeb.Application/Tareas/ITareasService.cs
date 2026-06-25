using MarketWeb.Shared.Tareas;

namespace MarketWeb.Application.Tareas;

public interface ITareasService
{
    /// <summary>Crea las tablas (TareasProgramadas / TareasProgramadasLog) si no existen. Idempotente.</summary>
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TareaProgramadaDto>> ListarAsync(CancellationToken ct = default);
    Task<TareaProgramadaEditorDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<int> GuardarAsync(TareaSaveRequest req, string usuario, CancellationToken ct = default);
    Task EliminarAsync(int id, string usuario, CancellationToken ct = default);

    /// <summary>Tareas activas que corresponde disparar ahora (día + hora cumplida + no corridas hoy).</summary>
    Task<IReadOnlyList<int>> PendientesAsync(DateTime ahora, CancellationToken ct = default);

    /// <summary>Historial de corridas de una tarea (más recientes primero).</summary>
    Task<IReadOnlyList<TareaLogDto>> HistorialAsync(int idTarea, int top = 20, CancellationToken ct = default);

    /// <summary>Ejecuta una tarea (dispatch por tipo). origen: AUTO (scheduler) / MANUAL (botón). Registra log + estado.</summary>
    Task EjecutarAsync(int id, string origen, CancellationToken ct = default);

    /// <summary>Reenvía solo el PDF + mail de la última corrida guardada (sin correr el SP). Para tareas de Reposición.</summary>
    Task<(bool Ok, string Resultado)> ReenviarReposicionAsync(int id, CancellationToken ct = default);
}
