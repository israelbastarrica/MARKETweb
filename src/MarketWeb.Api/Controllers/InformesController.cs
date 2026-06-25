using MarketWeb.Application.Informes;
using MarketWeb.Shared.Informes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

// Informes (Ventas: comprado vs vendido por proveedor/temporada/año + margen + venta forzada).
[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/informes")]
public sealed class InformesController : ControllerBase
{
    private readonly IInformesService _svc;
    public InformesController(IInformesService svc) => _svc = svc;

    [HttpGet("ventas/combos")]
    public async Task<IActionResult> Combos(CancellationToken ct) => Ok(await _svc.CombosAsync(ct));

    [HttpPost("ventas")]
    public async Task<IActionResult> Ventas([FromBody] InformeVentaFiltro f, CancellationToken ct)
        => Ok(await _svc.VentasAsync(f, ct));

    [HttpPost("ventas/serie")]
    public async Task<IActionResult> VentasSerie([FromQuery] string dimension, [FromBody] InformeVentaFiltro f, CancellationToken ct)
        => Ok(await _svc.VentasSerieAsync(f, dimension, ct));

    [HttpGet("ventas/excel")]
    public async Task<IActionResult> VentasExcel([FromQuery] string? prov, [FromQuery] string? temp,
        [FromQuery] int anio, [FromQuery] DateTime desde, [FromQuery] DateTime hasta, CancellationToken ct)
    {
        var f = new InformeVentaFiltro { ProveedorCod = prov, Temporada = temp, Anio = anio, Desde = desde, Hasta = hasta };
        var bytes = await _svc.VentasExcelAsync(f, ct);
        var nombre = $"Ventas_{(string.IsNullOrWhiteSpace(prov) ? "todos" : prov)}_{desde:yyyyMMdd}-{hasta:yyyyMMdd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }
}
