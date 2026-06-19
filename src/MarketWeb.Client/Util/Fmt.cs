using System.Globalization;

namespace MarketWeb.Client.Util;

/// <summary>Formato numérico AR (separador de miles ".", decimal ","). Reutilizable en todas las pantallas.</summary>
public static class Fmt
{
    private static readonly NumberFormatInfo Ar = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = new[] { 3 }
    };

    /// <summary>Entero con separador de miles (ej: 1.234).</summary>
    public static string Miles(int v) => v.ToString("#,##0", Ar);

    /// <summary>Número con separador de miles, sin decimales (ej: 1.234).</summary>
    public static string Miles(decimal v) => v.ToString("#,##0", Ar);

    /// <summary>Moneda (ej: $ 1.234,56).</summary>
    public static string Money(decimal v) => "$ " + v.ToString("#,##0.00", Ar);

    /// <summary>Porcentaje (ej: 41,93 %).</summary>
    public static string Pct(decimal v) => v.ToString("#,##0.00", Ar) + " %";
}
