using MarketWeb.Shared.Reemplazos;

namespace MarketWeb.Application.Reemplazos;

public interface IReemplazosService
{
    /// <summary>Locales (Ubicaciones tipo LOCAL) para el combo.</summary>
    Task<IReadOnlyList<LocalReemplazoDto>> ListarLocalesAsync(CancellationToken ct = default);

    /// <summary>Listado de reemplazos. idUbicacion 0 = TODOS; verTodos=false → solo no procesados.</summary>
    Task<IReadOnlyList<ReemplazoDto>> ListarAsync(int idUbicacion, bool verTodos, CancellationToken ct = default);

    /// <summary>Reemplazo para el editor (modificación).</summary>
    Task<ReemplazoEditorDto?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Descripción + combo de un artículo de Dragonfish.</summary>
    Task<ArticuloDescDto?> DescripcionArticuloAsync(string artCod, CancellationToken ct = default);

    /// <summary>Valida que el artículo original esté en una posición reponible del local.</summary>
    Task<ValidacionReemplazoDto> ValidarOriginalAsync(int idUbicacion, string artCod, CancellationToken ct = default);

    /// <summary>Buscador automático: candidatos de reemplazo en depósito, rankeados por match.</summary>
    Task<IReadOnlyList<ReemplazoCandidatoDto>> BuscarCandidatosAsync(int idUbicacion, string artCod, CancellationToken ct = default);

    /// <summary>Variante "PASAR A PERCHERO": candidatos que ya están en Perchero del local.</summary>
    Task<IReadOnlyList<ReemplazoCandidatoDto>> BuscarCandidatosPercheroAsync(int idUbicacion, string artCod, CancellationToken ct = default);

    /// <summary>Alta/modificación: resuelve IDMapeoLocal/Logistica y valida duplicados.</summary>
    Task GuardarAsync(ReemplazoSaveRequest req, string usuario, CancellationToken ct = default);

    /// <summary>Baja lógica.</summary>
    Task EliminarAsync(int id, string usuario, CancellationToken ct = default);
}
