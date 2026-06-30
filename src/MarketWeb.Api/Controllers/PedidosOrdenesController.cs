using MarketWeb.Application.Common;
using MarketWeb.Application.PedidosOrdenes;
using MarketWeb.Shared.PedidosOrdenes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

/// <summary>Órdenes de Pedido (Diseño) - listado/reporte filtrable + ABM (tabla PedidosOrdenes).</summary>
[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/pedidos-ordenes")]
public sealed class PedidosOrdenesController : ControllerBase
{
    private readonly IPedidosOrdenesService _service;
    public PedidosOrdenesController(IPedidosOrdenesService service) => _service = service;

    private string Usuario()
        => User.Identity?.Name
           ?? (Request.Headers.TryGetValue("X-Pc", out var pc) ? pc.ToString() : null)
           ?? "WEB";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PedidoOrdenListaDto>>> Listar([FromQuery] PedidoOrdenFiltro filtro, CancellationToken ct)
        => Ok(await _service.ListarAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PedidoOrdenDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await _service.ObtenerAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult> Crear([FromBody] PedidoOrdenSaveRequest req, CancellationToken ct)
    {
        try { return Ok(new { id = await _service.CrearAsync(req, Usuario(), ct) }); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Modificar(int id, [FromBody] PedidoOrdenSaveRequest req, CancellationToken ct)
    {
        try { await _service.ModificarAsync(id, req, Usuario(), ct); return NoContent(); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Eliminar(int id, CancellationToken ct)
    {
        try { await _service.EliminarAsync(id, Usuario(), ct); return NoContent(); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpGet("resolver-articulo")]
    public async Task<ActionResult<ArticuloDragonDto>> ResolverArticulo([FromQuery] string artcod, CancellationToken ct)
        => Ok(await _service.ResolverArticuloAsync(artcod, ct));

    [HttpGet("estados")]
    public async Task<ActionResult<IReadOnlyList<string>>> Estados(CancellationToken ct)
        => Ok(await _service.EstadosAsync(ct));

    [HttpGet("equivalencias-talles")]
    public async Task<ActionResult<IReadOnlyList<EquivalenciaTalleDto>>> EquivalenciasTalles(CancellationToken ct)
        => Ok(await _service.EquivalenciasTallesAsync(ct));

    [HttpGet("proveedores")]
    public async Task<ActionResult<IReadOnlyList<ProveedorOrdenDto>>> Proveedores(CancellationToken ct)
        => Ok(await _service.ProveedoresAsync(ct));
}
