using MarketWeb.Shared.RemitoImpresion;

namespace MarketWeb.Application.RemitoImpresion;

/// <summary>
/// Cola de impresión de remitos (espejo de frmRemitoImpresion, ModoLocales=True).
/// En modo Locales se filtra por LocalOrigen. La reimpresión re-encola (Estado=PENDIENTE)
/// y un servicio externo procesa la cola hacia las impresoras térmicas.
/// </summary>
public interface IRemitoImpresionService
{
    Task<IReadOnlyList<string>> ListarLocalesAsync(CancellationToken ct = default);

    /// <summary>Impresoras (SALTAFW + IP) de la cola, para el selector "esta PC" de logística.</summary>
    Task<IReadOnlyList<ImpresoraColaDto>> ListarImpresorasAsync(CancellationToken ct = default);

    Task<IReadOnlyList<RemitoColaDto>> ListarAsync(
        DateTime desde, DateTime hasta, string? localOrigen, string? estado, bool soloErrores, int? saltafw, CancellationToken ct = default);

    /// <summary>Re-encola el remito. Si se pasa localOrigen, solo lo permite si el remito es de ese origen.</summary>
    Task<bool> ReimprimirAsync(int id, string? localOrigen, CancellationToken ct = default);

    Task<IReadOnlyList<RemitoEstadoDto>> EstadoAsync(IReadOnlyList<int> ids, CancellationToken ct = default);

    /// <summary>Anula el remito: deja un pedido de RECHAZO en RemitoRecepcion (Accion='RECHAZAR') para que el agente lo rechace en el destino.</summary>
    Task<bool> AnularRemitoAsync(int id, CancellationToken ct = default);
}
