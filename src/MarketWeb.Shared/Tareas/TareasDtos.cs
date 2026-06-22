namespace MarketWeb.Shared.Tareas;

/// <summary>Tipos de tarea soportados por el programador. Por ahora solo Reposición.</summary>
public static class TipoTarea
{
    public const string Reposicion = "REPOSICION";
}

/// <summary>Una tarea programada (fila del listado en SISTEMAS → Tareas).</summary>
public sealed class TareaProgramadaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Tipo { get; set; } = TipoTarea.Reposicion;
    public string Hora { get; set; } = "22:00";        // HH:mm
    public string DiasSemana { get; set; } = "1,2,3,4,5,6,7";  // ISO: 1=lunes .. 7=domingo
    public bool Activa { get; set; } = true;
    public DateTime? UltimaEjecucion { get; set; }
    public bool? UltimoOk { get; set; }
    public string? UltimoResultado { get; set; }
}

/// <summary>Editor de una tarea (incluye los parámetros tipados de Reposición).</summary>
public sealed class TareaProgramadaEditorDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Tipo { get; set; } = TipoTarea.Reposicion;
    public string Hora { get; set; } = "22:00";
    public string DiasSemana { get; set; } = "1,2,3,4,5,6,7";
    public bool Activa { get; set; } = true;
    // Parámetros de Reposición:
    public string Local { get; set; } = "TODOS";
    public bool GenerarReemplazos { get; set; } = true;
    public string Destinatarios { get; set; } = "";    // separados por ; o ,
}

/// <summary>Alta/modificación de una tarea.</summary>
public sealed class TareaSaveRequest
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Tipo { get; set; } = TipoTarea.Reposicion;
    public string Hora { get; set; } = "22:00";
    public string DiasSemana { get; set; } = "1,2,3,4,5,6,7";
    public bool Activa { get; set; } = true;
    public string Local { get; set; } = "TODOS";
    public bool GenerarReemplazos { get; set; } = true;
    public string Destinatarios { get; set; } = "";
}

/// <summary>Una corrida registrada de una tarea (historial).</summary>
public sealed class TareaLogDto
{
    public int Id { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime? Fin { get; set; }
    public bool Ok { get; set; }
    public string Origen { get; set; } = "";           // AUTO / MANUAL
    public string? Resultado { get; set; }
}

/// <summary>Parámetros serializados (JSON en la columna Parametros) de una tarea de Reposición.</summary>
public sealed class ParametrosReposicion
{
    public string Local { get; set; } = "TODOS";
    public bool GenerarReemplazos { get; set; } = true;
    public string Destinatarios { get; set; } = "";
}
