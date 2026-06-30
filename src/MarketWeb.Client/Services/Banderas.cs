namespace MarketWeb.Client.Services;

/// <summary>País (texto libre) → emoji de bandera. Para mostrar al lado del viaje.</summary>
public static class Banderas
{
    private static readonly Dictionary<string, string> Mapa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["china"] = "🇨🇳", ["chile"] = "🇨🇱", ["argentina"] = "🇦🇷",
        ["brasil"] = "🇧🇷", ["brazil"] = "🇧🇷",
        ["india"] = "🇮🇳", ["italia"] = "🇮🇹",
        ["españa"] = "🇪🇸", ["espana"] = "🇪🇸",
        ["turquia"] = "🇹🇷", ["turquía"] = "🇹🇷",
        ["estados unidos"] = "🇺🇸", ["usa"] = "🇺🇸", ["eeuu"] = "🇺🇸",
        ["peru"] = "🇵🇪", ["perú"] = "🇵🇪",
        ["uruguay"] = "🇺🇾", ["paraguay"] = "🇵🇾",
        ["mexico"] = "🇲🇽", ["méxico"] = "🇲🇽",
        ["francia"] = "🇫🇷", ["alemania"] = "🇩🇪", ["portugal"] = "🇵🇹",
        ["colombia"] = "🇨🇴", ["bolivia"] = "🇧🇴", ["vietnam"] = "🇻🇳",
        ["bangladesh"] = "🇧🇩", ["indonesia"] = "🇮🇩", ["tailandia"] = "🇹🇭",
    };

    public static string Emoji(string? pais)
    {
        var k = (pais ?? "").Trim();
        return k.Length > 0 && Mapa.TryGetValue(k, out var e) ? e : "🌎";
    }
}
