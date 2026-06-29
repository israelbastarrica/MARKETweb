using MarketWeb.Application.Common;
using MarketWeb.Application.Telas;
using MarketWeb.Shared.Telas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

/// <summary>Telas (Producción) - stock por rollo + tablero (por pedido / depósito / color) + ABM.</summary>
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

    // ---- Catálogos (combos) ----
    [HttpGet("catalogos/{tipo}")]
    public async Task<ActionResult<IReadOnlyList<CatalogoItemDto>>> Catalogo(string tipo, CancellationToken ct)
    {
        try { return Ok(await _service.ListarCatalogoAsync(tipo, ct)); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    // ---- Tablero ----
    [HttpGet("stock-depositos")]
    public async Task<ActionResult<IReadOnlyList<DepoStockDto>>> StockDepositos(CancellationToken ct)
        => Ok(await _service.StockPorDepositoAsync(ct));

    [HttpGet("resumen-pedidos")]
    public async Task<ActionResult<IReadOnlyList<PedidoBarraDto>>> ResumenPedidos([FromQuery] int? idDeposito, [FromQuery] int top = 20, CancellationToken ct = default)
        => Ok(await _service.ResumenPorPedidoAsync(idDeposito, top, ct));

    [HttpGet("deposito/{idDeposito:int}/materiales")]
    public async Task<ActionResult<IReadOnlyList<DepoMaterialDto>>> MaterialesDeposito(int idDeposito, CancellationToken ct)
        => Ok(await _service.MaterialesPorDepositoAsync(idDeposito, ct));

    [HttpGet("deposito/{idDeposito:int}/material/{idMaterial:int}/colores")]
    public async Task<ActionResult<IReadOnlyList<ColorStockDto>>> ColoresStock(int idDeposito, int idMaterial, CancellationToken ct)
        => Ok(await _service.ColoresStockAsync(idDeposito, idMaterial, ct));

    // ---- ABM de stock (rollos) ----
    [HttpGet("rollos")]
    public async Task<ActionResult<IReadOnlyList<TelaRolloDto>>> Rollos(
        [FromQuery] int? idDeposito, [FromQuery] int? idMaterial, [FromQuery] int? idColor,
        [FromQuery] int? idTelera, [FromQuery] string? numPedido, [FromQuery] bool sinColor, CancellationToken ct)
        => Ok(await _service.ListarRollosAsync(idDeposito, idMaterial, idColor, idTelera, numPedido, sinColor, ct));

    [HttpPost("rollos")]
    public async Task<ActionResult> CrearRollo([FromBody] RolloSaveRequest req, CancellationToken ct)
    {
        try { return Ok(new { id = await _service.CrearRolloAsync(req, Usuario(), ct) }); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPut("rollos/{id:int}")]
    public async Task<ActionResult> ModificarRollo(int id, [FromBody] RolloSaveRequest req, CancellationToken ct)
    {
        try { await _service.ModificarRolloAsync(id, req, Usuario(), ct); return NoContent(); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpDelete("rollos/{id:int}")]
    public async Task<ActionResult> EliminarRollo(int id, CancellationToken ct)
    {
        await _service.EliminarRolloAsync(id, Usuario(), ct);
        return NoContent();
    }
}
