using MarketWeb.Shared.Insumos;

namespace MarketWeb.Application.Insumos;

public interface IInsumosService
{
    /// <summary>Ubicaciones activas para el combo de filtro.</summary>
    Task<IReadOnlyList<UbicacionDto>> ListarUbicacionesAsync(CancellationToken ct = default);

    /// <summary>Genera los remitos de insumos (uno por local, Motivo 13, CENTRAL→local) de los pedidos EN ARMADO no enviados; marca ENVIADO.</summary>
    Task<GenerarRemitosResultado> GenerarRemitosAsync(int? ubicacionId, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Pedidos de insumos (cabecera + totales). Filtros:
    ///  - ubicacionId: null = TODOS, -1 = LOCALES (Luro+Peralta), &gt;0 = una ubicación.
    ///  - estado: "SIN ENVIAR" | "ENVIADOS" | "TODOS".
    /// </summary>
    Task<IReadOnlyList<PedidoInsumoDto>> ListarPedidosAsync(int? ubicacionId, string estado, CancellationToken ct = default);

    /// <summary>
    /// Consumos consolidados por proveedor (Administración). Filtros:
    ///  - ubicacionId: null = TODOS, -1 = LOCALES, &gt;0 = una ubicación.
    ///  - estado: "SIN MARCAR" | "MARCADOS" | "TODOS" (contra PedidosInsumosAdministracion).
    /// </summary>
    Task<IReadOnlyList<InsumoConsumoDto>> ListarConsumosAsync(int? ubicacionId, string estado, CancellationToken ct = default);

    /// <summary>
    /// Marca consumos como procesados (los inserta en PedidosInsumosAdministracion).
    /// refs en formato "R|id" / "P|id". Idempotente (WHERE NOT EXISTS). Devuelve la cantidad insertada.
    /// </summary>
    Task<int> MarcarAsync(IEnumerable<string> refs, string usuario, CancellationToken ct = default);

    // ---- ABM de pedidos (LOCALES) ----

    /// <summary>Valida la regla 1/15 + pase de gracia por quincena para un local (2=LURO, 3=PERALTA).</summary>
    Task<ValidacionFechaDto> ValidarFechaPedidoAsync(int idLocal, CancellationToken ct = default);

    /// <summary>Pedido completo (cabecera + renglones) para el editor.</summary>
    Task<PedidoEditorDto?> ObtenerEditorAsync(int idPedido, CancellationToken ct = default);

    /// <summary>Busca artículos de insumo (Dragonfish ART TIPOARTI='IS') por descripción. TOP 50.</summary>
    Task<IReadOnlyList<ArticuloInsumoDto>> BuscarArticulosInsumoAsync(string busqueda, CancellationToken ct = default);

    /// <summary>
    /// Guarda el pedido COMPLETO en una transacción (cabecera + renglones). Id=0 crea; Id&gt;0 reemplaza renglones.
    /// Re-valida 1/15 en altas. Devuelve id + nro. Nada se persiste hasta este llamado.
    /// </summary>
    Task<CrearPedidoResultado> GuardarPedidoAsync(GuardarPedidoRequest req, string usuario, bool esDeposito, CancellationToken ct = default);

    /// <summary>Borra (lógico) un pedido entero (cabecera + renglones). Devuelve false si no se permite (ENVIADO).</summary>
    Task<bool> EliminarPedidoAsync(int idPedido, string usuario, CancellationToken ct = default);

    // ---- DEPÓSITO / LOGÍSTICA ----

    /// <summary>
    /// Hoja de ruta de armado: toma los pedidos PENDIENTES (sin imprimir ni enviar) del
    /// filtro de ubicación, los marca EN ARMADO (FechaImpresion) — cerrándolos para el local —
    /// y devuelve su detalle para imprimir. Transaccional.
    /// </summary>
    Task<ArmadoInsumosDto> ImprimirArmadoAsync(int? ubicacionId, string usuario, CancellationToken ct = default);
}
