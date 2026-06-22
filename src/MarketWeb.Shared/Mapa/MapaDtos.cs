using System.Text.Json.Serialization;

namespace MarketWeb.Shared.Mapa;

// Los nombres JSON son EXACTOS a los que consume el visor 3D (deposito-3d/index.html),
// por eso los [JsonPropertyName] — así el visor se reusa sin tocar la lógica.

/// <summary>Módulo con cantidad de artículos (para pintar verde/rojo en el mapa).</summary>
public sealed class MapaModuloDto
{
    [JsonPropertyName("Modulo")] public string Modulo { get; set; } = "";
    [JsonPropertyName("CantidadArticulos")] public int CantidadArticulos { get; set; }
}

/// <summary>Un artículo dentro de un módulo (panel al clickear).</summary>
public sealed class MapaArticuloDto
{
    [JsonPropertyName("ARTCOD")] public string ARTCOD { get; set; } = "";
    [JsonPropertyName("ARTDES")] public string? ARTDES { get; set; }
    [JsonPropertyName("MARCA")] public string? MARCA { get; set; }
    [JsonPropertyName("NroPalet")] public string? NroPalet { get; set; }
}

/// <summary>Detalle de un módulo: vacío + sus artículos.</summary>
public sealed class MapaModuloDetalleDto
{
    [JsonPropertyName("modulo")] public string Modulo { get; set; } = "";
    [JsonPropertyName("vacio")] public bool Vacio { get; set; }
    [JsonPropertyName("productos")] public List<MapaArticuloDto> Productos { get; set; } = new();
}
