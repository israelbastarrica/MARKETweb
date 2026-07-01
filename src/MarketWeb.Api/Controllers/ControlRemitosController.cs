using MarketWeb.Application.Reposicion;
using MarketWeb.Shared.Reposicion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/control-remitos")]
public sealed class ControlRemitosController : ControllerBase
{
    private readonly IControlRemitosService _service;
    private readonly IReporteControlReposicionService _reporte;
    public ControlRemitosController(IControlRemitosService service, IReporteControlReposicionService reporte)
    {
        _service = service;
        _reporte = reporte;
    }

    // Reporte de control de reposición (mismo que se manda por mail): HTML listo para mostrar/imprimir.
    [HttpGet("reporte")]
    public async Task<IActionResult> Reporte([FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null, CancellationToken ct = default)
    {
        var (html, _) = await _reporte.ConstruirAsync(desde ?? DateTime.Today.AddDays(-1).AddHours(21), hasta ?? DateTime.Now, ct);
        return Content(html, "text/html", System.Text.Encoding.UTF8);
    }

    [HttpGet("reporte/pdf")]
    public async Task<IActionResult> ReportePdf([FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null, CancellationToken ct = default)
    {
        var (pdf, _) = await _reporte.ConstruirPdfAsync(desde ?? DateTime.Today.AddDays(-1).AddHours(21), hasta ?? DateTime.Now, ct);
        return File(pdf, "application/pdf");   // inline: se abre en el navegador
    }

    [HttpGet("estado")]
    public async Task<ActionResult<IReadOnlyList<ControlEstadoDto>>> Estado(
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null, CancellationToken ct = default)
        => Ok(await _service.EstadoAsync(desde ?? DateTime.Today.AddDays(-4), hasta ?? DateTime.Today, ct));

    [HttpGet("listado")]
    public async Task<ActionResult<IReadOnlyList<RemitoControlDto>>> Listado(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta,
        [FromQuery] int idDestino = 0, [FromQuery] string estado = "TODOS", CancellationToken ct = default)
        => Ok(await _service.ListadoAsync(desde, hasta, idDestino, estado ?? "TODOS", ct));

    [HttpGet("contenido")]
    public async Task<ActionResult<IReadOnlyList<EventoItemDto>>> Contenido(
        [FromQuery] string remitoId, [FromQuery] string origen, CancellationToken ct)
        => Ok(await _service.ContenidoAsync(remitoId ?? "", origen ?? "", ct));

    public sealed record ReasignarRequest(int DespachoId, int NuevoIdDestino);

    [HttpPost("reasignar")]
    public async Task<IActionResult> Reasignar([FromBody] ReasignarRequest req, CancellationToken ct)
    {
        await _service.ReasignarDestinoAsync(req.DespachoId, req.NuevoIdDestino, ct);
        return NoContent();
    }

    [HttpPost("eliminar-despacho/{despachoId:int}")]
    public async Task<IActionResult> EliminarDespacho(int despachoId, CancellationToken ct)
    {
        await _service.EliminarDespachoAsync(despachoId, ct);
        return NoContent();
    }

    [HttpGet("log-qr")]
    public async Task<ActionResult<IReadOnlyList<QrLogDto>>> LogQr(
        [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null, CancellationToken ct = default)
        => Ok(await _service.LogQrAsync(desde ?? DateTime.Today.AddDays(-6), hasta ?? DateTime.Today, ct));

    [HttpGet("qr-foto-info")]
    public async Task<ActionResult<QrFotoInfoDto>> QrFotoInfo(
        [FromQuery] string remitoId, [FromQuery] int idLocal, CancellationToken ct)
    {
        var info = await _service.FotoQrInfoAsync(remitoId ?? "", idLocal, ct);
        return info is null ? NotFound() : Ok(info);
    }

    [HttpGet("qr-foto")]
    public async Task<IActionResult> QrFoto([FromQuery] string remitoId, [FromQuery] int idLocal, CancellationToken ct)
    {
        var bytes = await _service.FotoQrBytesAsync(remitoId ?? "", idLocal, ct);
        return bytes is null ? NotFound() : File(bytes, "image/jpeg");
    }
}
