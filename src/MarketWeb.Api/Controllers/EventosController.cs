using MarketWeb.Application.Reposicion;
using MarketWeb.Shared.Reposicion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/eventos")]
public sealed class EventosController : ControllerBase
{
    private readonly IEventosService _service;
    public EventosController(IEventosService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EventoDto>>> Listar(
        [FromQuery] string local = "TODOS", [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null,
        [FromQuery] bool verTodos = false, CancellationToken ct = default)
    {
        var d = desde ?? DateTime.Today.AddDays(-30);
        var h = hasta ?? DateTime.Today;
        return Ok(await _service.ListarAsync(local ?? "TODOS", d, h, verTodos, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EventoDetalleDto>> Detalle(int id, CancellationToken ct)
    {
        var d = await _service.DetalleAsync(id, ct);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpGet("{id:int}/foto")]
    public async Task<IActionResult> Foto(int id, CancellationToken ct)
    {
        var bytes = await _service.FotoAsync(id, ct);
        return bytes is null ? NotFound() : File(bytes, "image/jpeg");
    }

    [HttpPost("{id:int}/procesar")]
    public async Task<IActionResult> Procesar(int id, CancellationToken ct)
    {
        await _service.MarcarProcesadoAsync(id, ct);
        return NoContent();
    }

    public sealed record AccionRequest(string Accion);

    [HttpPost("{id:int}/accion")]
    public async Task<IActionResult> Accion(int id, [FromBody] AccionRequest req, CancellationToken ct)
    {
        await _service.GuardarAccionAsync(id, req?.Accion ?? "", ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await _service.EliminarAsync(id, ct);
        return NoContent();
    }

    // ---- Motivos normalizados (catálogo + asignación al evento) ----

    [HttpGet("motivos")]
    public async Task<ActionResult<IReadOnlyList<MotivoEventoDto>>> Motivos(CancellationToken ct)
        => Ok(await _service.ListarMotivosAsync(ct));

    public sealed record MotivoNuevoRequest(string Nombre);

    [HttpPost("motivos")]
    public async Task<ActionResult<MotivoEventoDto>> CrearMotivo([FromBody] MotivoNuevoRequest req, CancellationToken ct)
    {
        var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
        return Ok(await _service.CrearMotivoAsync(req?.Nombre ?? "", usuario, ct));
    }

    public sealed record MotivoEventoRequest(int IdMotivo);

    [HttpPost("{id:int}/motivo")]
    public async Task<IActionResult> GuardarMotivo(int id, [FromBody] MotivoEventoRequest req, CancellationToken ct)
    {
        await _service.GuardarMotivoAsync(id, req?.IdMotivo ?? 0, ct);
        return NoContent();
    }
}
