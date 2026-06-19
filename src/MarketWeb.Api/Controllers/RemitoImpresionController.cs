using MarketWeb.Application.RemitoImpresion;
using MarketWeb.Shared.RemitoImpresion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

// Cola de impresión de remitos (modo Locales: filtra por LocalOrigen). Un local solo
// ve/re-encola lo suyo; ADMIN ve todos.
[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class RemitoImpresionController : ControllerBase
{
    private readonly IRemitoImpresionService _service;
    public RemitoImpresionController(IRemitoImpresionService service) => _service = service;

    [HttpGet("locales")]
    public async Task<ActionResult<IReadOnlyList<string>>> Locales(CancellationToken ct)
        => Ok(await _service.ListarLocalesAsync(ct));

    // Devuelve el LocalOrigen forzado por perfil (o null si ADMIN/otro), respetando el filtro pedido.
    private async Task<string?> ResolverOrigenAsync(string? localPedido, CancellationToken ct)
    {
        var perfil = (User.FindFirst("perfil")?.Value ?? "").Trim();
        // LOGISTICA → origen CENTRAL (el grueso de la cola). Local → su propio origen. ADMIN → lo pedido.
        if (perfil.Equals("LOGISTICA", StringComparison.OrdinalIgnoreCase)) return "CENTRAL";
        var locales = await _service.ListarLocalesAsync(ct);
        var propio = locales.FirstOrDefault(l => string.Equals(l, perfil, StringComparison.OrdinalIgnoreCase));
        return propio ?? localPedido;   // si es un local, forzado; si no, lo que pidió (o null=todos)
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RemitoColaDto>>> Listar(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta, [FromQuery] string? local,
        [FromQuery] string? estado, [FromQuery] bool soloErrores, CancellationToken ct)
    {
        var origen = await ResolverOrigenAsync(local, ct);
        return Ok(await _service.ListarAsync(desde, hasta, origen, estado, soloErrores, ct));
    }

    [HttpPost("{id:int}/reimprimir")]
    public async Task<ActionResult> Reimprimir(int id, CancellationToken ct)
    {
        var origen = await ResolverOrigenAsync(null, ct); // local-user: solo lo suyo
        var ok = await _service.ReimprimirAsync(id, origen, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("estado")]
    public async Task<ActionResult<IReadOnlyList<RemitoEstadoDto>>> Estado([FromQuery] string ids, CancellationToken ct)
    {
        var lista = (ids ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToList();
        return Ok(await _service.EstadoAsync(lista, ct));
    }
}
