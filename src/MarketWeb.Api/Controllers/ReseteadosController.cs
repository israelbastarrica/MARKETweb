using MarketWeb.Application.Reposicion;
using MarketWeb.Shared.Reposicion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/reseteados")]
public sealed class ReseteadosController : ControllerBase
{
    private readonly IReseteadosService _service;
    public ReseteadosController(IReseteadosService service) => _service = service;

    [HttpGet("mobiliarios")]
    public async Task<ActionResult<IReadOnlyList<string>>> Mobiliarios(CancellationToken ct)
        => Ok(await _service.ListarMobiliariosAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReseteadoDto>>> Listar(
        [FromQuery] string local = "TODOS", [FromQuery] string mobiliario = "TODOS", [FromQuery] string artCod = "", CancellationToken ct = default)
        => Ok(await _service.ListarAsync(local ?? "TODOS", mobiliario ?? "TODOS", artCod ?? "", ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReseteadoEditorDto>> Obtener(int id, CancellationToken ct)
    {
        var r = await _service.ObtenerAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("guardar")]
    public async Task<ActionResult<int>> Guardar([FromBody] ReseteadoSaveRequest req, CancellationToken ct)
        => Ok(await _service.GuardarAsync(req, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await _service.EliminarAsync(id, ct);
        return NoContent();
    }
}
