using MarketWeb.Shared.Telas;

namespace MarketWeb.Application.Telas;

/// <summary>
/// Telas (Producción): catálogo de telas + catálogos editables de depósitos, textiles y colores.
/// Tablas MARKET.dbo.Telas / TelasDepositos / TelasTextiles / TelasColores. Baja lógica.
/// Stock y compras (Fase 2) no están acá todavía.
/// </summary>
public interface ITelasService
{
    // ---- Telas ----
    Task<IReadOnlyList<TelaDto>> ListarAsync(int? idDeposito, string? material, int? idTextil, int? idColor, CancellationToken ct = default);
    Task<TelaDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<int> CrearAsync(TelaSaveRequest req, string usuario, CancellationToken ct = default);
    Task ModificarAsync(int id, TelaSaveRequest req, string usuario, CancellationToken ct = default);
    Task EliminarAsync(int id, string usuario, CancellationToken ct = default);

    // ---- Catálogos (depositos / textiles / colores) ----
    Task<IReadOnlyList<CatalogoItemDto>> ListarCatalogoAsync(string tipo, CancellationToken ct = default);
    Task<int> CrearCatalogoAsync(string tipo, CatalogoSaveRequest req, string usuario, CancellationToken ct = default);
    Task ModificarCatalogoAsync(string tipo, int id, CatalogoSaveRequest req, string usuario, CancellationToken ct = default);
    Task EliminarCatalogoAsync(string tipo, int id, string usuario, CancellationToken ct = default);
}
