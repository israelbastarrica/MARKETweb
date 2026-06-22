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
}
