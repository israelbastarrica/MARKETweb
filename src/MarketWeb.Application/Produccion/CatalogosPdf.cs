using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using MarketWeb.Application.Reposicion; // ArialFontResolver

namespace MarketWeb.Application.Produccion;

/// <summary>
/// Genera el PDF del catálogo (lo que antes armaba Canva): una tarjeta por ítem sobre fondo negro.
///  - TEXTO: título centrado (la "pantalla negra con un texto").
///  - ARTÍCULO/OP/DG: foto del artículo + descripción, código y atributos (categoría, talles, materiales, etc.).
/// Diseño base a pulir. Se guarda como archivo físico en el server (no en la base).
/// </summary>
public sealed class CatalogosPdf
{
    private static readonly object _fontLock = new();

    /// <summary>Datos ya resueltos de una tarjeta (el servicio los arma; acá sólo se dibuja).</summary>
    public sealed class Carta
    {
        public bool EsTexto { get; set; }
        /// <summary>Ficha técnica (Orden de Pedido): la página ES esta imagen, a hoja completa.</summary>
        public byte[]? ImagenPagina { get; set; }
        public string Texto { get; set; } = "";
        public string Codigo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string Talles { get; set; } = "";
        public string Stock { get; set; } = "";
        public string Materiales { get; set; } = "";
        public string Colores { get; set; } = "";
        public string Subfamilia { get; set; } = "";
        public string Familia { get; set; } = "";
        public string Linea { get; set; } = "";
        public string Combo { get; set; } = "";
        public string Peso { get; set; } = "";
        public string Precio { get; set; } = "";
        public byte[]? Foto { get; set; }
    }

