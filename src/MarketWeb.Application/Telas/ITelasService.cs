using MarketWeb.Shared.Telas;

namespace MarketWeb.Application.Telas;

/// <summary>
/// Telas (Producción): catálogo de telas + catálogos propios de depósitos y textiles.
/// Los COLORES salen de Dragonfish (DRAGONFISH_CENTRAL.Zoologic.DPCOLOR), no de una tabla propia.
/// Tablas MARKET.dbo.Telas / TelasDepositos / TelasTextiles. Baja lógica. Stock/compras = Fase 2.
/// </summary>
public interface ITelasService
{
    // ---- Telas ----
    Task<IReadOnlyList<TelaDto>> ListarAsync(int? idDeposito, string? material, int? idTextil, CancellationToken ct = default);
    Task<TelaDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<int> CrearAsync(TelaSaveRequest req, string usuario, CancellationToken ct = default);
    Task ModificarAsync(int id, TelaSaveRequest req, string usuario, CancellationToken ct = default);
    Task EliminarAsync(int id, string usuario, CancellationToken ct = default);

    // ---- Catálogos propios (depositos / textiles) ----
    Task<IReadOnlyList<CatalogoItemDto>> ListarCatalogoAsync(string tipo, CancellationToken ct = default);
    Task<int> CrearCatalogoAsync(string tipo, CatalogoSaveRequest req, string usuario, CancellationToken ct = default);
    Task ModificarCatalogoAsync(string tipo, int id, CatalogoSaveRequest req, string usuario, CancellationToken ct = default);
    Task EliminarCatalogoAsync(string tipo, int id, string usuario, CancellationToken ct = default);

    // ---- Colores desde Dragonfish ----
    Task<IReadOnlyList<ColorDragonDto>> ListarColoresAsync(CancellationToken ct = default);
}
