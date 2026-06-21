namespace MarketWeb.Shared.Reemplazos;

/// <summary>Local (Ubicaciones tipo LOCAL) para el combo de filtro/ABM.</summary>
public sealed class LocalReemplazoDto
{
    public int Id { get; set; }
    public string Descripcion { get; set; } = "";
}

/// <summary>Una fila del listado de reemplazos.</summary>
public sealed class ReemplazoDto
{
    public int Id { get; set; }
    public DateTime? Fecha { get; set; }
    public string Ubicacion { get; set; } = "";
    public string ArtCod { get; set; } = "";
    public string DescripcionArt { get; set; } = "";
    public string ArtCodReemplazo { get; set; } = "";
    public string DescripcionArtReemplazo { get; set; } = "";
    public string? UbicacionLocal { get; set; }      // MAP2.Modulo (posición en el local)
    public string? MobiliarioLocal { get; set; }      // MAP2.Mobiliario
    public string? UbicacionDeposito { get; set; }    // MAP.Modulo (posición en depósito)
    public string Accion { get; set; } = "";
    public bool Procesado { get; set; }
}

/// <summary>Pedido de guardado (alta/modificación) de un reemplazo.</summary>
public sealed class ReemplazoSaveRequest
{
    public int Id { get; set; }
    public int IdUbicacion { get; set; }
    public string ArtCod { get; set; } = "";
    public string ArtCodReemplazo { get; set; } = "";
    public string Accion { get; set; } = "";
}

/// <summary>Reemplazo para el editor (modificación).</summary>
public sealed class ReemplazoEditorDto
{
    public int Id { get; set; }
    public int IdUbicacion { get; set; }
    public string ArtCod { get; set; } = "";
    public string DescripcionArt { get; set; } = "";
    public string ArtCodReemplazo { get; set; } = "";
    public string DescripcionArtReemplazo { get; set; } = "";
    public string Accion { get; set; } = "";
}

/// <summary>Descripción (+ combo) de un artículo de Dragonfish, para los lookups del ABM.</summary>
public sealed class ArticuloDescDto
{
    public string ArtCod { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Combo { get; set; } = "";
}

/// <summary>Resultado de validar que el original esté en posición reponible del local.</summary>
public sealed class ValidacionReemplazoDto
{
    public bool Ok { get; set; }
    public string Mensaje { get; set; } = "";
}

/// <summary>Resultado de marcar procesados + notificar a los locales.</summary>
public sealed class MarcarProcesadosResultadoDto
{
    public int Procesados { get; set; }
    public int Saltados { get; set; }          // sin artículo de reemplazo definido
    public int MailsEnviados { get; set; }
    public bool SmtpConfigurado { get; set; }
    public string? MailError { get; set; }
}

/// <summary>Un candidato del buscador automático de reemplazo (título de grupo o ítem).</summary>
public sealed class ReemplazoCandidatoDto
{
    public bool EsTitulo { get; set; }
    public int CatId { get; set; }
    public string ArtCod { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Combo { get; set; } = "";
    public int Stock { get; set; }
    // Solo en la variante "PASAR A PERCHERO":
    public string? Mobiliario { get; set; }
    public string? Modulo { get; set; }
}
