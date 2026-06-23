using MarketWeb.Application.Common;
using MarketWeb.Application.ConfigImagenes;
using MarketWeb.Shared.ConfigImagenes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

/// <summary>
/// Catálogo de imágenes de Diseño (espejo de frmRepoCatalogosConfigImagenes +
/// frmABMCatalogoConfigImagenes). La imagen se sirve como binario por /{id}/imagen.
/// </summary>
[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/[controller]")]
public sealed class ConfigImagenesController : ControllerBase
{
    private readonly IConfigImagenesService _service;
    public ConfigImagenesController(IConfigImagenesService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConfigImagenDto>>> Listar(
        [FromQuery] string? tipo, [FromQuery] string? descripcion, CancellationToken ct)
        => Ok(await _service.ListarAsync(tipo, descripcion, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConfigImagenDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await _service.ObtenerAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Devuelve los bytes de la imagen para mostrarla en un &lt;img&gt;.</summary>
    [HttpGet("{id:int}/imagen")]
    public async Task<IActionResult> Imagen(int id, CancellationToken ct)
    {
        var bytes = await _service.ObtenerImagenAsync(id, ct);
        if (bytes is null || bytes.Length == 0) return NotFound();
        return File(bytes, DetectarContentType(bytes));
    }

    [HttpPost]
    public async Task<ActionResult> Crear([FromBody] ConfigImagenSaveRequest req, CancellationToken ct)
    {
        try { return Ok(new { id = await _service.CrearAsync(req, Usuario(), ct) }); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Modificar(int id, [FromBody] ConfigImagenSaveRequest req, CancellationToken ct)
    {
        try { await _service.ModificarAsync(id, req, Usuario(), ct); return NoContent(); }
        catch (BusinessException ex) { return BadRequest(new { mensaje = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Eliminar(int id, CancellationToken ct)
    {
        await _service.EliminarAsync(id, Usuario(), ct);
        return NoContent();
    }

    // Auditoría: mail del SSO; fallback al nombre de PC del header X-Pc; por último WEB.
    private string Usuario()
        => User.Identity?.Name
           ?? (Request.Headers.TryGetValue("X-Pc", out var pc) ? pc.ToString() : null)
           ?? "WEB";

    private static string DetectarContentType(byte[] b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return "image/gif";
        if (b.Length >= 2 && b[0] == 0x42 && b[1] == 0x4D) return "image/bmp";
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
            && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return "image/webp";
        return "image/png";
    }
}
