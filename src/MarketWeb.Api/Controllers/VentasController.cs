using ClosedXML.Excel;
using MarketWeb.Application.Ventas;
using MarketWeb.Shared.Ventas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

// Ventas/cobranzas = info sensible → solo ADMIN (igual que Costos).
[Authorize(Policy = "Admin")]
[ApiController]
[Route("api/[controller]")]
public sealed class VentasController : ControllerBase
{
    private readonly IVentasService _service;

    public VentasController(IVentasService service) => _service = service;

    [HttpGet("resumen")]
    public async Task<ActionResult<IReadOnlyList<VentaResumenDto>>> Resumen(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta, CancellationToken ct)
        => Ok(await _service.ListarResumenAsync(desde, hasta, ct));

    [HttpGet("cobranzas")]
    public async Task<ActionResult<IReadOnlyList<CobranzaDto>>> Cobranzas(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta,
        [FromQuery] string local = "TODOS", [FromQuery] string agrupamiento = "DÍA",
        [FromQuery] string detalle = "CATEGORIA", [FromQuery] string categoria = "TODOS",
        [FromQuery] string medio = "TODOS", CancellationToken ct = default)
        => Ok(await _service.ListarCobranzasAsync(desde, hasta, local, agrupamiento, detalle, categoria, medio, ct));

    [HttpGet("excel")]
    public async Task<IActionResult> Excel(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta,
        [FromQuery] string local = "TODOS", [FromQuery] string agrupamiento = "DÍA",
        [FromQuery] string detalle = "CATEGORIA", [FromQuery] string categoria = "TODOS",
        [FromQuery] string medio = "TODOS", CancellationToken ct = default)
    {
        var resumen = await _service.ListarResumenAsync(desde, hasta, ct);
        var cobranzas = await _service.ListarCobranzasAsync(desde, hasta, local, agrupamiento, detalle, categoria, medio, ct);

        using var wb = new XLWorkbook();

        // --- Hoja Resumen ---
        var ws1 = wb.Worksheets.Add("Resumen");
        string[] h1 = { "Período", "Ventas", "Costos", "IVA Ventas" };
        for (var c = 0; c < h1.Length; c++) ws1.Cell(1, c + 1).Value = h1[c];
        EstiloCabecera(ws1.Range(1, 1, 1, h1.Length));
        var r = 2;
        foreach (var x in resumen)
        {
            ws1.Cell(r, 1).Value = x.Periodo;
            ws1.Cell(r, 2).Value = x.Ventas;
            ws1.Cell(r, 3).Value = x.Costos;
            ws1.Cell(r, 4).Value = x.Iva_Vtas;
            r++;
        }
        ws1.Columns(2, 4).Style.NumberFormat.Format = "$ #,##0.00";
        ws1.Columns().AdjustToContents();

        // --- Hoja Cobranzas ---
        var ws2 = wb.Worksheets.Add("Cobranzas");
        string[] h2 = { "Período", "Local", "Categoría", "Medio", "Payway", "Cant. Op.", "Total" };
        for (var c = 0; c < h2.Length; c++) ws2.Cell(1, c + 1).Value = h2[c];
        EstiloCabecera(ws2.Range(1, 1, 1, h2.Length));
        r = 2;
        foreach (var x in cobranzas)
        {
            ws2.Cell(r, 1).Value = x.Periodo;
            ws2.Cell(r, 2).Value = x.Local;
            ws2.Cell(r, 3).Value = x.Categoria;
            ws2.Cell(r, 4).Value = x.Medio;
            ws2.Cell(r, 5).Value = x.PasaPorPayway ? "Sí" : "No";
            ws2.Cell(r, 6).Value = x.CantidadOperaciones;
            ws2.Cell(r, 7).Value = x.Total;
            if (x.Total < 0) ws2.Range(r, 1, r, 7).Style.Font.FontColor = XLColor.Red;
            r++;
        }
        ws2.Column(6).Style.NumberFormat.Format = "#,##0";
        ws2.Column(7).Style.NumberFormat.Format = "$ #,##0.00";
        ws2.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var nombre = $"Ventas_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    private static void EstiloCabecera(IXLRange rango)
    {
        rango.Style.Font.Bold = true;
        rango.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C2C2E");
        rango.Style.Font.FontColor = XLColor.White;
    }
}
