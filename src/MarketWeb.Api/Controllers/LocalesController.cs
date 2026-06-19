using MarketWeb.Application.Common;
using MarketWeb.Application.Locales;
using MarketWeb.Shared.Locales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class LocalesController : ControllerBase
{
    private readonly ILocalesService _service;

    public LocalesController(ILocalesService service) => _service = service;

    /// <summary>Listado de locales activos, filtrable por descripción.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocalDto>>> Listar(
        [FromQuery] string? filtro, CancellationToken ct)
        => Ok(await _service.ListarAsync(filtro, ct));

    /// <summary>Tipos de local para el combo del ABM.</summary>
    [HttpGet("tipos")]
    public async Task<ActionResult<IReadOnlyList<LocalTipoDto>>> Tipos(CancellationToken ct)
        => Ok(await _service.ListarTiposAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LocalDto>> Obtener(int id, CancellationToken ct)
    {
        var local = await _service.ObtenerAsync(id, ct);
        return local is null ? NotFound() : Ok(local);
    }

    [HttpPost]
    public async Task<ActionResult> Crear([FromBody] LocalSaveRequest req, CancellationToken ct)
    {
        try
        {
            var id = await _service.CrearAsync(req, ct);
            return CreatedAtAction(nameof(Obtener), new { id }, new { id });
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Modificar(int id, [FromBody] LocalSaveRequest req, CancellationToken ct)
    {
        try
        {
            await _service.ModificarAsync(id, req, ct);
            return NoContent();
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Eliminar(int id, CancellationToken ct)
    {
        await _service.EliminarAsync(id, ct);
        return NoContent();
    }
}
