namespace MarketWeb.Shared.Insumos;

/// <summary>Pedido para marcar consumos como procesados (Administración).
/// Refs en formato "R|id" / "P|id" (REGISTRO / PEDIDO).</summary>
public sealed class MarcarInsumosRequest
{
    public List<string> Refs { get; set; } = new();
}
