using MarketWeb.Shared.Dragonfish;

namespace MarketWeb.Application.Dragonfish;

public interface IDragonfishService
{
    bool Configurado { get; }

    /// <summary>Crea un Remito de venta CENTRAL→local en Dragonfish (mercadería en tránsito). Devuelve la respuesta cruda.</summary>
    Task<DragonRemitoResultDto> CrearRemitoAsync(DragonRemitoRequest req, CancellationToken ct = default);

    /// <summary>Diagnóstico: crea un remito inyectando campos extra de primer nivel (para ver cuál de "observaciones" persiste Dragon).</summary>
    Task<DragonRemitoResultDto> CrearRemitoConExtrasAsync(DragonRemitoRequest req, IDictionary<string, object?>? extras, CancellationToken ct = default);
}
