using System.Data;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Reposicion;
using Microsoft.Data.SqlClient;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;

namespace MarketWeb.Application.Reposicion;

/// <summary>
/// Porteo del generador de PDF de frmRepoReposicion (GenerarPDFsPorLocal / EscribirSeccionLocal) a PdfSharpCore.
/// Cuadernillo único: una página por local, secciones fijas de reposición + reemplazos (huérfanos de la corrida).
/// </summary>
public sealed class ReposicionPdf : IReposicionPdf
{
    private readonly ISqlConnectionFactory _db;
    private static readonly object _fontLock = new();

    public ReposicionPdf(ISqlConnectionFactory db) => _db = db;

    // Secciones fijas de reposición (cada una agrupa uno o más TipoArt, case/tilde insensible).
    private static readonly (string Titulo, string[] Tipos)[] SeccionesReposicion =
    {
        ("Indumentaria: mesa", new[] { "INDUMENTARIA" }),
        ("Blanquería, Calzado: mesa, mesa chica, estanteria, panel, cajon",
            new[] { "CASA BLANQUERIA", "CASA BLANQUERÍA", "BLANQUERIA", "BLANQUERÍA", "CALZADO" }),
        ("Lencería: mesa, mesa chica, estanteria, panel, cajon", new[] { "LENCERIA", "LENCERÍA" }),
        ("Accesorios", new[] { "ACCESORIOS" }),
    };

    private static readonly string[] MueblesPerchero = { "PERCHERO" };

    private static readonly string[] HeadersRepo =
        { "Ub. Depósito", "TipoArt", "Categoria", "Codigo", "DescArt", "Mes, Día, Año", "Mobiliario", "Cant." };
    private static readonly double[] PctsRepo = { 0.18, 0.10, 0.10, 0.10, 0.24, 0.10, 0.10, 0.08 };

    private static readonly string[] HeadersReemp =
        { "TipoArt", "Categoria", "Combo", "Codigo", "DescArt", "Mes, Día, Año", "Mobiliario", "Cant." };
    private static readonly double[] PctsReemp = { 0.12, 0.10, 0.10, 0.10, 0.30, 0.10, 0.10, 0.08 };

    // Alineación por columna (una sola línea, recortada al ancho de la celda como hace el desktop).
    private static readonly XStringAlignment[] CellAligns =
    {
        XStringAlignment.Near, XStringAlignment.Near, XStringAlignment.Near,
        XStringAlignment.Near, XStringAlignment.Near, XStringAlignment.Center,
        XStringAlignment.Near, XStringAlignment.Far
    };

