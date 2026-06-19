using MarketWeb.Application.Common;
using MarketWeb.Application.TiposLocal;
using MarketWeb.Shared.Locales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class TiposLocalesController : ControllerBase
{
    private readonly ITiposLocalService _service;

    public TiposLocalesController(ITiposLocalService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocalTipoDto>>> Listar(
        [FromQuery] string? filtro, CancellationToken ct)
        => Ok(await _service.ListarAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LocalTipoDto>> Obtener(int id, CancellationToken ct)
    {
        var tipo = await _service.ObtenerAsync(id, ct);
        return tipo is null ? NotFound() : Ok(tipo);
    }

    [HttpPost]
    public async Task<ActionResult> Crear([FromBody] TipoLocalSaveRequest req, CancellationToken ct)
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
    public async Task<ActionResult> Modificar(int id, [FromBody] TipoLocalSaveRequest req, CancellationToken ct)
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
