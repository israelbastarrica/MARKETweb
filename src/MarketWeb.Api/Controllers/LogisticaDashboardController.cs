using MarketWeb.Application.LogisticaDashboard;
using MarketWeb.Shared.LogisticaDashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

[Authorize(Policy = "Aprobado")]
[ApiController]
[Route("api/logistica-dashboard")]
public sealed class LogisticaDashboardController : ControllerBase
{
    private readonly ILogisticaDashboardService _service;
    public LogisticaDashboardController(ILogisticaDashboardService service) => _service = service;

    [HttpGet("panel1")]
    public async Task<ActionResult<PanelDespachoRecepcionDto>> Panel1(CancellationToken ct)
        => Ok(await _service.GetPanelDespachoRecepcionAsync(ct));

    [HttpGet("panel2")]
    public async Task<ActionResult<PanelPendientesDto>> Panel2(CancellationToken ct)
        => Ok(await _service.GetPanelPendientesAsync(ct));

    [HttpGet("panel3")]
    public async Task<ActionResult<PanelMapeosDto>> Panel3(CancellationToken ct)
        => Ok(await _service.GetPanelMapeosAsync(ct));

    [HttpGet("panel4")]
    public async Task<ActionResult<PanelVaciasDto>> Panel4(CancellationToken ct)
        => Ok(await _service.GetPanelVaciasAsync(ct));

    [HttpGet("panel5")]
    public async Task<ActionResult<PanelEstancadosDto>> Panel5(CancellationToken ct)
        => Ok(await _service.GetPanelEstancadosAsync(ct));

    [HttpGet("panel6")]
    public async Task<ActionResult<PanelPickingDto>> Panel6(CancellationToken ct)
        => Ok(await _service.GetPanelPickingAsync(ct));

    [HttpGet("panel7")]
    public async Task<ActionResult<PanelMasUbicDto>> Panel7(CancellationToken ct)
        => Ok(await _service.GetPanelMasUbicAsync(ct));

    [HttpGet("panel8")]
    public async Task<ActionResult<PanelReposicionDto>> Panel8(CancellationToken ct)
        => Ok(await _service.GetPanelReposicionAsync(ct));

    [HttpGet("panel9")]
    public async Task<ActionResult<PanelRojosDto>> Panel9(CancellationToken ct)
        => Ok(await _service.GetPanelRojosAsync(ct));
}
