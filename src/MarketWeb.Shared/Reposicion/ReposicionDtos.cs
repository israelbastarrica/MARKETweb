namespace MarketWeb.Shared.Reposicion;

/// <summary>Pedido de cálculo de reposición (dispara SP_RepoCalcularPacks). Espejo de los filtros de frmRepoReposicion.</summary>
public sealed class ReposicionCalcularRequest
{
    public string Local { get; set; } = "TODOS";          // TODOS / LURO / PERALTA
    public DateTime? FechaCorte { get; set; }              // null = HOY (repo real); fecha pasada = SIMULACIÓN (corte 21:00)
    public bool GenerarReemplazos { get; set; } = true;    // persiste huérfanos en RepoReemplazos
}

/// <summary>Una fila del resultado de SP_RepoCalcularPacks (reposición o huérfano para reemplazo).</summary>
public sealed class ReposicionFilaDto
{
    public string LocalDestino { get; set; } = "";
    public string UbicacionDeposito { get; set; } = "";   // módulo de picking en depósito (ej. "A01-1")
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public int CantPack { get; set; }
    public int Pendiente { get; set; }
    public int Packs { get; set; }                          // packs a reponer (la columna fuerte)
    public bool EsVirtual { get; set; }
    public bool EsHuerfano { get; set; }
    public bool NuevoEstaCorrida { get; set; }              // huérfano insertado en RepoReemplazos en esta corrida
    // Últ. remito (componentes + texto ya formateado para la grilla).
    public string UltRemitoNro { get; set; } = "";
    public DateTime UltRemitoFecha { get; set; }
    public string UltRemitoHora { get; set; } = "";
    public string UltRemitoTexto { get; set; } = "";
    // Ocultas: solo para el PDF agrupado (Fase 2).
    public string TipoArt { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Combo { get; set; } = "";
    public string Mobiliario { get; set; } = "";
    public string UbicacionesLocal { get; set; } = "";
}

/// <summary>Resultado completo de una corrida (filas + totales del footer).</summary>
public sealed class ReposicionResultadoDto
{
    public List<ReposicionFilaDto> Filas { get; set; } = new();
    public int TotalArticulos { get; set; }
    public int TotalPacks { get; set; }
    public int TotalPrendas { get; set; }
}

/// <summary>Una corrida guardada (MARKET.dbo.Reposicion) para el historial / reimpresión.</summary>
public sealed class CorridaDto
{
    public int Id { get; set; }
    public DateTime FechaHoraCorrida { get; set; }
    public string LocalParam { get; set; } = "";
    public int TotalArticulos { get; set; }
    public int TotalPacks { get; set; }
    public int TotalPrendas { get; set; }
    public string MachineName { get; set; } = "";
}

/// <summary>Estado de un job de cálculo (la corrida tarda ~2 min → se ejecuta en background y se consulta por polling).</summary>
public sealed class ReposicionJobDto
{
    public string JobId { get; set; } = "";
    public string Estado { get; set; } = "running";        // running / done / error
    public string? Error { get; set; }
    public ReposicionResultadoDto? Resultado { get; set; }
}
