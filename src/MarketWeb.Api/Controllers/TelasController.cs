using MarketWeb.Application.Common;
using MarketWeb.Application.Telas;
using MarketWeb.Shared.Telas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

/// <summary>Telas (Producción): catálogo de telas + catálogos propios (depósitos/textiles).
/// Los colores se listan desde Dragonfish.</summary>
[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class TelasController : ControllerBase
{
    private readonly ITelasService _service;
    public TelasController(ITelasService service) => _service = service;

    private string Usuario()
        => User.Identity?.Name
           ?? (Request.Headers.TryGetValue("X-Pc", out var pc) ? pc.ToString() : null)
           ?? "WEB";

    // ---- Telas ----
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TelaDto>>> Listar(
        [FromQuery] int? idDeposito, [FromQuery] string? material, [FromQuery] int? idTextil, CancellationToken ct)
        => Ok(await _service.ListarAsync(idDeposito, material, idTextil, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TelaDto>> Obtener(int id, CancellationToken ct)
    {
        var t = await _service.ObtenerAsync(id, ct);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost]
    public async Task<ActionResult> Crear([FromBody] TelaSaveRequest req, CancellationToken ct)
    {
        try { return Ok(new { id = await _service.CrearAsync(req, Usuario(), ct) }); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Modificar(int id, [FromBody] TelaSaveRequest req, CancellationToken ct)
    {
        try { await _service.ModificarAsync(id, req, Usuario(), ct); return NoContent(); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Eliminar(int id, CancellationToken ct)
    {
        await _service.EliminarAsync(id, Usuario(), ct);
        return NoContent();
    }

    // ---- Colores (Dragonfish) ----
    [HttpGet("colores")]
    public async Task<ActionResult<IReadOnlyList<ColorDragonDto>>> Colores(CancellationToken ct)
        => Ok(await _service.ListarColoresAsync(ct));

    // ---- Catálogos propios (tipo = depositos | textiles) ----
    [HttpGet("catalogos/{tipo}")]
    public async Task<ActionResult<IReadOnlyList<CatalogoItemDto>>> ListarCatalogo(string tipo, CancellationToken ct)
    {
        try { return Ok(await _service.ListarCatalogoAsync(tipo, ct)); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPost("catalogos/{tipo}")]
    public async Task<ActionResult> CrearCatalogo(string tipo, [FromBody] CatalogoSaveRequest req, CancellationToken ct)
    {
        try { return Ok(new { id = await _service.CrearCatalogoAsync(tipo, req, Usuario(), ct) }); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPut("catalogos/{tipo}/{id:int}")]
    public async Task<ActionResult> ModificarCatalogo(string tipo, int id, [FromBody] CatalogoSaveRequest req, CancellationToken ct)
    {
        try { await _service.ModificarCatalogoAsync(tipo, id, req, Usuario(), ct); return NoContent(); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpDelete("catalogos/{tipo}/{id:int}")]
    public async Task<ActionResult> EliminarCatalogo(string tipo, int id, CancellationToken ct)
    {
        try { await _service.EliminarCatalogoAsync(tipo, id, Usuario(), ct); return NoContent(); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }
}
