using MarketWeb.Shared.Telas;

namespace MarketWeb.Application.Telas;

/// <summary>
/// Telas (Producción) - stock por rollo. Tablas TelasRollos + catálogos
/// (TelasMateriales/TelasColores/TelasColoresEquivalencias/TelasDepositos/TelasTeleras).
/// Baja lógica, consultas parametrizadas.
/// </summary>
public interface ITelasService
{
    // Combos / catálogos (solo lectura para selectores)
    Task<IReadOnlyList<CatalogoItemDto>> ListarCatalogoAsync(string tipo, CancellationToken ct = default);
    // Alta de catálogo (material: solo nombre; depósito/telera: código + nombre)
    Task<int> CrearCatalogoAsync(string tipo, string? codigo, string nombre, string usuario, CancellationToken ct = default);

    // Tablero
    Task<IReadOnlyList<DepoStockDto>> StockPorDepositoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PedidoBarraDto>> ResumenPorPedidoAsync(int? idDeposito, int top, CancellationToken ct = default);
    Task<IReadOnlyList<DepoMaterialDto>> MaterialesPorDepositoAsync(int idDeposito, CancellationToken ct = default);
    Task<IReadOnlyList<ColorStockDto>> ColoresStockAsync(int idDeposito, int idMaterial, CancellationToken ct = default);

    // ABM de stock (rollos)
    Task<IReadOnlyList<TelaRolloDto>> ListarRollosAsync(int? idDeposito, int? idMaterial, int? idColor, int? idTelera, string? numPedido, bool sinColor = false, CancellationToken ct = default);
    Task<int> CrearRolloAsync(RolloSaveRequest req, string usuario, CancellationToken ct = default);
    Task ModificarRolloAsync(int id, RolloSaveRequest req, string usuario, CancellationToken ct = default);
    Task EliminarRolloAsync(int id, string usuario, CancellationToken ct = default);
}
