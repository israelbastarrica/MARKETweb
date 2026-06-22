using MarketWeb.Application.Mapa;
using MarketWeb.Shared.Mapa;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

// Datos del Mapa 3D del depósito (visor Babylon en wwwroot/mapa3d). Porteo del backend Node deposito-3d.
[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class MapaController : ControllerBase
{
    private readonly IMapaService _svc;
    public MapaController(IMapaService svc) => _svc = svc;

    // Todos los módulos con cantidad de artículos (para colorear verde/rojo).
    [HttpGet("modulos")]
    public async Task<ActionResult<IReadOnlyList<MapaModuloDto>>> Modulos(CancellationToken ct)
        => Ok(await _svc.ModulosAsync(ct));

    // Artículos de un módulo específico.
    [HttpGet("modulo/{modulo}")]
    public async Task<ActionResult<MapaModuloDetalleDto>> Modulo(string modulo, CancellationToken ct)
        => Ok(await _svc.ModuloAsync(modulo, ct));

    // Módulos vacíos (sin MapeoRegistro activo).
    [HttpGet("vacios")]
    public async Task<ActionResult<IReadOnlyList<string>>> Vacios(CancellationToken ct)
        => Ok(await _svc.VaciosAsync(ct));

    // Busca artículos (descripción contiene / código prefijo) → módulos donde están.
    [HttpGet("buscar")]
    public async Task<ActionResult<IReadOnlyList<string>>> Buscar([FromQuery] string? q, CancellationToken ct)
        => Ok(await _svc.BuscarAsync(q, ct));
}
