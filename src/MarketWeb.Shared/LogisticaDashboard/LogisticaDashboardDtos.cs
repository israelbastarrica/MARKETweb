namespace MarketWeb.Shared.LogisticaDashboard;

/// <summary>Despacho CENTRAL → local (cruce RemitosDespachados ⨝ RemitosEscaneados).</summary>
public sealed class DespachoLocalDto
{
    public string Local { get; set; } = "";
    public int Despachados { get; set; }   // "enviados"
    public int Recibidos { get; set; }     // "escaneados"
    public int CircuitoOk { get; set; }
    public int EnTransito { get; set; }    // "pendientes"
    public int SinSalida { get; set; }
    public int Fantasmas { get; set; }     // recibidos − despachados (inconsistencia)
    public int DobleDespacho { get; set; }
    public double PctCircuito { get; set; }
    public int? DemoraMin { get; set; }
    public int Anulados { get; set; }
    public int QrPc { get; set; }
}

/// <summary>Recepción local → CENTRAL vía QR (local imprime con QR, CENTRAL escanea al recibir).</summary>
public sealed class RecepcionLocalDto
{
    public string Local { get; set; } = "";
    public int Despachados { get; set; }   // local emitió con QR
    public int Recibidos { get; set; }     // CENTRAL escaneó el QR
    public int Pendientes { get; set; }
    public double PctLlegada { get; set; }
    public double? DemoraMinProm { get; set; }
}

/// <summary>Un remito pendiente (panel 2).</summary>
public sealed class PendItemDto
{
    public string NroRemito { get; set; } = "";
    public int Minutos { get; set; }
    public string? Contenido { get; set; }   // "ARTCOD · ARTDES" o "REPOSICIÓN"
    public string? Local { get; set; }        // para agrupar (destino/origen/recibido_en)
    public bool Despachado { get; set; }      // sin escanear: tiene despacho registrado
    public string? Traza { get; set; }        // texto auxiliar (cruzado/doble/recepción)
}

/// <summary>Bloque de pendientes agrupado por local (2 columnas).</summary>
public sealed class PendBloqueDto
{
    public List<PendItemDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Luro { get; set; }
    public int Peralta { get; set; }
}

