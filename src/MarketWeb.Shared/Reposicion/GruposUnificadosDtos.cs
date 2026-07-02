namespace MarketWeb.Shared.Reposicion;

/// <summary>
/// Grupo de artículos "unificados" para reposición: varios ARTCOD (mismo cajón/mobiliario) que
/// deben contar su venta como uno solo. Este ABM solo DEFINE los grupos; la lógica de la repo
/// (sumar la venta y decidir qué reponer) se maneja aparte.
/// </summary>
public sealed class GrupoUnificadoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int CantArticulos { get; set; }
}

/// <summary>Un artículo dentro de un grupo, resuelto para mostrar.</summary>
public sealed class GrupoArticuloDto
{
    public string ArtCod { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int? CantPack { get; set; }
    public bool ExisteEnDragon { get; set; }
}

/// <summary>Cabecera + artículos de un grupo (pantalla ABM).</summary>
public sealed class GrupoUnificadoDetalleDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public List<GrupoArticuloDto> Articulos { get; set; } = new();
}

/// <summary>Alta/edición de un grupo (cabecera + lista completa de ARTCOD).</summary>
public sealed class GrupoUnificadoGuardarRequest
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public List<string> ArtCods { get; set; } = new();
}
