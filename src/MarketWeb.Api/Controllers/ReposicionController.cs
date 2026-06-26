using MarketWeb.Application.Reposicion;
using MarketWeb.Shared.Reposicion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/reposicion")]
public sealed class ReposicionController : ControllerBase
{
    private readonly ReposicionJobs _jobs;
    private readonly IReposicionPdf _pdf;
    public ReposicionController(ReposicionJobs jobs, IReposicionPdf pdf)
    {
        _jobs = jobs;
        _pdf = pdf;
    }

    // Arranca la corrida (SP_RepoCalcularPacks, ~2 min, PERSISTE). Devuelve el id para hacer polling.
    [HttpPost("calcular")]
    public IActionResult Calcular([FromBody] ReposicionCalcularRequest req)
    {
        var jobId = _jobs.Start(req ?? new ReposicionCalcularRequest(), "MARKETWEB");
        return Ok(new { jobId });
    }

    // Estado/resultado de la corrida.
    [HttpGet("calcular/{jobId}")]
    public ActionResult<ReposicionJobDto> Estado(string jobId)
    {
        var job = _jobs.Get(jobId);
        return job is null ? NotFound() : Ok(job);
    }

    // PDF cuadernillo de la corrida (mismo formato que el desktop). Se abre en una pestaña nueva.
    [HttpGet("calcular/{jobId}/pdf")]
    public async Task<IActionResult> Pdf(string jobId, CancellationToken ct)
    {
        var datos = _jobs.Datos(jobId);
        if (datos is null) return NotFound();
        var bytes = await _pdf.GenerarAsync(datos.Value.Datos, datos.Value.Req.FechaCorte, ct);
        var nombre = $"Reposicion_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        Response.Headers.ContentDisposition = $"inline; filename=\"{nombre}\"";
        return File(bytes, "application/pdf");
    }

    // ---- Historial / Reimpresión de corridas guardadas ----

    [HttpGet("historial")]
    public async Task<ActionResult<IReadOnlyList<CorridaDto>>> Historial([FromServices] IReposicionService svc, CancellationToken ct)
        => Ok(await svc.ListarCorridasAsync(ct));

    // "Explain": por qué el sistema repone lo que repone (SP_RepoExplicarArticulo). Read-only.
    [HttpGet("explicar")]
    public async Task<ActionResult<ExplicarDto>> Explicar(
        [FromServices] IReposicionService svc, [FromQuery] string local, [FromQuery] string artCod,
        [FromQuery] bool historiaCompleta, CancellationToken ct)
        => Ok(await svc.ExplicarAsync(local ?? "", artCod ?? "", historiaCompleta, ct));

    // Resetea un artículo desde un remito (re-ancla la cuenta). Escribe en producción.
    [HttpPost("resetear")]
    public async Task<ActionResult<ResetResultadoDto>> Resetear(
        [FromServices] IReposicionService svc, [FromBody] ResetRemitoRequest req, CancellationToken ct)
    {
        var mail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
        var pc = Request.Headers["X-Pc"].ToString();
        var usuario = string.IsNullOrWhiteSpace(pc) ? mail : $"{mail} ({pc})";
        return Ok(await svc.ResetearDesdeRemitoAsync(req, usuario, ct));
    }

    public sealed record ResetEventoRequest(int IdEvento, string? Comentario);

    // Reset firmado desde un evento de piso (packs con signo, ancla al último remito). Escribe en producción.
    [HttpPost("resetear-evento")]
    public async Task<ActionResult<ResetResultadoDto>> ResetearEvento(
        [FromServices] IReposicionService svc, [FromBody] ResetEventoRequest req, CancellationToken ct)
    {
        var mail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
        var pc = Request.Headers["X-Pc"].ToString();
        var usuario = string.IsNullOrWhiteSpace(pc) ? mail : $"{mail} ({pc})";
        return Ok(await svc.ResetearDesdeEventoAsync(req.IdEvento, req.Comentario ?? "", usuario, ct));
    }

    // Reimprime el PDF de una corrida pasada (reconstruye desde el snapshot; read-only).
    [HttpGet("historial/{id:int}/pdf")]
    public async Task<IActionResult> HistorialPdf(int id, [FromServices] IReposicionService svc, CancellationToken ct)
    {
        var datos = await svc.ReconstruirCorridaAsync(id, ct);
        if (datos is null) return NotFound();
        var bytes = await _pdf.GenerarAsync(datos, null, ct);
        var nombre = $"Reposicion_corrida{id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        Response.Headers.ContentDisposition = $"inline; filename=\"{nombre}\"";
        return File(bytes, "application/pdf");
    }
}
