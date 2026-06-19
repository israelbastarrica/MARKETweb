using ClosedXML.Excel;
using MarketWeb.Application.Costos;
using MarketWeb.Shared.Costos;
using MarketWeb.Shared.Insumos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

// Costos/márgenes = info sensible → solo ADMIN (ajustable). En el desktop estaba
// restringido por máquina; en web lo gobernamos por perfil.
[Authorize(Policy = "Admin")]
[ApiController]
[Route("api/[controller]")]
public sealed class CostosController : ControllerBase
{
    private readonly ICostosService _service;

    public CostosController(ICostosService service) => _service = service;

    [HttpGet("ubicaciones")]
    public async Task<ActionResult<IReadOnlyList<UbicacionDto>>> Ubicaciones(CancellationToken ct)
        => Ok(await _service.ListarUbicacionesAsync(ct));

    [HttpGet("margen")]
    public async Task<ActionResult<IReadOnlyList<CostoMargenDto>>> Margen(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta,
        [FromQuery] string local = "TODOS", [FromQuery] string agrupamiento = "DÍA",
        CancellationToken ct = default)
        => Ok(await _service.ListarMargenAsync(desde, hasta, local, agrupamiento, ct));

    [HttpGet("margen/excel")]
    public async Task<IActionResult> MargenExcel(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta,
        [FromQuery] string local = "TODOS", [FromQuery] string agrupamiento = "DÍA",
        CancellationToken ct = default)
    {
        var datos = await _service.ListarMargenAsync(desde, hasta, local, agrupamiento, ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Margen");

        string[] cabeceras = { "Período", "Local", "Cant. Vendida", "Total Venta", "Total Costo", "Margen ($)", "Margen (%)" };
        for (var c = 0; c < cabeceras.Length; c++)
            ws.Cell(1, c + 1).Value = cabeceras[c];
        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#2C2C2E");
        ws.Row(1).Style.Font.FontColor = XLColor.White;

        var r = 2;
        foreach (var d in datos)
        {
            ws.Cell(r, 1).Value = d.Periodo;
            ws.Cell(r, 2).Value = d.Local;
            ws.Cell(r, 3).Value = d.CantidadVendida;
            ws.Cell(r, 4).Value = d.TotalFacturado;
            ws.Cell(r, 5).Value = d.TotalCosto;
            ws.Cell(r, 6).Value = d.MargenDinero;
            ws.Cell(r, 7).Value = d.MargenPorcentaje;
            if (d.MargenDinero < 0)
                ws.Range(r, 1, r, 7).Style.Font.FontColor = XLColor.Red;
            r++;
        }

        // Total general
        if (datos.Count > 0)
        {
            var totVenta = datos.Sum(d => d.TotalFacturado);
            var totCosto = datos.Sum(d => d.TotalCosto);
            var totMargen = datos.Sum(d => d.MargenDinero);
            ws.Cell(r, 2).Value = "TOTAL";
            ws.Cell(r, 3).Value = datos.Sum(d => d.CantidadVendida);
            ws.Cell(r, 4).Value = totVenta;
            ws.Cell(r, 5).Value = totCosto;
            ws.Cell(r, 6).Value = totMargen;
            ws.Cell(r, 7).Value = totVenta != 0 ? Math.Round(totMargen / totVenta * 100, 2) : 0;
            ws.Range(r, 1, r, 7).Style.Font.Bold = true;
            ws.Range(r, 1, r, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1EFE8");
        }

        // Formatos numéricos (Excel muestra separadores según el idioma del usuario)
        ws.Column(3).Style.NumberFormat.Format = "#,##0";
        ws.Column(4).Style.NumberFormat.Format = "$ #,##0.00";
        ws.Column(5).Style.NumberFormat.Format = "$ #,##0.00";
        ws.Column(6).Style.NumberFormat.Format = "$ #,##0.00";
        ws.Column(7).Style.NumberFormat.Format = "#,##0.00\" %\"";
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var nombre = $"Costos_Margen_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }
}