/// <summary>Lista de pendientes a todo el ancho (cruzados / doble despacho).</summary>
public sealed class PendListaDto
{
    public List<PendItemDto> Items { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>Panel 2: Pendientes en tránsito.</summary>
public sealed class PanelPendientesDto
{
    public PendListaDto Cruzados { get; set; } = new();
    public PendListaDto DobleDespacho { get; set; } = new();
    public PendBloqueDto SinEscanear { get; set; } = new();
    public PendBloqueDto SinSalida { get; set; } = new();
    public PendBloqueDto Recepcion { get; set; } = new();
    public string Ventana { get; set; } = "";
    public string Actualizado { get; set; } = "";
}

/// <summary>Un mapeo registrado en CENTRAL (panel 3).</summary>
public sealed class MapeoRecienteDto
{
    public string Codigo { get; set; } = "";       // ARTCOD o "PALET"
    public string Descripcion { get; set; } = "";   // ARTDES o "N° xx"
    public bool EsPalet { get; set; }
    public string Modulo { get; set; } = "";
    public int Minutos { get; set; }
}

/// <summary>Panel 3: últimos mapeos en logística.</summary>
public sealed class PanelMapeosDto
{
    public List<MapeoRecienteDto> Items { get; set; } = new();
    public int Total { get; set; }
    public string Ventana { get; set; } = "";
    public string Actualizado { get; set; } = "";
}

/// <summary>Una ubicación de logística sin artículo asignado (panel 4).</summary>
public sealed class UbicacionLibreDto
{
    public string Pasillo { get; set; } = "";
    public string Modulo { get; set; } = "";
    public int? Posicion { get; set; }
}

/// <summary>Resumen de ubicaciones libres por pasillo (panel 4).</summary>
public sealed class PasilloLibreDto
{
    public string Pasillo { get; set; } = "";
    public int Cantidad { get; set; }
}

/// <summary>Panel 4: ubicaciones libres en logística (CENTRAL, sin artículo).</summary>
public sealed class PanelVaciasDto
{
    public List<UbicacionLibreDto> Items { get; set; } = new();
    public List<PasilloLibreDto> PorPasillo { get; set; } = new();
    public int Total { get; set; }
    public string Actualizado { get; set; } = "";
}

/// <summary>Un artículo estancado en logística (panel 5).</summary>
public sealed class ArticuloEstancadoDto
{
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public int DiasEstancado { get; set; }
    public bool NuncaEnviado { get; set; }
    public DateTime? FechaRef { get; set; }
    public string Atemporada { get; set; } = "";
    public bool EnTemporada { get; set; }
    public string? Modulo { get; set; }
    public int NUbic { get; set; }
    public bool ViaPalet { get; set; }
    public int? NroPalet { get; set; }
    public string? PaletModulo { get; set; }
    public int StockTotal { get; set; }
}

/// <summary>Panel 5: artículos estancados en logística (CENTRAL, fuera de palet).</summary>
public sealed class PanelEstancadosDto
{
    public List<ArticuloEstancadoDto> Items { get; set; } = new();
    public int Total { get; set; }
    public bool Loading { get; set; }       // cache calculándose por primera vez
    public string Temporada { get; set; } = "";
    public string Actualizado { get; set; } = "";
}

/// <summary>Un artículo pendiente de armar en el picking nocturno (panel 6).</summary>
public sealed class PickingItemDto
{
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public string Modulo { get; set; } = "";
    public string Pasillo { get; set; } = "";
    public int Pedidos { get; set; }
    public decimal Armados { get; set; }
    public int Falta { get; set; }
}

/// <summary>Estado del picking de un local (panel 6).</summary>
public sealed class PickingLocalDto
{
    public string Local { get; set; } = "";
    public int TotalPedido { get; set; }       // packs pedidos (reponibles)
    public decimal TotalArmado { get; set; }    // packs cubiertos (con cap)
    public int ArtsPedidos { get; set; }
    public int ArtsArmados { get; set; }
    public int ExtrasArts { get; set; }
    public decimal ExtrasPacks { get; set; }
    public List<PickingItemDto> Items { get; set; } = new();
}

/// <summary>Panel 6: picking nocturno (modo reposición 21-05h).</summary>
public sealed class PanelPickingDto
{
    public PickingLocalDto Luro { get; set; } = new() { Local = "LURO" };
    public PickingLocalDto Peralta { get; set; } = new() { Local = "PERALTA" };
    public DateTime? Corrida { get; set; }
    public bool ModoRepo { get; set; }          // estamos en horario 21-05h
    public string Actualizado { get; set; } = "";
}

/// <summary>Chip de ubicación (panel 7): góndola (pasillo+cantidad) o palet.</summary>
public sealed class UbicChipDto
{
    public string Tag { get; set; } = "";
    public int Cantidad { get; set; }
    public bool EsPalet { get; set; }
}

/// <summary>Un artículo con múltiples ubicaciones en CENTRAL (panel 7).</summary>
public sealed class ArticuloUbicacionesDto
{
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public int NUbic { get; set; }
    public List<UbicChipDto> PorPasillo { get; set; } = new();   // resumen (góndola + palets)
    public List<UbicChipDto> Detalle { get; set; } = new();       // cada módulo / palet individual
}

/// <summary>Panel 7: artículos con más ubicaciones en CENTRAL.</summary>
public sealed class PanelMasUbicDto
{
    public List<ArticuloUbicacionesDto> Items { get; set; } = new();
    public int Total { get; set; }
    public bool Loading { get; set; }       // cache calculándose por primera vez
    public string Actualizado { get; set; } = "";
}

/// <summary>Abastecimiento 7 ciclos (panel 8).</summary>
public sealed class RepoAbastDto
{
    public int EnvNeto { get; set; }   // enviado − devolución
    public int Venta { get; set; }     // venta gatillo (ventana -8..-2)
    public int Abast { get; set; }     // env_neto − venta
}

/// <summary>Día operativo de reposición por local (panel 8).</summary>
public sealed class RepoDiaOpDto
{
    public int VentaAyer { get; set; }
    public int RepoHoy { get; set; }
    public int RefuerzoHoy { get; set; }
    public int DevolAyer { get; set; }
    public double DevolPct { get; set; }
}

/// <summary>Un artículo "rojo" (cobertura ≥100%, a pedir) — panel 8/9.</summary>
public sealed class RepoRojoItemDto
{
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public int Pendientes { get; set; }
    public int Pack { get; set; }
    public int PacksAEnviar { get; set; }
    public string Modulo { get; set; } = "";
    public string Pasillo { get; set; } = "";
    public double Pct { get; set; }
}

/// <summary>Cobertura en vivo por local (panel 8) — bandas verde/ámbar/rojo.</summary>
public sealed class RepoCoberturaDto
{
    public int Verde { get; set; }
    public int Ambar { get; set; }
    public int Rojo { get; set; }
    public double PctRojo { get; set; }
    public int Mapeados { get; set; }
    public DateTime? Corrida { get; set; }
    public List<RepoRojoItemDto> ItemsRojos { get; set; } = new();
}

/// <summary>Card de reposición de un local (panel 8).</summary>
public sealed class RepoLocalPanelDto
{
    public string Local { get; set; } = "";
    public RepoAbastDto Abast { get; set; } = new();
    public RepoDiaOpDto DiaOp { get; set; } = new();
    public int VentaHoy { get; set; }
    public RepoCoberturaDto Cobertura { get; set; } = new();
}

/// <summary>Panel 8: panel de reposición · inteligencia del flujo.</summary>
public sealed class PanelReposicionDto
{
    public RepoLocalPanelDto Luro { get; set; } = new() { Local = "LURO" };
    public RepoLocalPanelDto Peralta { get; set; } = new() { Local = "PERALTA" };
    public bool Loading { get; set; }
    public string Actualizado { get; set; } = "";
}

/// <summary>Panel 9: detalle de artículos a reponer (rojos ≥100%), por local.</summary>
public sealed class PanelRojosDto
{
    public List<RepoRojoItemDto> Luro { get; set; } = new();
    public List<RepoRojoItemDto> Peralta { get; set; } = new();
    public int RojoLuro { get; set; }
    public int RojoPeralta { get; set; }
    public DateTime? Corrida { get; set; }
    public bool Loading { get; set; }
    public string Actualizado { get; set; } = "";
}

/// <summary>Panel 1 del dashboard de Logística: Despachos + Recepción.</summary>
public sealed class PanelDespachoRecepcionDto
{
    public List<DespachoLocalDto> Despacho { get; set; } = new();
    public List<RecepcionLocalDto> Recepcion { get; set; } = new();
    public string Ventana { get; set; } = "";
    public string Actualizado { get; set; } = "";
}
