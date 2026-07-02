using MarketWeb.Application.Reposicion;
using MarketWeb.Shared.Reposicion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/repo-grupos")]
public sealed class GruposUnificadosController : ControllerBase
{
    private readonly IGruposUnificadosService _service;
    public GruposUnificadosController(IGruposUnificadosService service) => _service = service;

    private string Usuario => User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
    private string Aud => $"Grupos unificados web | {Usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GrupoUnificadoDto>>> Listar(CancellationToken ct)
        => Ok(await _service.ListarAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GrupoUnificadoDetalleDto>> Detalle(int id, CancellationToken ct)
    {
        var g = await _service.DetalleAsync(id, ct);
        return g is null ? NotFound() : Ok(g);
    }

    [HttpGet("articulo/{codigo}")]
    public async Task<ActionResult<GrupoArticuloDto>> Articulo(string codigo, CancellationToken ct)
    {
        var it = await _service.ResolverArticuloAsync(codigo, ct);
        return it is null ? NotFound() : Ok(it);
    }

    [HttpPost("guardar")]
    public async Task<ActionResult<int>> Guardar([FromBody] GrupoUnificadoGuardarRequest req, CancellationToken ct)
        => Ok(await _service.GuardarAsync(req, Aud, ct));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<bool>> Eliminar(int id, CancellationToken ct)
        => Ok(await _service.EliminarAsync(id, Aud, ct));
}
