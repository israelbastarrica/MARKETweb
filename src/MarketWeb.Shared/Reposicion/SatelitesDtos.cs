namespace MarketWeb.Shared.Reposicion;

// ---- Eventos de reposición (sobrante/faltante cargados por los locales) ----

/// <summary>Una fila del listado de eventos (MARKET.dbo.EventosReposicion).</summary>
public sealed class EventoDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Local { get; set; } = "";
    public string ArtCod { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string RemitoDisplay { get; set; } = "";
    public string TipoDiferencia { get; set; } = "";   // SOBRANTE / FALTANTE
    public string CantidadPacks { get; set; } = "";
    public bool TieneFoto { get; set; }
    public string Estado { get; set; } = "";           // PENDIENTE / PROCESADO / ELIMINADO
}

/// <summary>Un ítem del remito asociado a un evento.</summary>
public sealed class EventoItemDto
{
    public string ArtCod { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Color { get; set; } = "";
    public string Talle { get; set; } = "";
    public string Cantidad { get; set; } = "";
}

/// <summary>Detalle de un evento (datos + items del remito si los hay).</summary>
public sealed class EventoDetalleDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Local { get; set; } = "";
    public string TipoCodigo { get; set; } = "";
    public string TipoDiferencia { get; set; } = "";
    public string CodigoEscaneado { get; set; } = "";
    public string DescripcionArt { get; set; } = "";
    public string RemitoDisplay { get; set; } = "";
    public string CantidadPacks { get; set; } = "";
    public string Usuario { get; set; } = "";
    public bool Procesado { get; set; }
    public bool Eliminado { get; set; }
    public bool TieneFoto { get; set; }
    public List<EventoItemDto> Items { get; set; } = new();
}

// ---- Reseteados (RepoReposicionArticulosReseteados) ----

/// <summary>Una fila del listado de reseteados.</summary>
public sealed class ReseteadoDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public string Local { get; set; } = "";
    public string Mobiliario { get; set; } = "";
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public int PacksDetectados { get; set; }
}

/// <summary>Reseteado para el editor.</summary>
public sealed class ReseteadoEditorDto
{
    public int Id { get; set; }
    public string Local { get; set; } = "";
    public string Mobiliario { get; set; } = "";
    public string ArtCod { get; set; } = "";
    public string ArtDes { get; set; } = "";
    public int PacksDetectados { get; set; }
}

/// <summary>Alta/modificación de un reseteado.</summary>
public sealed class ReseteadoSaveRequest
{
    public int Id { get; set; }
    public string Local { get; set; } = "";
    public string Mobiliario { get; set; } = "";
    public string ArtCod { get; set; } = "";
    public int PacksDetectados { get; set; }
}