    public async Task<byte[]> GenerarAsync(ReposicionResultadoDto datos, DateTime? fechaCorte, CancellationToken ct = default)
    {
        lock (_fontLock)
        {
            if (GlobalFontSettings.FontResolver is null)
                GlobalFontSettings.FontResolver = new ArialFontResolver();
        }

        // Agrupar por LocalDestino preservando el orden de picking del SP.
        var porLocal = new Dictionary<string, List<ReposicionFilaDto>>();
        var ordenLocales = new List<string>();
        foreach (var f in datos.Filas)
        {
            if (!porLocal.TryGetValue(f.LocalDestino, out var lst))
            {
                lst = new List<ReposicionFilaDto>();
                porLocal[f.LocalDestino] = lst;
                ordenLocales.Add(f.LocalDestino);
            }
            lst.Add(f);
        }

        var doc = new PdfDocument();
        foreach (var loc in ordenLocales)
            await EscribirSeccionLocalAsync(doc, loc, porLocal[loc], fechaCorte, ct);

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    private async Task EscribirSeccionLocalAsync(PdfDocument doc, string local, List<ReposicionFilaDto> filas,
        DateTime? fechaCorte, CancellationToken ct)
    {
        var fontTitulo = new XFont("Arial", 14, XFontStyle.Bold);
        var fontSeccion = new XFont("Arial", 11, XFontStyle.Bold);
        var fontMeta = new XFont("Arial", 9, XFontStyle.Regular);
        var fontCabecera = new XFont("Arial", 8, XFontStyle.Bold);
        var fontCelda = new XFont("Arial", 8, XFontStyle.Regular);

        const double margen = 40;
        const double altoFila = 16;

        var page = doc.AddPage();
        page.Orientation = PageOrientation.Landscape;
        var gfx = XGraphics.FromPdfPage(page);
        double y = margen;
        double anchoTotal = page.Width.Point - margen * 2;

        // Totales del local (solo reposición).
        var repoFilas = filas.Where(f => !f.EsHuerfano).ToList();
        int totalPacks = repoFilas.Sum(f => f.Packs);
        int totalPrendas = repoFilas.Sum(f => f.Packs * f.CantPack);

        // Ventas del día anterior (respecto de la fecha simulada si la hay).
        var diaAnterior = (fechaCorte ?? DateTime.Today).AddDays(-1);
        int ventasAyer = await VentasDiaAsync(local, diaAnterior, ct);

        // Cabecera de la página.
        gfx.DrawString("Reposición — " + local, fontTitulo, XBrushes.Black, margen, y + 14);
        y += 22;
        gfx.DrawString("Generado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"), fontMeta, XBrushes.Black, margen, y + 10);
        y += 14;
        var metaTxt = $"{repoFilas.Count:N0} artículos · {totalPacks:N0} packs · {totalPrendas:N0} prendas";
        if (ventasAyer >= 0) metaTxt += $" · {ventasAyer:N0} ventas ({diaAnterior:dd/MM})";
        gfx.DrawString(metaTxt, fontMeta, XBrushes.Black, margen, y + 10);
        y += 22;

        // --- Helpers de dibujo (closures sobre page/gfx/tf/y reasignables) ---
        void NuevaPagina()
        {
            page = doc.AddPage();
            page.Orientation = PageOrientation.Landscape;
            gfx = XGraphics.FromPdfPage(page);
            y = margen;
        }

        // Una sola línea, centrada vertical y recortada con … al ancho de la celda (no desborda nunca).
        void Celda(string texto, XFont font, XRect celda, XStringAlignment align)
        {
            var inner = new XRect(celda.X + 2, celda.Y, celda.Width - 4, celda.Height);
            var t = TruncarParaAncho(gfx, texto ?? "", font, inner.Width);
            if (t.Length == 0) return;
            gfx.DrawString(t, font, XBrushes.Black, inner,
                new XStringFormat { Alignment = align, LineAlignment = XLineAlignment.Center });
        }

        void DibujarEncabezados(string[] headers, double[] anchos)
        {
            double x = margen;
            for (int i = 0; i < headers.Length; i++)
            {
                gfx.DrawRectangle(XPens.Black, XBrushes.LightGray, new XRect(x, y, anchos[i], altoFila));
                Celda(headers[i], fontCabecera, new XRect(x, y, anchos[i], altoFila), XStringAlignment.Center);
                x += anchos[i];
            }
            y += altoFila;
        }

        void DibujarSeccion(string titulo, List<string[]> items, string[] headers, double[] pcts)
        {
            if (items.Count == 0) return;
            var anchos = new double[headers.Length];
            for (int i = 0; i < headers.Length; i++) anchos[i] = anchoTotal * pcts[i];

            if (y + altoFila * 3 > page.Height.Point - margen) NuevaPagina();
            gfx.DrawString(titulo, fontSeccion, XBrushes.Black, margen, y + 12);
            y += 18;
            DibujarEncabezados(headers, anchos);

            foreach (var valores in items)
            {
                if (y + altoFila > page.Height.Point - margen)
                {
                    NuevaPagina();
                    gfx.DrawString(titulo + "  (cont.)", fontSeccion, XBrushes.Black, margen, y + 12);
                    y += 18;
                    DibujarEncabezados(headers, anchos);
                }
                double x = margen;
                for (int i = 0; i < headers.Length; i++)
                {
                    gfx.DrawRectangle(XPens.Black, new XRect(x, y, anchos[i], altoFila));
                    Celda(valores[i] ?? "", fontCelda, new XRect(x, y, anchos[i], altoFila), CellAligns[i]);
                    x += anchos[i];
                }
                y += altoFila;
            }
            y += 12;
        }

        // === REPOSICIÓN: 4 secciones fijas por TipoArt + "otros" ===
        var porSeccion = new List<string[]>[SeccionesReposicion.Length];
        for (int i = 0; i < porSeccion.Length; i++) porSeccion[i] = new List<string[]>();
        var otros = new List<string[]>();

        foreach (var f in repoFilas)
        {
            var fila = FilaRepo(f);
            int asignada = -1;
            for (int idx = 0; idx < SeccionesReposicion.Length && asignada < 0; idx++)
                foreach (var cand in SeccionesReposicion[idx].Tipos)
                    if (string.Equals(f.TipoArt, cand, StringComparison.OrdinalIgnoreCase)) { asignada = idx; break; }
            if (asignada >= 0) porSeccion[asignada].Add(fila); else otros.Add(fila);
        }

        for (int idx = 0; idx < SeccionesReposicion.Length; idx++)
            DibujarSeccion($"{local} reposición ({SeccionesReposicion[idx].Titulo})", porSeccion[idx], HeadersRepo, PctsRepo);
        if (otros.Count > 0)
            DibujarSeccion($"{local} reposición (otros)", otros, HeadersRepo, PctsRepo);

        // === REEMPLAZOS: huérfanos nuevos de esta corrida, 2 secciones por mobiliario ===
        var huerfanos = filas.Where(f => f.EsHuerfano && f.NuevoEstaCorrida).ToList();
        var reempNoPerchero = huerfanos
            .Where(h => !MueblesPerchero.Contains(h.Mobiliario.ToUpperInvariant()))
            .OrderBy(f => f.TipoArt).ThenBy(f => f.Categoria).ThenBy(f => f.ArtCod)
            .Select(FilaReemp).ToList();
        var reempPerchero = huerfanos
            .Where(h => MueblesPerchero.Contains(h.Mobiliario.ToUpperInvariant()))
            .OrderBy(f => f.TipoArt).ThenBy(f => f.Categoria).ThenBy(f => f.ArtCod)
            .Select(FilaReemp).ToList();

        if (reempNoPerchero.Count > 0 || reempPerchero.Count > 0) NuevaPagina();
        DibujarSeccion($"{local} reemplazos (mesa, mesa chica, estantería, panel, cajón)", reempNoPerchero, HeadersReemp, PctsReemp);
        DibujarSeccion($"{local} reemplazos (perchero)", reempPerchero, HeadersReemp, PctsReemp);
    }

