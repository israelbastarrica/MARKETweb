using MarketWeb.Shared.Reposicion;

namespace MarketWeb.Application.Reposicion;

public interface IReporteArticulosService
{
    /// <summary>Listas para los combos de filtro (Dragonfish CENTRAL).</summary>
    Task<ReporteArticulosCombosDto> CombosAsync(CancellationToken ct = default);

    /// <summary>Reporte de artículos (sp_ConsultaArticulos) con foco en packs (Logística).</summary>
    Task<IReadOnlyList<ArticuloReporteDto>> ListarAsync(ReporteArticulosFiltro filtro, CancellationToken ct = default);

    /// <summary>Edición masiva de packs (upsert en ArticulosDatosAdiciones) sobre los artículos elegidos.</summary>
    Task<int> GuardarPacksAsync(GuardarPacksRequest req, string usuario, CancellationToken ct = default);
}
