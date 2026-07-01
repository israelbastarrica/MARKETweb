using MarketWeb.Application.Produccion;
using MarketWeb.Shared.Produccion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/catalogos")]
public sealed class CatalogosController : ControllerBase
{
    private readonly ICatalogosService _service;
    public CatalogosController(ICatalogosService service) => _service = service;

    private string Usuario => User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
    private string Aud => $"Catálogos web | {Usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CatalogoDto>>> Listar(CancellationToken ct)
        => Ok(await _service.ListarAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatalogoDetalleDto>> Detalle(int id, CancellationToken ct)
    {
        var c = await _service.DetalleAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpGet("combos")]
    public async Task<ActionResult<CatalogoCombosDto>> Combos(CancellationToken ct)
        => Ok(await _service.CombosAsync(ct));

    [HttpGet("articulo/{codigo}")]
    public async Task<ActionResult<CatalogoRenglonDto>> Articulo(string codigo, CancellationToken ct)
    {
        var it = await _service.ResolverArticuloAsync(codigo, ct);
        return it is null ? NotFound() : Ok(it);
    }

    [HttpGet("pedidos-ordenes")]
    public async Task<ActionResult<IReadOnlyList<PedidoOrdenSelDto>>> PedidosOrdenes([FromQuery] string? filtro, CancellationToken ct)
        => Ok(await _service.PedidosOrdenesAsync(filtro, ct));

    [HttpGet("proforma/{nro:int}")]
    public async Task<ActionResult<IReadOnlyList<CatalogoRenglonDto>>> Proforma(int nro, CancellationToken ct)
        => Ok(await _service.ProformaAsync(nro, ct));

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        var bytes = await _service.ObtenerPdfAsync(id, ct);
        if (bytes is null) return NotFound();
        // Inline (sin nombre de archivo) → se abre en el navegador en vez de descargarse.
        return File(bytes, "application/pdf");
    }

    [HttpPost("{id:int}/generar-pdf")]
    public async Task<ActionResult<bool>> GenerarPdf(int id, CancellationToken ct)
    {
        var bytes = await _service.GenerarPdfAsync(id, ct);
        return Ok(bytes is { Length: > 0 });
    }

    [HttpPost("guardar")]
    public async Task<ActionResult<int>> Guardar([FromBody] CatalogoGuardarRequest req, CancellationToken ct)
        => Ok(await _service.GuardarAsync(req, Aud, ct));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<bool>> Eliminar(int id, CancellationToken ct)
        => Ok(await _service.EliminarAsync(id, Aud, ct));
}
