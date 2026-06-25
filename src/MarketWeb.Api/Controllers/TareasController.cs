using MarketWeb.Application.Tareas;
using MarketWeb.Shared.Tareas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Admin")]
[ApiController]
[Route("api/tareas")]
public sealed class TareasController : ControllerBase
{
    private readonly ITareasService _service;
    private readonly TareasRunner _runner;

    public TareasController(ITareasService service, TareasRunner runner)
    {
        _service = service;
        _runner = runner;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TareaProgramadaDto>>> Listar(CancellationToken ct)
        => Ok(await _service.ListarAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TareaProgramadaEditorDto>> Obtener(int id, CancellationToken ct)
    {
        var t = await _service.ObtenerAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost("guardar")]
    public async Task<ActionResult<int>> Guardar([FromBody] TareaSaveRequest req, CancellationToken ct)
    {
        var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
        return Ok(await _service.GuardarAsync(req, usuario, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
        await _service.EliminarAsync(id, usuario, ct);
        return NoContent();
    }

    [HttpGet("{id:int}/historial")]
    public async Task<ActionResult<IReadOnlyList<TareaLogDto>>> Historial(int id, CancellationToken ct)
        => Ok(await _service.HistorialAsync(id, 20, ct));

    // Dispara la tarea ahora (en background). Devuelve si arrancó o si ya estaba corriendo.
    [HttpPost("{id:int}/ejecutar")]
    public IActionResult Ejecutar(int id)
    {
        var arrancada = _runner.Lanzar(id, "MANUAL");
        return Ok(new { arrancada });
    }

    // Reenvía solo el PDF + mail de la última corrida (sin re-correr el SP). Es rápido → se espera el resultado.
    [HttpPost("{id:int}/reenviar")]
    public async Task<IActionResult> Reenviar(int id, CancellationToken ct)
    {
        var (ok, resultado) = await _service.ReenviarReposicionAsync(id, ct);
        return Ok(new { ok, resultado });
    }
}
