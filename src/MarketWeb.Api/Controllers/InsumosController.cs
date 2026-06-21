using MarketWeb.Application.Insumos;
using MarketWeb.Shared.Insumos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class InsumosController : ControllerBase
{
    private readonly IInsumosService _service;

    public InsumosController(IInsumosService service) => _service = service;

    [HttpGet("ubicaciones")]
    public async Task<ActionResult<IReadOnlyList<UbicacionDto>>> Ubicaciones(CancellationToken ct)
        => Ok(await _service.ListarUbicacionesAsync(ct));

    [HttpGet("pedidos")]
    public async Task<ActionResult<IReadOnlyList<PedidoInsumoDto>>> Pedidos(
        [FromQuery] int? ubicacionId, [FromQuery] string estado = "SIN ENVIAR", CancellationToken ct = default)
        => Ok(await _service.ListarPedidosAsync(ubicacionId, estado, ct));

    [HttpGet("consumos")]
    public async Task<ActionResult<IReadOnlyList<InsumoConsumoDto>>> Consumos(
        [FromQuery] int? ubicacionId, [FromQuery] string estado = "SIN MARCAR", CancellationToken ct = default)
        => Ok(await _service.ListarConsumosAsync(ubicacionId, estado, ct));

    public sealed record MarcarResultado(int Marcados);

    [HttpPost("marcar")]
    public async Task<ActionResult<MarcarResultado>> Marcar([FromBody] MarcarInsumosRequest req, CancellationToken ct)
    {
        var usuario = User.Identity?.Name ?? "WEB";
        var n = await _service.MarcarAsync(req?.Refs ?? new(), usuario, ct);
        return Ok(new MarcarResultado(n));
    }

    // ---- ABM de pedidos (LOCALES) ----

    [HttpGet("pedido/validar-fecha")]
    public async Task<ActionResult<ValidacionFechaDto>> ValidarFecha([FromQuery] int idLocal, CancellationToken ct)
        => Ok(await _service.ValidarFechaPedidoAsync(idLocal, ct));

    [HttpGet("pedido/{id:int}")]
    public async Task<ActionResult<PedidoEditorDto>> ObtenerPedido(int id, CancellationToken ct)
    {
        var ped = await _service.ObtenerEditorAsync(id, ct);
        return ped is null ? NotFound() : Ok(ped);
    }

    [HttpGet("articulos-insumo")]
    public async Task<ActionResult<IReadOnlyList<ArticuloInsumoDto>>> ArticulosInsumo([FromQuery] string q, CancellationToken ct)
        => Ok(await _service.BuscarArticulosInsumoAsync(q ?? "", ct));

    [HttpPost("pedido/guardar")]
    public async Task<ActionResult<CrearPedidoResultado>> GuardarPedido([FromBody] GuardarPedidoRequest req, CancellationToken ct)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "WEB";
            // El rol decide el lock y si se persiste existencia/cant. enviada (no se confía en el cliente).
            var perfil = (User.FindFirst("perfil")?.Value ?? "").Trim().ToUpperInvariant();
            var esDeposito = perfil is "LOGISTICA" or "ADMIN";
            return Ok(await _service.GuardarPedidoAsync(req, usuario, esDeposito, ct));
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("pedido/{id:int}")]
    public async Task<IActionResult> EliminarPedido(int id, CancellationToken ct)
    {
        var ok = await _service.EliminarPedidoAsync(id, User.Identity?.Name ?? "WEB", ct);
        return ok ? NoContent() : BadRequest("No se puede eliminar un pedido enviado. Comuníquese con Logística.");
    }

    // ---- DEPÓSITO / LOGÍSTICA ----

    [HttpPost("pedido/imprimir-armado")]
    public async Task<ActionResult<ArmadoInsumosDto>> ImprimirArmado([FromBody] ImprimirArmadoRequest req, CancellationToken ct)
    {
        var usuario = User.Identity?.Name ?? "WEB";
        return Ok(await _service.ImprimirArmadoAsync(req?.UbicacionId, usuario, ct));
    }

    public sealed record ImprimirArmadoRequest(int? UbicacionId);
}
