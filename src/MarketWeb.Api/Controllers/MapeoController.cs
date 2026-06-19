using MarketWeb.Application.Common;
using MarketWeb.Application.Mapeo;
using MarketWeb.Shared.Mapeo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class MapeoController : ControllerBase
{
    private readonly IMapeoService _service;
    public MapeoController(IMapeoService service) => _service = service;

    // ---- Ubicaciones ----
    [HttpGet("ubicaciones")]
    public async Task<ActionResult<IReadOnlyList<MapeoUbicacionDto>>> Ubicaciones(CancellationToken ct)
        => Ok(await _service.ListarUbicacionesAsync(ct));

    // ---- Combos ----
    [HttpGet("sectores")]
    public async Task<ActionResult<IReadOnlyList<string>>> Sectores(CancellationToken ct)
        => Ok(await _service.ListarSectoresAsync(ct));

    [HttpGet("mobiliarios")]
    public async Task<ActionResult<IReadOnlyList<string>>> Mobiliarios(CancellationToken ct)
        => Ok(await _service.ListarMobiliariosAsync(ct));

    // ---- Posiciones ----
    [HttpGet("posiciones")]
    public async Task<ActionResult<IReadOnlyList<MapeoPosicionDto>>> Posiciones([FromQuery] int idUbicacion, CancellationToken ct)
        => Ok(await _service.ListarPosicionesAsync(idUbicacion, ct));

    [HttpGet("posiciones/{id:int}")]
    public async Task<ActionResult<MapeoPosicionDto>> Posicion(int id, CancellationToken ct)
    {
        var p = await _service.ObtenerPosicionAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost("posiciones")]
    public async Task<ActionResult> CrearPosicion([FromBody] MapeoPosicionSaveRequest req, CancellationToken ct)
    {
        try { return Ok(new { id = await _service.CrearPosicionAsync(req, ct) }); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPut("posiciones/{id:int}")]
    public async Task<ActionResult> ModificarPosicion(int id, [FromBody] MapeoPosicionSaveRequest req, CancellationToken ct)
    {
        try { await _service.ModificarPosicionAsync(id, req, ct); return NoContent(); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpDelete("posiciones/{id:int}")]
    public async Task<ActionResult> EliminarPosicion(int id, CancellationToken ct)
    {
        await _service.EliminarPosicionAsync(id, ct);
        return NoContent();
    }

    // ---- Artículos por posición ----
    [HttpGet("articulos")]
    public async Task<ActionResult<IReadOnlyList<MapeoArticuloDto>>> Articulos([FromQuery] int idMapeo, CancellationToken ct)
        => Ok(await _service.ListarArticulosAsync(idMapeo, ct));

    [HttpPost("articulos")]
    public async Task<ActionResult> CrearArticulo([FromBody] MapeoArticuloSaveRequest req, CancellationToken ct)
    {
        try { return Ok(new { id = await _service.CrearArticuloAsync(req, ct) }); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPut("articulos/{id:int}")]
    public async Task<ActionResult> ModificarArticulo(int id, [FromBody] MapeoArticuloSaveRequest req, CancellationToken ct)
    {
        try { await _service.ModificarArticuloAsync(id, req, ct); return NoContent(); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpDelete("articulos/{id:int}")]
    public async Task<ActionResult> EliminarArticulo(int id, CancellationToken ct)
    {
        await _service.EliminarArticuloAsync(id, ct);
        return NoContent();
    }

    // ---- Reporte (Logística) ----
    [HttpGet("reporte/ubicaciones")]
    public async Task<ActionResult<IReadOnlyList<MapeoUbicacionDto>>> ReporteUbicaciones(CancellationToken ct)
        => Ok(await _service.ListarUbicacionesReporteAsync(ct));

    [HttpGet("reporte/tipos")]
    public async Task<ActionResult<IReadOnlyList<string>>> ReporteTipos(CancellationToken ct)
        => Ok(await _service.ListarTiposAsync(ct));

    [HttpGet("reporte/categorias")]
    public async Task<ActionResult<IReadOnlyList<string>>> ReporteCategorias(CancellationToken ct)
        => Ok(await _service.ListarCategoriasAsync(ct));

    [HttpPost("reporte")]
    public async Task<ActionResult<IReadOnlyList<MapeoReporteDto>>> Reporte([FromBody] MapeoReporteRequest req, CancellationToken ct)
        => Ok(await _service.ReporteAsync(req, ct));
}
