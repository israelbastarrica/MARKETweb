using MarketWeb.Shared.Dragonfish;

namespace MarketWeb.Application.Dragonfish;

public interface IDragonfishService
{
    bool Configurado { get; }

    /// <summary>Crea un Remito de venta CENTRAL→local en Dragonfish (mercadería en tránsito). Devuelve la respuesta cruda.</summary>
    Task<DragonRemitoResultDto> CrearRemitoAsync(DragonRemitoRequest req, CancellationToken ct = default);
}
