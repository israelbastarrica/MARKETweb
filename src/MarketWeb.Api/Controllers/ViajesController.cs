using MarketWeb.Application.Produccion;
using MarketWeb.Shared.Produccion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/viajes")]
public sealed class ViajesController : ControllerBase
{
    private readonly IViajesService _service;
    public ViajesController(IViajesService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ViajeDto>>> Listar(CancellationToken ct)
        => Ok(await _service.ListarViajesAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ViajeDto>> Viaje(int id, CancellationToken ct)
    {
        var v = await _service.ViajeAsync(id, ct);
        return v is null ? NotFound() : Ok(v);
    }

    [HttpGet("{id:int}/articulos")]
    public async Task<ActionResult<IReadOnlyList<ViajeArticuloDto>>> Articulos(int id, CancellationToken ct)
        => Ok(await _service.ArticulosAsync(id, ct));

    [HttpGet("{id:int}/proveedores")]
    public async Task<ActionResult<IReadOnlyList<ViajeProveedorDto>>> Proveedores(int id, CancellationToken ct)
        => Ok(await _service.ProveedoresAsync(id, ct));

    [HttpGet("{id:int}/contenedores")]
    public async Task<ActionResult<IReadOnlyList<ViajeContenedorDto>>> Contenedores(int id, CancellationToken ct)
        => Ok(await _service.ContenedoresAsync(id, ct));

    [HttpGet("articulo/{idArticulo:int}")]
    public async Task<ActionResult<ViajeArticuloFichaDto>> Ficha(int idArticulo, CancellationToken ct)
    {
        var f = await _service.FichaAsync(idArticulo, ct);
        return f is null ? NotFound() : Ok(f);
    }

    // ---- ABM ----
    private string Usuario => User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";

    [HttpPost("guardar")]
    public async Task<ActionResult<int>> GuardarViaje([FromBody] ViajeSaveRequest req, CancellationToken ct)
        => Ok(await _service.GuardarViajeAsync(req, Usuario, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> EliminarViaje(int id, CancellationToken ct)
    {
        await _service.EliminarViajeAsync(id, Usuario, ct);
        return NoContent();
    }

    [HttpGet("contenedor/{id:int}")]
    public async Task<ActionResult<ContenedorEditorDto>> Contenedor(int id, CancellationToken ct)
    {
        var c = await _service.ObtenerContenedorAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost("contenedor")]
    public async Task<IActionResult> GuardarContenedor([FromBody] ContenedorSaveRequest req, CancellationToken ct)
    {
        await _service.GuardarContenedorAsync(req, Usuario, ct);
        return Ok();
    }

    [HttpDelete("contenedor/{id:int}")]
    public async Task<IActionResult> EliminarContenedor(int id, CancellationToken ct)
    {
        await _service.EliminarContenedorAsync(id, Usuario, ct);
        return NoContent();
    }

    [HttpGet("proveedor/{id:int}")]
    public async Task<ActionResult<ProveedorEditorDto>> Proveedor(int id, CancellationToken ct)
    {
        var p = await _service.ObtenerProveedorAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost("proveedor")]
    public async Task<IActionResult> GuardarProveedor([FromBody] ProveedorEditorDto req, CancellationToken ct)
    {
        await _service.GuardarProveedorAsync(req, Usuario, ct);
        return Ok();
    }

    [HttpDelete("proveedor/{id:int}")]
    public async Task<IActionResult> EliminarProveedor(int id, CancellationToken ct)
    {
        await _service.EliminarProveedorAsync(id, Usuario, ct);
        return NoContent();
    }

    [HttpGet("articulo/{id:int}/editor")]
    public async Task<ActionResult<ArticuloEditorDto>> ArticuloEditor(int id, CancellationToken ct)
    {
        var a = await _service.ObtenerArticuloEditorAsync(id, ct);
        return a is null ? NotFound() : Ok(a);
    }

    [HttpPost("articulo")]
    public async Task<ActionResult<int>> GuardarArticulo([FromBody] ArticuloEditorDto req, CancellationToken ct)
        => Ok(await _service.GuardarArticuloAsync(req, Usuario, ct));

    [HttpDelete("articulo/{id:int}")]
    public async Task<IActionResult> EliminarArticulo(int id, CancellationToken ct)
    {
        await _service.EliminarArticuloAsync(id, Usuario, ct);
        return NoContent();
    }

    public sealed record CodigoDragonRequest(string Codigo);

    [HttpPost("articulo/{idArticulo:int}/codigo-dragon")]
    public async Task<IActionResult> GuardarCodigoDragon(int idArticulo, [FromBody] CodigoDragonRequest req, CancellationToken ct)
    {
        var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
        await _service.GuardarCodigoDragonAsync(idArticulo, req?.Codigo ?? "", usuario, ct);
        return NoContent();
    }

    [HttpGet("foto")]
    public IActionResult Foto([FromQuery] string archivo)
    {
        var path = _service.FotoFullPath(archivo ?? "");
        if (path is null) return NotFound();
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext switch { ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif", _ => "image/jpeg" };
        return PhysicalFile(path, mime);
    }

    [HttpPost("importar")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ImportarViajeResultadoDto>> Importar(CancellationToken ct)
    {
        var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
        return Ok(await _service.ImportarAsync(usuario, ct));
    }

    // ---- Equivalencias código proveedor → código MARKET (Dragon) ----

    [HttpGet("codigos-market/count")]
    public async Task<ActionResult<int>> CodigosMarketCount(CancellationToken ct)
        => Ok(await _service.ContarCodigosMarketAsync(ct));

    public sealed record CodigosMarketRequest(string Texto);

    [HttpPost("codigos-market")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ImportarCodigosResultadoDto>> CodigosMarketImportar([FromBody] CodigosMarketRequest req, CancellationToken ct)
    {
        var usuario = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
        return Ok(await _service.ImportarCodigosMarketAsync(req?.Texto ?? "", usuario, ct));
    }
}
