using MarketWeb.Application.Palets;
using MarketWeb.Shared.Palets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class PaletsController : ControllerBase
{
    private readonly IPaletsService _service;
    public PaletsController(IPaletsService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaletDto>>> Listar(
        [FromQuery] string? nroPalet, [FromQuery] string? codArticulo,
        [FromQuery] string? tipo, [FromQuery] string? categoria,
        [FromQuery] bool verDesarmados, [FromQuery] DateTime desde, CancellationToken ct)
        => Ok(await _service.ListarAsync(nroPalet, codArticulo, tipo, categoria, verDesarmados, desde, ct));

    [HttpGet("tipos")]
    public async Task<ActionResult<IReadOnlyList<string>>> Tipos(CancellationToken ct)
        => Ok(await _service.ListarTiposAsync(ct));

    [HttpGet("categorias")]
    public async Task<ActionResult<IReadOnlyList<string>>> Categorias(CancellationToken ct)
        => Ok(await _service.ListarCategoriasAsync(ct));

    [HttpGet("{id:int}/articulos")]
    public async Task<ActionResult<IReadOnlyList<PaletArticuloDto>>> Articulos(int id, CancellationToken ct)
        => Ok(await _service.ListarArticulosAsync(id, ct));

    [HttpPost("{id:int}/desarmar")]
    public async Task<ActionResult> Desarmar(int id, CancellationToken ct)
    {
        await _service.DesarmarAsync(id, ct);
        return NoContent();
    }
}