    // Recorta el texto a una sola línea que entre en maxWidth, agregando … si hubo que cortar.
    // Reemplaza al wrap de XTextFormatter, que desbordaba con strings largos sin espacios.
    private static string TruncarParaAncho(XGraphics gfx, string s, XFont font, double maxWidth)
    {
        if (string.IsNullOrEmpty(s) || maxWidth <= 0) return "";
        if (gfx.MeasureString(s, font).Width <= maxWidth) return s;
        for (int len = s.Length - 1; len > 0; len--)
        {
            var cand = s.Substring(0, len).TrimEnd() + "…";
            if (gfx.MeasureString(cand, font).Width <= maxWidth) return cand;
        }
        return "";
    }

    private static string[] FilaRepo(ReposicionFilaDto f) => new[]
    {
        f.UbicacionDeposito, f.TipoArt, f.Categoria, f.ArtCod, f.ArtDes,
        f.UltRemitoFecha.ToString("dd/MM/yyyy"), f.Mobiliario, f.Packs.ToString("N0")
    };

    private static string[] FilaReemp(ReposicionFilaDto f) => new[]
    {
        f.TipoArt, f.Categoria, f.Combo, f.ArtCod, f.ArtDes,
        f.UltRemitoFecha == default ? "" : f.UltRemitoFecha.ToString("dd/MM/yyyy"), f.Mobiliario, f.Packs.ToString("N0")
    };

    private async Task<int> VentasDiaAsync(string local, DateTime dia, CancellationToken ct)
    {
        try
        {
            await using var cn = _db.Create();
            await cn.OpenAsync(ct);
            var sql =
                "SELECT ISNULL(SUM(D.FCANT * C.SIGNOMOV), 0) " +
                $"FROM DRAGONFISH_{local}.Zoologic.COMPROBANTEV C WITH(NOLOCK) " +
                $"INNER JOIN DRAGONFISH_{local}.Zoologic.COMPROBANTEVDET D WITH(NOLOCK) ON C.CODIGO = D.CODIGO " +
                "WHERE C.ANULADO = 0 AND C.FLETRA NOT IN ('R', 'X') AND CAST(C.FFCH AS DATE) = @Dia";
            await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            cmd.Parameters.Add("@Dia", SqlDbType.Date).Value = dia.Date;
            var res = await cmd.ExecuteScalarAsync(ct);
            return res is null or DBNull ? -1 : Convert.ToInt32(res);
        }
        catch
        {
            return -1;   // dato accesorio: no rompemos el PDF
        }
    }
}
