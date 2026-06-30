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

    /// <summary>Reemplazos de mesa con acción "PASAR A PERCHERO" (con flag de si ya los tomó un perchero). idUbicacion 0 = TODOS.</summary>
    Task<IReadOnlyList<MesaPercheroDto>> MesasParaPercheroAsync(int idUbicacion, CancellationToken ct = default);

    /// <summary>Alta/modificación: resuelve IDMapeoLocal/Logistica y valida duplicados.</summary>
    Task GuardarAsync(ReemplazoSaveRequest req, string usuario, CancellationToken ct = default);

    /// <summary>Marca como procesados los no procesados del filtro (con reemplazo) y avisa por mail a los locales.</summary>
    Task<MarcarProcesadosResultadoDto> MarcarProcesadosAsync(int idUbicacion, string usuario, CancellationToken ct = default);

    /// <summary>Baja lógica.</summary>
    Task EliminarAsync(int id, string usuario, CancellationToken ct = default);

    // ---- Reemplazo por Mueble (bloqueos) ----

    /// <summary>Mobiliarios distintos vigentes en Mapeo (para el combo).</summary>
    Task<IReadOnlyList<string>> ListarMobiliariosAsync(CancellationToken ct = default);

    /// <summary>Listado de bloqueos por mueble. local/mobiliario "" o "TODOS" = sin filtro; artCod = LIKE.</summary>
    Task<IReadOnlyList<BloqueoMuebleDto>> ListarBloqueosAsync(string local, string mobiliario, string artCod, CancellationToken ct = default);

    Task<BloqueoMuebleEditorDto?> ObtenerBloqueoAsync(int id, CancellationToken ct = default);

    /// <summary>Alta/modificación de un bloqueo (resuelve IDUbicacion; dedup por local+mueble+artículo en alta).</summary>
    Task GuardarBloqueoAsync(BloqueoMuebleSaveRequest req, string usuario, CancellationToken ct = default);

    Task EliminarBloqueoAsync(int id, string usuario, CancellationToken ct = default);
}
