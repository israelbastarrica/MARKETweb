using MarketWeb.Shared.LogisticaDashboard;

namespace MarketWeb.Application.LogisticaDashboard;

/// <summary>Bundle de las fuentes "rápidas" de reposición (TTL 60s): día operativo,
/// venta de hoy y cobertura en vivo por local. La cobertura incluye los rojos
/// (los reusa el panel 9).</summary>
public sealed class ReposFast
{
    public Dictionary<string, RepoDiaOpDto> DiaOp { get; set; } = new();
    public Dictionary<string, int> VentaHoy { get; set; } = new();
    public Dictionary<string, RepoCoberturaDto> Cobertura { get; set; } = new();
}
