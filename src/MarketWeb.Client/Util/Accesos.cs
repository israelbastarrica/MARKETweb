namespace MarketWeb.Client.Util;

/// <summary>Un acceso conocido de la app (ruta canónica + etiqueta + ícono + área/módulo).</summary>
public sealed record Acceso(string Ruta, string Etiqueta, string Icono, string Area = "");

/// <summary>
/// Catálogo de pantallas "reales" (no WIP). Se usa para registrar el uso
/// (normalizando la URL a su ruta canónica) y para pintar los accesos rápidos del home.
/// </summary>
public static class Accesos
{
    public static readonly Acceso[] Catalogo =
    {
        new("/locales/dashboard", "Dashboard", "fa-solid fa-gauge-high", "Locales"),
        new("/logistica/dashboard", "Dashboard", "fa-solid fa-gauge-high", "Logística"),
        new("/locales/mapeo", "Mapeo", "fa-solid fa-map", "Locales"),
        new("/logistica/mapeo", "Mapeo", "fa-solid fa-map", "Logística"),
        new("/consulta-articulo", "Consulta Artículo", "fa-solid fa-barcode"),
        new("/insumos/pedidos", "Insumos", "fa-solid fa-boxes-stacked", "Locales"),
        new("/administracion/insumos", "Insumos", "fa-solid fa-boxes-stacked", "Administración"),
        new("/administracion/costos", "Costos", "fa-solid fa-money-bill", "Administración"),
        new("/administracion/ventas", "Ventas", "fa-solid fa-cash-register", "Administración"),
        new("/locales/despachos", "Despachos", "fa-solid fa-qrcode", "Locales"),
        new("/logistica/despachos", "Despachos", "fa-solid fa-qrcode", "Logística"),
        new("/locales/remito-impresion", "Remito Impresión", "fa-solid fa-print", "Locales"),
        new("/logistica/remito-impresion", "Remito Impresión", "fa-solid fa-print", "Logística"),
        new("/logistica/palets", "Palets", "fa-solid fa-pallet", "Logística"),
        new("/logistica/reemplazos", "Reemplazos", "fa-solid fa-retweet", "Logística"),
        new("/logistica/reposicion", "Reposición", "fa-solid fa-clipboard-check", "Logística"),
        new("/configuracion/locales", "Locales", "fa-solid fa-shop", "Configuración"),
        new("/configuracion/tipos-locales", "Tipos de Locales", "fa-solid fa-warehouse", "Configuración"),
        new("/configuracion/usuarios", "Usuarios", "fa-solid fa-users", "Configuración"),
        new("/compras/calculadora", "Calculadora", "fa-solid fa-calculator", "Compras"),
        new("/sistemas/tareas", "Tareas programadas", "fa-solid fa-clock", "Sistemas"),
    };

    /// <summary>Normaliza una URL a su acceso canónico (prefijo más largo que matchea). null si no es conocida o es el home.</summary>
    public static Acceso? Normalizar(string? ruta)
    {
        var r = (ruta ?? "").Split('?')[0].TrimEnd('/').ToLowerInvariant();
        if (r.Length == 0) return null;
        Acceso? mejor = null;
        foreach (var a in Catalogo)
        {
            var c = a.Ruta.ToLowerInvariant();
            if (r == c || r.StartsWith(c + "/"))
                if (mejor is null || a.Ruta.Length > mejor.Ruta.Length) mejor = a;
        }
        return mejor;
    }

    public static Acceso? PorRuta(string? ruta)
        => Catalogo.FirstOrDefault(a => string.Equals(a.Ruta, ruta, StringComparison.OrdinalIgnoreCase));
}
