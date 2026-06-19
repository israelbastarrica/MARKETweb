using MarketWeb.Application.Common;
using MarketWeb.Application.UsuariosPc;
using MarketWeb.Shared.UsuariosPc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Admin")]
[ApiController]
[Route("api/[controller]")]
public sealed class UsuariosPcController : ControllerBase
{
    private readonly IUsuariosPcService _service;

    public UsuariosPcController(IUsuariosPcService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioPcDto>>> Listar(
        [FromQuery] string? filtro, CancellationToken ct)
        => Ok(await _service.ListarAsync(filtro, ct));

    [HttpGet("perfiles")]
    public async Task<ActionResult<IReadOnlyList<string>>> Perfiles(CancellationToken ct)
        => Ok(await _service.ListarPerfilesAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UsuarioPcDto>> Obtener(int id, CancellationToken ct)
    {
        var u = await _service.ObtenerAsync(id, ct);
        return u is null ? NotFound() : Ok(u);
    }

    [HttpPost]
    public async Task<ActionResult> Crear([FromBody] UsuarioPcSaveRequest req, CancellationToken ct)
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
    public async Task<ActionResult> Modificar(int id, [FromBody] UsuarioPcSaveRequest req, CancellationToken ct)
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

    /// <summary>Aprueba el match mail↔PC de un usuario pendiente.</summary>
    [HttpPut("{id:int}/aprobar")]
    public async Task<ActionResult> Aprobar(int id, CancellationToken ct)
    {
        await _service.AprobarAsync(id, ct);
        return NoContent();
    }
}
