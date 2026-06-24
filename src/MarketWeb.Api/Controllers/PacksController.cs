using MarketWeb.Application.Packs;
using MarketWeb.Shared.Packs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

/// <summary>Reporte de packs (espejo de FrmRepoPack). Solo lectura + desarmar (baja lógica).</summary>
[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class PacksController : ControllerBase
{
    private readonly IPacksService _service;
    public PacksController(IPacksService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PackDto>>> Listar(
        [FromQuery] string? nroPedido, [FromQuery] string? codArt, [FromQuery] bool verDesarmados = false, CancellationToken ct = default)
        => Ok(await _service.ListarAsync(nroPedido, codArt, verDesarmados, ct));

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        var bytes = await _service.ObtenerPdfAsync(id, ct);
        return bytes is null || bytes.Length == 0 ? NotFound() : File(bytes, "application/pdf");
    }

    [HttpGet("{id:int}/txt")]
    public async Task<IActionResult> Txt(int id, CancellationToken ct)
    {
        var bytes = await _service.ObtenerTxtAsync(id, ct);
        return bytes is null || bytes.Length == 0 ? NotFound() : File(bytes, "text/plain; charset=utf-8");
    }

    [HttpDelete]
    public async Task<IActionResult> Eliminar([FromQuery] string nroPedido, CancellationToken ct)
    {
        var usuario = User.Identity?.Name
            ?? (Request.Headers.TryGetValue("X-Pc", out var pc) ? pc.ToString() : null)
            ?? "WEB";
        await _service.EliminarAsync(nroPedido, usuario, ct);
        return NoContent();
    }
}
