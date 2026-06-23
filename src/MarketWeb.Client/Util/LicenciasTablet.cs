namespace MarketWeb.Client.Util;

/// <summary>
/// Mapa "Esta PC" (tablet) → licencia/terminal Dragon, para rutear la impresora del remito
/// (va en InformacionAdicional). Solo aplica a las tablets de Logística; el resto no tiene licencia.
/// </summary>
public static class LicenciasTablet
{
    public static readonly IReadOnlyDictionary<string, string> PorTablet =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TabletLog1"] = "809131",
            ["TabletLog2"] = "809129",
        };

    /// <summary>Licencia de esa PC, o null si no es una tablet mapeada.</summary>
    public static string? Licencia(string? pcNombre)
        => !string.IsNullOrWhiteSpace(pcNombre) && PorTablet.TryGetValue(pcNombre.Trim(), out var l) ? l : null;
}
