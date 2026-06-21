using MarketWeb.Application.Common;
using MarketWeb.Application.Reemplazos;
using MarketWeb.Shared.Reemplazos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/reemplazos")]
public sealed class ReemplazosController : ControllerBase
{
    private readonly IReemplazosService _service;
    public ReemplazosController(IReemplazosService service) => _service = service;

    [HttpGet("locales")]
    public async Task<ActionResult<IReadOnlyList<LocalReemplazoDto>>> Locales(CancellationToken ct)
        => Ok(await _service.ListarLocalesAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReemplazoDto>>> Listar(
        [FromQuery] int idUbicacion = 0, [FromQuery] bool verTodos = false, CancellationToken ct = default)
        => Ok(await _service.ListarAsync(idUbicacion, verTodos, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReemplazoEditorDto>> Obtener(int id, CancellationToken ct)
    {
        var r = await _service.ObtenerAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpGet("articulo")]
    public async Task<ActionResult<ArticuloDescDto>> Articulo([FromQuery] string cod, CancellationToken ct)
    {
        var a = await _service.DescripcionArticuloAsync(cod ?? "", ct);
        return a is null ? NotFound() : Ok(a);
    }

    [HttpGet("validar")]
    public async Task<ActionResult<ValidacionReemplazoDto>> Validar(
        [FromQuery] int idUbicacion, [FromQuery] string cod, CancellationToken ct)
        => Ok(await _service.ValidarOriginalAsync(idUbicacion, cod ?? "", ct));

    [HttpGet("candidatos")]
    public async Task<ActionResult<IReadOnlyList<ReemplazoCandidatoDto>>> Candidatos(
        [FromQuery] int idUbicacion, [FromQuery] string cod, CancellationToken ct)
        => Ok(await _service.BuscarCandidatosAsync(idUbicacion, cod ?? "", ct));

    [HttpGet("candidatos-perchero")]
    public async Task<ActionResult<IReadOnlyList<ReemplazoCandidatoDto>>> CandidatosPerchero(
        [FromQuery] int idUbicacion, [FromQuery] string cod, CancellationToken ct)
        => Ok(await _service.BuscarCandidatosPercheroAsync(idUbicacion, cod ?? "", ct));

    [HttpPost("guardar")]
    public async Task<IActionResult> Guardar([FromBody] ReemplazoSaveRequest req, CancellationToken ct)
    {
        try
        {
            var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
            await _service.GuardarAsync(req, usuario, ct);
            return Ok();
        }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
        await _service.EliminarAsync(id, usuario, ct);
        return NoContent();
    }
}
