using MarketWeb.Application.Produccion;
using MarketWeb.Shared.Produccion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/ordenes")]
public sealed class OrdenesController : ControllerBase
{
    private readonly IOrdenesService _service;
    public OrdenesController(IOrdenesService service) => _service = service;

    private string Usuario => User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "WEB";
    private string Aud => $"Órdenes web | {Usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrdenDto>>> Listar(CancellationToken ct)
        => Ok(await _service.ListarAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrdenDetalleDto>> Detalle(int id, CancellationToken ct)
    {
        var o = await _service.DetalleAsync(id, ct);
        return o is null ? NotFound() : Ok(o);
    }

    [HttpPost("importar-muestra")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ImportarOrdenesResultadoDto>> ImportarMuestra(CancellationToken ct)
        => Ok(await _service.ImportarMuestraAsync(Aud, ct));

    [HttpPost("importar/{nroOrden:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ImportarOrdenesResultadoDto>> ImportarOrden(int nroOrden, CancellationToken ct)
        => Ok(await _service.ImportarOrdenAsync(nroOrden, Aud, ct));

    [HttpGet("combos")]
    public async Task<ActionResult<IReadOnlyList<ComboRangoDto>>> Combos(CancellationToken ct)
        => Ok(await _service.ListarCombosAsync(ct));

    [HttpGet("combos-cabecera")]
    public async Task<ActionResult<OrdenCabeceraCombosDto>> CombosCabecera(CancellationToken ct)
        => Ok(await _service.CombosCabeceraAsync(ct));

    [HttpGet("colores-tela")]
    public async Task<ActionResult<IReadOnlyList<TelaColorDto>>> ColoresTela(CancellationToken ct)
        => Ok(await _service.ListarColoresTelaAsync(ct));

    [HttpGet("renglon/{idRenglon:int}/colores")]
    public async Task<ActionResult<IReadOnlyList<OrdenColorDto>>> Colores(int idRenglon, CancellationToken ct)
        => Ok(await _service.ColoresAsync(idRenglon, ct));

    [HttpPost("renglon/{idRenglon:int}/colores")]
    public async Task<ActionResult> GuardarColores(int idRenglon, [FromBody] List<OrdenColorDto> colores, CancellationToken ct)
    {
        await _service.GuardarColoresAsync(idRenglon, colores ?? new(), Aud, ct);
        return Ok();
    }

    [HttpGet("renglon/{idRenglon:int}/produccion")]
    public async Task<ActionResult<IReadOnlyList<OrdenProduccionCeldaDto>>> Produccion(int idRenglon, CancellationToken ct)
        => Ok(await _service.ProduccionAsync(idRenglon, ct));

    [HttpPost("renglon/{idRenglon:int}/produccion")]
    public async Task<ActionResult> GuardarProduccion(int idRenglon, [FromBody] List<OrdenProduccionCeldaDto> celdas, CancellationToken ct)
    {
        await _service.GuardarProduccionAsync(idRenglon, celdas ?? new(), Aud, ct);
        return Ok();
    }

    [HttpPost("guardar")]
    public async Task<ActionResult<int>> Guardar([FromBody] OrdenSaveRequest req, CancellationToken ct)
        => Ok(await _service.GuardarCabeceraAsync(req, Aud, ct));

    [HttpPost("renglon")]
    public async Task<ActionResult<bool>> GuardarRenglon([FromBody] OrdenRenglonSaveRequest req, CancellationToken ct)
        => Ok(await _service.GuardarRenglonAsync(req, Aud, ct));

    [HttpDelete("renglon/{id:int}")]
    public async Task<ActionResult<bool>> EliminarRenglon(int id, CancellationToken ct)
        => Ok(await _service.EliminarRenglonAsync(id, Aud, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<bool>> Eliminar(int id, CancellationToken ct)
        => Ok(await _service.EliminarOrdenAsync(id, Aud, ct));
}
