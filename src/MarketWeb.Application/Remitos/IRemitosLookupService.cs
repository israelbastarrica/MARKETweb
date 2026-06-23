using MarketWeb.Shared.Dragonfish;

namespace MarketWeb.Application.Remitos;

public interface IRemitosLookupService
{
    Task<IReadOnlyList<UltimaRepoItemDto>> UltimaRepoAsync(string local, CancellationToken ct = default);
    Task<ArticuloLookupDto?> BuscarArticuloAsync(string cod, CancellationToken ct = default);
}