    public byte[] Construir(string nombreCatalogo, string temporada, int? anio, IReadOnlyList<Carta> cartas)
    {
        lock (_fontLock)
            if (GlobalFontSettings.FontResolver is null)
                GlobalFontSettings.FontResolver = new ArialFontResolver();

        using var doc = new PdfDocument();
        doc.Info.Title = $"Catálogo {nombreCatalogo}".Trim();

        var negro = XBrushes.Black;
        var blanco = XBrushes.White;
        var gris = new XSolidBrush(XColor.FromArgb(180, 180, 180));

        foreach (var c in cartas)
        {
            var page = doc.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            using var gfx = XGraphics.FromPdfPage(page);
            double w = page.Width, h = page.Height;

            // Ficha técnica (Orden de Pedido): la hoja ES la imagen, a página completa. Sin componer nada.
            if (c.ImagenPagina is { Length: > 0 })
            {
                try
                {
                    var bytes = c.ImagenPagina;
                    using var img = XImage.FromStream(() => new MemoryStream(bytes));
                    double iw = w, ih = img.PixelHeight / (double)img.PixelWidth * w;
                    if (ih > h) { ih = h; iw = img.PixelWidth / (double)img.PixelHeight * h; }
                    gfx.DrawImage(img, (w - iw) / 2, (h - ih) / 2, iw, ih);
                }
                catch { /* ficha ilegible → hoja en blanco */ }
                continue;
            }

            // Fondo negro (TEXTO y tarjeta de artículo compuesta).
            gfx.DrawRectangle(negro, 0, 0, w, h);

            if (c.EsTexto)
            {
                DibujarTexto(gfx, c.Texto, temporada, anio, w, h, blanco);
                continue;
            }

            const double margen = 40;
            double y = margen;

            // Encabezado chico (nombre del catálogo).
            var fEnc = new XFont("Arial", 10, XFontStyle.Regular);
            var enc = string.Join("  ·  ", new[] { nombreCatalogo, temporada, anio?.ToString() ?? "" }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (enc.Length > 0) { gfx.DrawString(enc, fEnc, gris, new XRect(margen, y, w - 2 * margen, 16), XStringFormats.TopLeft); }
            y += 24;

            // Foto (encajada en un recuadro, preservando proporción).
            double boxTop = y, boxH = h * 0.50, boxW = w - 2 * margen;
            if (c.Foto is { Length: > 0 })
            {
                try
                {
                    var bytes = c.Foto;
                    using var img = XImage.FromStream(() => new MemoryStream(bytes));
                    double scale = Math.Min(boxW / img.PixelWidth, boxH / img.PixelHeight);
                    double iw = img.PixelWidth * scale, ih = img.PixelHeight * scale;
                    double ix = (w - iw) / 2, iy = boxTop + (boxH - ih) / 2;
                    gfx.DrawImage(img, ix, iy, iw, ih);
                }
                catch { /* foto ilegible → se omite */ }
            }
            y = boxTop + boxH + 16;

            // Descripción (título).
            var fDesc = new XFont("Arial", 20, XFontStyle.Bold);
            y = DibujarParrafo(gfx, c.Descripcion, fDesc, blanco, margen, y, w - 2 * margen, 26);

            // Código.
            var fCod = new XFont("Arial", 13, XFontStyle.Bold);
            if (!string.IsNullOrWhiteSpace(c.Codigo))
            {
                gfx.DrawString(c.Codigo, fCod, gris, new XRect(margen, y, w - 2 * margen, 18), XStringFormats.TopLeft);
                y += 24;
            }

            // Precio destacado (si hay).
            if (!string.IsNullOrWhiteSpace(c.Precio) && c.Precio != "0")
            {
                var fPrecio = new XFont("Arial", 22, XFontStyle.Bold);
                gfx.DrawString($"$ {c.Precio}", fPrecio, blanco, new XRect(margen, y, w - 2 * margen, 28), XStringFormats.TopLeft);
                y += 34;
            }

            // Atributos (sólo los que tienen valor).
            var fLbl = new XFont("Arial", 11, XFontStyle.Bold);
            var fVal = new XFont("Arial", 11, XFontStyle.Regular);
            foreach (var (lbl, val) in Atributos(c))
            {
                if (string.IsNullOrWhiteSpace(val)) continue;
                if (y > h - margen - 14) break; // no desbordar la página
                gfx.DrawString(lbl + ":", fLbl, gris, new XRect(margen, y, 110, 15), XStringFormats.TopLeft);
                gfx.DrawString(val, fVal, blanco, new XRect(margen + 115, y, w - 2 * margen - 115, 15), XStringFormats.TopLeft);
                y += 18;
            }
        }

        // Si no hubo ninguna carta, una página vacía para que el PDF sea válido.
        if (doc.PageCount == 0) doc.AddPage().Size = PdfSharpCore.PageSize.A4;

        using var msOut = new MemoryStream();
        doc.Save(msOut);
        return msOut.ToArray();
    }

    private static IEnumerable<(string, string)> Atributos(Carta c)
    {
        yield return ("Categoría", c.Categoria);
        yield return ("Talles", c.Talles);
        yield return ("Colores", c.Colores);
        yield return ("Materiales", c.Materiales);
        yield return ("Subfamilia", c.Subfamilia);
        yield return ("Familia", c.Familia);
        yield return ("Línea", c.Linea);
        yield return ("Combo", c.Combo);
        yield return ("Peso", string.IsNullOrWhiteSpace(c.Peso) || c.Peso == "0" ? "" : c.Peso + " g");
        yield return ("Stock", c.Stock);
    }

    // Portada / separador (tarjeta TEXTO): eyebrow temporada·año arriba + título centrado + marca "MARKET" fija ABAJO.
    private static void DibujarTexto(XGraphics gfx, string texto, string temporada, int? anio, double w, double h, XBrush blanco)
    {
        var gris = new XSolidBrush(XColor.FromArgb(150, 150, 150));

        // Eyebrow (temporada · año), chico y con tracking, arriba.
        var eyebrow = string.Join("  ·  ", new[] { temporada, anio?.ToString() ?? "" }
            .Where(s => !string.IsNullOrWhiteSpace(s))).ToUpperInvariant();
        if (eyebrow.Length > 0)
            DibujarConTracking(gfx, eyebrow, new XFont("Arial", 11, XFontStyle.Regular), gris, w, h * 0.13, 4);

        // Título: tamaño moderado. Se elige el mayor que entre, con tope prudente (nada gigante).
        double maxW = w * 0.72;
        double topRegion = h * 0.24, botRegion = h * 0.78;
        double availH = botRegion - topRegion;
        var candidatos = new[] { 46, 42, 38, 34, 30, 26, 22 };
        XFont f = new("Arial", candidatos[^1], XFontStyle.Bold);
        List<string> lineas = Envolver(gfx, texto ?? "", f, maxW);
        foreach (var size in candidatos)
        {
            var ff = new XFont("Arial", size, XFontStyle.Bold);
            var ls = Envolver(gfx, texto ?? "", ff, maxW);
            if (ls.Count * ff.GetHeight() * 1.3 <= availH) { f = ff; lineas = ls; break; }
        }

        double lh = f.GetHeight() * 1.3;  // interlineado holgado
        double totalH = lineas.Count * lh;
        double y = topRegion + (availH - totalH) / 2;
        foreach (var ln in lineas)
        {
            gfx.DrawString(ln, f, blanco, new XRect(0, y, w, lh), XStringFormats.TopCenter);
            y += lh;
        }

        // Marca MARKET fija ABAJO: chica, con tracking, gris claro; hairline corta encima.
        var marca = new XSolidBrush(XColor.FromArgb(210, 210, 210));
        var fMarca = new XFont("Arial", 13, XFontStyle.Bold);
        double marcaY = h * 0.88;
        gfx.DrawLine(new XPen(XColor.FromArgb(80, 80, 80), 0.8), w * 0.42, marcaY - 10, w * 0.58, marcaY - 10);
        DibujarConTracking(gfx, "MARKET", fMarca, marca, w, marcaY, 7);
    }

    // Dibuja un texto centrado horizontalmente con espaciado extra entre letras (tracking).
    private static void DibujarConTracking(XGraphics gfx, string texto, XFont f, XBrush brush, double w, double y, double tracking)
    {
        double total = 0;
        foreach (var ch in texto) total += gfx.MeasureString(ch.ToString(), f).Width + tracking;
        total -= tracking;
        double x = (w - total) / 2;
        foreach (var ch in texto)
        {
            var s = ch.ToString();
            gfx.DrawString(s, f, brush, new XRect(x, y, 200, f.GetHeight()), XStringFormats.TopLeft);
            x += gfx.MeasureString(s, f).Width + tracking;
        }
    }

    // Dibuja un párrafo envuelto y devuelve la Y siguiente.
    private static double DibujarParrafo(XGraphics gfx, string texto, XFont f, XBrush brush, double x, double y, double maxW, double lh)
    {
        foreach (var ln in Envolver(gfx, texto ?? "", f, maxW))
        {
            gfx.DrawString(ln, f, brush, new XRect(x, y, maxW, lh), XStringFormats.TopLeft);
            y += lh;
        }
        return y;
    }

    // Word-wrap simple usando MeasureString.
    private static List<string> Envolver(XGraphics gfx, string texto, XFont f, double maxW)
    {
        var res = new List<string>();
        if (string.IsNullOrWhiteSpace(texto)) { res.Add(""); return res; }
        var palabras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var actual = "";
        foreach (var p in palabras)
        {
            var prueba = actual.Length == 0 ? p : actual + " " + p;
            if (gfx.MeasureString(prueba, f).Width > maxW && actual.Length > 0)
            {
                res.Add(actual);
                actual = p;
            }
            else actual = prueba;
        }
        if (actual.Length > 0) res.Add(actual);
        return res;
    }
}
