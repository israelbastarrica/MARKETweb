using MarketWeb.Shared.Dragonfish;

namespace MarketWeb.Application.Remitos;

public interface IRemitosLookupService
{
    Task<IReadOnlyList<UltimaRepoItemDto>> UltimaRepoAsync(string local, CancellationToken ct = default);
    Task<ArticuloLookupDto?> BuscarArticuloAsync(string cod, CancellationToken ct = default);
    Task<BolsaDto?> BuscarBolsaAsync(string nroBolsa, CancellationToken ct = default);
    Task<IReadOnlyList<BolsaRenglonDto>> BuscarRemitoLocalAsync(string local, int punto, int numero, CancellationToken ct = default);
    Task<IReadOnlyList<MotivoDto>> MotivosAsync(CancellationToken ct = default);

    /// <summary>
    /// Registra el mapeo tablet→remito al dar el alta (Dragon NO persiste InformacionAdicional/ZADSFW).
    /// El agente de impresión lee esta tabla por N° de remito para rutear la impresora. Idempotente (crea la tabla si falta).
    /// </summary>
    Task RegistrarRemitoTabletAsync(long? numero, string? codigo, string local, string? licencia,
        string? dispositivoPc, string? motivo, string? usuario, CancellationToken ct = default);
}
