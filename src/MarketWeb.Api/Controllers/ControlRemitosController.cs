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
    public ControlRemitosController(IControlRemitosService service) => _service = service;

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
}
