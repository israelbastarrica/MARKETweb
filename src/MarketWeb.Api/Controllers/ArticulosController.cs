using MarketWeb.Application.Articulos;
using MarketWeb.Shared.Articulos;
using MarketWeb.Shared.Insumos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class ArticulosController : ControllerBase
{
    private readonly IArticulosService _service;

    public ArticulosController(IArticulosService service) => _service = service;

    [HttpGet("ubicaciones")]
    public async Task<ActionResult<IReadOnlyList<UbicacionDto>>> Ubicaciones(CancellationToken ct)
        => Ok(await _service.ListarUbicacionesAsync(ct));

    [HttpGet("consultar")]
    public async Task<ActionResult<ConsultaArticuloDto>> Consultar(
        [FromQuery] string codigo, [FromQuery] string ubicacion = "CENTRAL", CancellationToken ct = default)
    {
        var dto = await _service.ConsultarAsync(codigo, ubicacion, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("foto")]
    public async Task<IActionResult> Foto([FromQuery] string codigo, [FromQuery] bool ia = false, CancellationToken ct = default)
    {
        var bytes = await _service.ObtenerFotoAsync(codigo, ia, ct);
        return bytes is null || bytes.Length == 0 ? NotFound() : File(bytes, "image/jpeg");
    }

    /// <summary>Palets del depósito que contienen el artículo (búsqueda lenta, aparte).</summary>
    [HttpGet("palets")]
    public async Task<ActionResult<IReadOnlyList<UbicacionArtDto>>> Palets([FromQuery] string codigo, CancellationToken ct)
        => Ok(await _service.BuscarEnPaletsAsync(codigo, ct));
}
