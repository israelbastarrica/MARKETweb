namespace MarketWeb.Shared.Reposicion;

/// <summary>RS1 de SP_RepoExplicarArticulo: resumen, veredicto y cifras del cálculo.</summary>
public sealed class ExplicarResumenDto
{
    public bool HayDatos { get; set; }
    public string Explicacion { get; set; } = "";
    public string Clasificacion { get; set; } = "";   // REPOSICION / REEMPLAZO...
    public int CantPack { get; set; } = 1;
    public int Venta { get; set; }
    public int FallasRotacion { get; set; }
    public int Ajuste { get; set; }
    public int ReposEnviadas { get; set; }
    public int Pendiente { get; set; }
    public int Packs { get; set; }
    public DateTime? Ancla { get; set; }
    public int UltRemitoCant { get; set; }
    public DateTime? UltRemitoFecha { get; set; }
    public int EventosPendientes { get; set; }
    public int EventosSobrantePacks { get; set; }
    public int EventosFaltantePacks { get; set; }
    public int SobranteAplicadoPacks { get; set; }
    public int SobranteAplicadoUnidades { get; set; }
}

/// <summary>RS2: ubicaciones del artículo (depósito + local).</summary>
public sealed class ExplicarUbicacionDto
{
    public string Ubicacion { get; set; } = "";
    public string Mobiliario { get; set; } = "";
    public string Modulo { get; set; } = "";
    public string Pasillo { get; set; } = "";
    public string Fila { get; set; } = "";
    public string Posicion { get; set; } = "";
    public DateTime? FechaHora { get; set; }
    public bool EsDeposito { get; set; }
}

/// <summary>RS3: un movimiento desde el ancla, con saldo corrido (ya acumulado en el server).</summary>
public sealed class ExplicarMovimientoDto
{
    public DateTime? Fecha { get; set; }
    public string Hora { get; set; } = "";
    public string Remito { get; set; } = "";
    public string Motivo { get; set; } = "";
    public decimal? Cantidad { get; set; }
    public int Orden { get; set; }            // 0 ancla,1 envío,2 rotación,3 falla,4 venta,5 ajuste,6 evento,8/9 resumen
    public int SaldoDelta { get; set; }       // delta de esta fila (0 = no mueve el saldo)
    public int? Saldo { get; set; }           // acumulado (null en filas resumen Orden>=8)
    public string Origen { get; set; } = "";
    public string Tipo { get; set; } = "";
    public int EventoId { get; set; }         // si Orden=6 y el Remito trae "EVT #N"
    public bool EventoTieneFoto { get; set; }
}

/// <summary>Resultado completo del "explain" de un artículo en un local.</summary>
public sealed class ExplicarDto
{
    public string ArtCod { get; set; } = "";
    public string Local { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public ExplicarResumenDto Resumen { get; set; } = new();
    public bool EnPalet { get; set; }   // está en un palet activo del depósito (aunque no tenga ubicación fija)
    public List<ExplicarUbicacionDto> Ubicaciones { get; set; } = new();
    public List<ExplicarMovimientoDto> Movimientos { get; set; } = new();
    public bool SaldoCuadra { get; set; } = true;   // SUM(SaldoDelta) == Pendiente
}
