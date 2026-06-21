using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Reemplazos;

namespace MarketWeb.Application.Reemplazos;

/// <summary>
/// Reemplazos de Logística (port fiel de frmRepoReemplazos / frmABMReemplazos).
/// Tabla MARKET.dbo.RepoReemplazos; descripciones y candidatos desde DRAGONFISH_CENTRAL.
/// </summary>
public sealed class ReemplazosService : IReemplazosService
{
    private readonly ISqlConnectionFactory _db;
    public ReemplazosService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<LocalReemplazoDto>> ListarLocalesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT U.ID AS Id, RTRIM(U.Descripcion) AS Descripcion
            FROM Ubicaciones U
            INNER JOIN UbicacionesTipo T ON U.IDTipo = T.ID
            WHERE U.Eliminado = 0 AND T.Eliminado = 0 AND T.Descripcion = 'LOCAL'
            ORDER BY U.Descripcion;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<LocalReemplazoDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<ReemplazoDto>> ListarAsync(int idUbicacion, bool verTodos, CancellationToken ct = default)
    {
        const string sql = """
            SELECT R.ID AS Id, R.Fecha,
                   RTRIM(ISNULL(U.Descripcion,'')) AS Ubicacion,
                   RTRIM(ISNULL(R.ARTCOD,'')) AS ArtCod,
                   RTRIM(ISNULL(ART.ARTDES,'')) AS DescripcionArt,
                   RTRIM(ISNULL(R.ARTCODReemplazo,'')) AS ArtCodReemplazo,
                   RTRIM(ISNULL(ART2.ARTDES,'')) AS DescripcionArtReemplazo,
                   RTRIM(MAP2.Modulo) AS UbicacionLocal,
                   RTRIM(MAP2.Mobiliario) AS MobiliarioLocal,
                   RTRIM(MAP.Modulo) AS UbicacionDeposito,
                   ISNULL(R.Accion,'') AS Accion,
                   CAST(CASE WHEN ISNULL(R.Procesado,0)=1 THEN 1 ELSE 0 END AS BIT) AS Procesado
            FROM RepoReemplazos R
            LEFT JOIN Ubicaciones U ON R.IDUbicacion = U.ID
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART  ON R.ARTCOD = ART.ARTCOD
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART2 ON R.ARTCODReemplazo = ART2.ARTCOD
            LEFT JOIN Mapeo MAP2 ON MAP2.ID = R.IDMapeoLocal
            LEFT JOIN Mapeo MAP  ON MAP.ID = R.IDMapeoLogistica
            WHERE R.Eliminado = 0 AND U.Eliminado = 0
              AND (@verTodos = 1 OR ISNULL(R.Procesado,0) = 0)
              AND (@idUbic = 0 OR R.IDUbicacion = @idUbic)
            ORDER BY R.Fecha DESC;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<ReemplazoDto>(new CommandDefinition(
            sql, new { verTodos = verTodos ? 1 : 0, idUbic = idUbicacion }, commandTimeout: 90, cancellationToken: ct))).ToList();
    }

    public async Task<ReemplazoEditorDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT R.ID AS Id, R.IDUbicacion AS IdUbicacion,
                   RTRIM(ISNULL(R.ARTCOD,'')) AS ArtCod,
                   RTRIM(ISNULL(A1.ARTDES,'')) AS DescripcionArt,
                   RTRIM(ISNULL(R.ARTCODReemplazo,'')) AS ArtCodReemplazo,
                   RTRIM(ISNULL(A2.ARTDES,'')) AS DescripcionArtReemplazo,
                   ISNULL(R.Accion,'') AS Accion
            FROM RepoReemplazos R
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART A1 ON R.ARTCOD = A1.ARTCOD
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART A2 ON R.ARTCODReemplazo = A2.ARTCOD
            WHERE R.ID = @id AND R.Eliminado = 0;
            """;
        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<ReemplazoEditorDto>(new CommandDefinition(sql, new { id }, commandTimeout: 60, cancellationToken: ct));
    }

    public async Task<ArticuloDescDto?> DescripcionArticuloAsync(string artCod, CancellationToken ct = default)
    {
        var c = (artCod ?? "").Trim();
        if (c.Length == 0) return null;
        const string sql = """
            SELECT TOP 1 RTRIM(ARTCOD) AS ArtCod, RTRIM(ISNULL(ARTDES,'')) AS Descripcion, RTRIM(ISNULL(CLASIFART,'')) AS Combo
            FROM DRAGONFISH_CENTRAL.Zoologic.ART WITH(NOLOCK)
            WHERE ARTCOD = @c;
            """;
        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<ArticuloDescDto>(new CommandDefinition(sql, new { c }, commandTimeout: 60, cancellationToken: ct));
    }

    public async Task<ValidacionReemplazoDto> ValidarOriginalAsync(int idUbicacion, string artCod, CancellationToken ct = default)
    {
        var c = (artCod ?? "").Trim();
        if (idUbicacion <= 0) return new ValidacionReemplazoDto { Ok = false, Mensaje = "Debe ingresar el local del reemplazo." };
        const string sql = """
            SELECT COUNT(*)
            FROM Mapeo MAP INNER JOIN MapeoRegistro REG ON MAP.ID = REG.idMapeo
            WHERE MAP.IDUbicacion = @idu AND REG.ARTCOD = @c AND ISNULL(MAP.NoReposicion,0) = 0;
            """;
        using var cn = _db.Create();
        var n = await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { idu = idUbicacion, c }, commandTimeout: 60, cancellationToken: ct));
        return n > 0
            ? new ValidacionReemplazoDto { Ok = true, Mensaje = "Validación - OK." }
            : new ValidacionReemplazoDto { Ok = false, Mensaje = "El artículo no está en posiciones reponibles del local." };
    }

    public async Task<IReadOnlyList<ReemplazoCandidatoDto>> BuscarCandidatosAsync(int idUbicacion, string artCod, CancellationToken ct = default)
    {
        var c = (artCod ?? "").Trim();
        if (c.Length == 0 || idUbicacion <= 0) return new List<ReemplazoCandidatoDto>();

        // Port del SQL de AbrirBuscadorReemplazosModal (parametrizado @CodOrig / @LocActual).
        const string sql = """
            DECLARE @Fam VARCHAR(50), @Gru VARCHAR(50), @Tip VARCHAR(50), @Cat VARCHAR(50), @Combo VARCHAR(50);
            SELECT @Fam=RTRIM(FAMILIA), @Gru=RTRIM(GRUPO), @Tip=RTRIM(TIPOARTI), @Cat=RTRIM(CATEARTI), @Combo=RTRIM(CLASIFART)
            FROM DRAGONFISH_CENTRAL.Zoologic.ART WHERE ARTCOD = @CodOrig;
            ;WITH Candidatos AS (
                SELECT DISTINCT A.ARTCOD, A.ARTDES, RTRIM(ISNULL(A.CLASIFART, 'SIN COMBO')) AS ComboStr,
                       (SELECT COUNT(*) FROM MARKET.dbo.Mapeo M INNER JOIN MARKET.dbo.MapeoRegistro R ON M.ID=R.IDMapeo
                        WHERE RTRIM(R.ARTCOD) = RTRIM(A.ARTCOD) AND M.IDUbicacion NOT IN (1, @LocActual) AND M.Eliminado=0 AND R.Eliminado=0) AS CantOtrosLocales,
                       CASE
                           WHEN A.FAMILIA=@Fam AND A.GRUPO=@Gru AND RTRIM(A.CLASIFART)=@Combo THEN 1
                           WHEN A.FAMILIA=@Fam AND RTRIM(A.CLASIFART)=@Combo THEN 2
                           WHEN RTRIM(A.CLASIFART)=@Combo THEN 3
                           WHEN A.FAMILIA=@Fam AND A.GRUPO=@Gru THEN 4
                           WHEN A.FAMILIA=@Fam THEN 5
                           ELSE 6 END AS NivelMatch
                FROM DRAGONFISH_CENTRAL.Zoologic.ART A WITH(NOLOCK)
                INNER JOIN MARKET.dbo.MapeoRegistro R_Dep WITH(NOLOCK) ON RTRIM(A.ARTCOD) = RTRIM(R_Dep.ARTCOD)
                INNER JOIN MARKET.dbo.Mapeo M_Dep WITH(NOLOCK) ON R_Dep.IDMapeo = M_Dep.ID
                WHERE A.ARTCOD <> @CodOrig AND M_Dep.IDUbicacion = 1 AND M_Dep.Eliminado = 0 AND R_Dep.Eliminado = 0
                  AND A.TIPOARTI = @Tip AND A.CATEARTI = @Cat
                  AND NOT EXISTS (SELECT 1 FROM MARKET.dbo.MapeoRegistro RL INNER JOIN MARKET.dbo.Mapeo ML ON RL.IDMapeo=ML.ID
                                  WHERE RTRIM(RL.ARTCOD)=RTRIM(A.ARTCOD) AND ML.IDUbicacion=@LocActual AND ML.Eliminado=0)
            ), Categorizados AS (
                SELECT ARTCOD, ARTDES, ComboStr,
                CASE
                    WHEN CantOtrosLocales = 0 AND NivelMatch = 1 THEN 1 WHEN CantOtrosLocales = 0 AND NivelMatch = 2 THEN 2
                    WHEN CantOtrosLocales = 0 AND NivelMatch = 3 THEN 3 WHEN CantOtrosLocales = 0 AND NivelMatch = 4 THEN 4
                    WHEN CantOtrosLocales = 0 AND NivelMatch = 5 THEN 5 WHEN CantOtrosLocales = 0 AND NivelMatch = 6 THEN 11
                    WHEN CantOtrosLocales > 0 AND NivelMatch = 1 THEN 6 WHEN CantOtrosLocales > 0 AND NivelMatch = 2 THEN 7
                    WHEN CantOtrosLocales > 0 AND NivelMatch = 3 THEN 8 WHEN CantOtrosLocales > 0 AND NivelMatch = 4 THEN 9
                    WHEN CantOtrosLocales > 0 AND NivelMatch = 5 THEN 10 WHEN CantOtrosLocales > 0 AND NivelMatch = 6 THEN 12
                END AS CatID
                FROM Candidatos WHERE NivelMatch > 0
            ), Titulos AS (
                SELECT DISTINCT CatID, '' AS ARTCOD,
                CASE CatID
                    WHEN 1 THEN '1 - SÓLO EN DEPÓSITO (IDEAL)' WHEN 2 THEN '1.1 - SÓLO EN DEPÓSITO (AFLOJA SUBFAMILIA)'
                    WHEN 3 THEN '1.2 - SÓLO EN DEPÓSITO (AFLOJA FAM Y SUBFAM)' WHEN 4 THEN '1.3 - SÓLO EN DEPÓSITO (AFLOJA COMBO)'
                    WHEN 5 THEN '1.4 - SÓLO EN DEPÓSITO (AFLOJA SUBFAM Y COMBO)'
                    WHEN 11 THEN '3 - SÓLO EN DEPÓSITO (SÓLO COINCIDE TIPO Y CATEGORÍA)'
                    WHEN 6 THEN '2 - EN DEPÓSITO Y OTRO LOCAL (IDEAL)' WHEN 7 THEN '2.1 - EN DEPÓSITO Y OTRO LOCAL (AFLOJA SUBFAMILIA)'
                    WHEN 8 THEN '2.2 - EN DEPÓSITO Y OTRO LOCAL (AFLOJA FAM Y SUBFAM)' WHEN 9 THEN '2.3 - EN DEPÓSITO Y OTRO LOCAL (AFLOJA COMBO)'
                    WHEN 10 THEN '2.4 - EN DEPÓSITO Y OTRO LOCAL (AFLOJA SUBFAM Y COMBO)'
                    WHEN 12 THEN '4 - EN DEPÓSITO Y OTRO LOCAL (SÓLO COINCIDE TIPO Y CATEGORÍA)'
                END AS Descripcion, '' AS Combo, 0 AS Stock, 1 AS EsTitulo
                FROM Categorizados
            ), Items AS (
                SELECT CatID, RTRIM(ARTCOD) AS ARTCOD, RTRIM(ARTDES) AS Descripcion, ComboStr AS Combo,
                CAST(ISNULL((SELECT SUM(ISNULL(COCANT, 0)) FROM DRAGONFISH_CENTRAL.ZooLogic.COMB C WITH(NOLOCK) WHERE RTRIM(C.COART) = RTRIM(Categorizados.ARTCOD)), 0) AS INT) AS Stock, 0 AS EsTitulo
                FROM Categorizados
            ), Todo AS (
                SELECT CatID, ARTCOD, Descripcion, Combo, Stock, EsTitulo FROM Titulos
                UNION ALL
                SELECT CatID, ARTCOD, Descripcion, Combo, Stock, EsTitulo FROM Items
            )
            SELECT CAST(EsTitulo AS BIT) AS EsTitulo, CatID AS CatId, ARTCOD AS ArtCod,
                   Descripcion, Combo, Stock
            FROM Todo
            ORDER BY CatID ASC, EsTitulo DESC, ARTCOD ASC;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<ReemplazoCandidatoDto>(new CommandDefinition(
            sql, new { CodOrig = c, LocActual = idUbicacion }, commandTimeout: 120, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<ReemplazoCandidatoDto>> BuscarCandidatosPercheroAsync(int idUbicacion, string artCod, CancellationToken ct = default)
    {
        var c = (artCod ?? "").Trim();
        if (c.Length == 0 || idUbicacion <= 0) return new List<ReemplazoCandidatoDto>();

        // Port del SQL de AbrirBuscadorPercheroLocalModal (artículos en Perchero del local).
        const string sql = """
            DECLARE @Fam VARCHAR(50), @Gru VARCHAR(50), @Tip VARCHAR(50), @Cat VARCHAR(50), @Combo VARCHAR(50);
            SELECT @Fam=RTRIM(FAMILIA), @Gru=RTRIM(GRUPO), @Tip=RTRIM(TIPOARTI), @Cat=RTRIM(CATEARTI), @Combo=RTRIM(CLASIFART)
            FROM DRAGONFISH_CENTRAL.Zoologic.ART WHERE ARTCOD = @CodOrig;
            SELECT DISTINCT CAST(0 AS BIT) AS EsTitulo, 0 AS CatId, RTRIM(A.ARTCOD) AS ArtCod,
                   RTRIM(A.ARTDES) AS Descripcion, RTRIM(ISNULL(A.CLASIFART, 'SIN COMBO')) AS Combo,
                   CAST(ISNULL((SELECT SUM(ISNULL(COCANT, 0)) FROM DRAGONFISH_CENTRAL.ZooLogic.COMB C WITH(NOLOCK) WHERE RTRIM(C.COART) = RTRIM(A.ARTCOD)), 0) AS INT) AS Stock,
                   RTRIM(M.Mobiliario) AS Mobiliario, RTRIM(ISNULL(M.Modulo, '')) AS Modulo
            FROM DRAGONFISH_CENTRAL.Zoologic.ART A WITH(NOLOCK)
            INNER JOIN MARKET.dbo.MapeoRegistro R WITH(NOLOCK) ON RTRIM(A.ARTCOD) = RTRIM(R.ARTCOD)
            INNER JOIN MARKET.dbo.Mapeo M WITH(NOLOCK) ON R.IDMapeo = M.ID
            WHERE A.ARTCOD <> @CodOrig
              AND M.IDUbicacion = @LocActual
              AND RTRIM(UPPER(M.Mobiliario)) = 'PERCHERO'
              AND M.Eliminado = 0 AND R.Eliminado = 0
              AND ISNULL(M.NoReposicion, 0) = 0
              AND A.TIPOARTI = @Tip
              AND A.CATEARTI = @Cat
              AND EXISTS (SELECT 1 FROM MARKET.dbo.RepoReemplazos RR WITH(NOLOCK)
                          WHERE RTRIM(RR.ARTCOD) = RTRIM(A.ARTCOD)
                            AND RR.IDUbicacion = @LocActual
                            AND ISNULL(RR.Eliminado, 0) = 0
                            AND ISNULL(RR.Procesado, 0) = 0)
            ORDER BY ArtCod ASC;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<ReemplazoCandidatoDto>(new CommandDefinition(
            sql, new { CodOrig = c, LocActual = idUbicacion }, commandTimeout: 120, cancellationToken: ct))).ToList();
    }

    public async Task GuardarAsync(ReemplazoSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var artOrig = (req.ArtCod ?? "").Trim().ToUpperInvariant();
        var artReemp = (req.ArtCodReemplazo ?? "").Trim().ToUpperInvariant();
        var accion = (req.Accion ?? "").Trim();
        if (artOrig.Length == 0) throw new BusinessException("Debe ingresar el código del artículo a reemplazar.");
        if (artReemp.Length == 0) throw new BusinessException("Debe ingresar el código del artículo de reemplazo.");
        if (accion.Length == 0) throw new BusinessException("Debe ingresar una acción de reemplazo.");
        if (req.IdUbicacion <= 0) throw new BusinessException("Debe seleccionar un local.");

        using var cn = _db.Create();

        // Alta: no duplicar un reemplazo activo (no procesado) para el mismo art reemplazo + local.
        if (req.Id == 0)
        {
            var existe = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM RepoReemplazos WHERE ARTCODReemplazo=@reemp AND IDUbicacion=@idu AND Eliminado=0 AND ISNULL(Procesado,0)=0;",
                new { reemp = artReemp, idu = req.IdUbicacion }, cancellationToken: ct));
            if (existe > 0) throw new BusinessException("Ya existe un reemplazo para el artículo y local seleccionados.");
        }

        // Mapeo del original en el LOCAL.
        int? idMapeoLocal = await cn.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 M.ID FROM MARKET.dbo.MapeoRegistro R WITH(NOLOCK)
            INNER JOIN MARKET.dbo.Mapeo M WITH(NOLOCK) ON R.IDMapeo = M.ID
            WHERE R.ARTCOD = @art AND M.IDUbicacion = @idu AND M.Eliminado = 0 AND R.Eliminado = 0
            ORDER BY M.ID DESC;
            """, new { art = artOrig, idu = req.IdUbicacion }, commandTimeout: 60, cancellationToken: ct));

        // Mapeo del reemplazo en el DEPÓSITO (IDUbicacion = 1).
        int? idMapeoLogistica = await cn.ExecuteScalarAsync<int?>(new CommandDefinition("""
            SELECT TOP 1 M.ID FROM MARKET.dbo.MapeoRegistro R WITH(NOLOCK)
            INNER JOIN MARKET.dbo.Mapeo M WITH(NOLOCK) ON R.IDMapeo = M.ID
            WHERE R.ARTCOD = @art AND M.IDUbicacion = 1 AND R.Eliminado = 0 AND M.Eliminado = 0
            ORDER BY M.ID DESC;
            """, new { art = artReemp }, commandTimeout: 60, cancellationToken: ct));

        var audit = $"{usuario} | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

        if (req.Id == 0)
        {
            const string ins = """
                INSERT INTO RepoReemplazos (ARTCOD, ARTCODReemplazo, idUbicacion, Fecha, idMapeoLogistica, IDMapeoLocal, Eliminado, Auditoria, Accion, Procesado)
                VALUES (@artOrig, @artReemp, @idUbi, GETDATE(), @idMapLog, @idMapLoc, 0, @audit, @accion, 0);
                """;
            await cn.ExecuteAsync(new CommandDefinition(ins, new
            {
                artOrig, artReemp, idUbi = req.IdUbicacion,
                idMapLog = idMapeoLogistica, idMapLoc = idMapeoLocal, audit, accion
            }, cancellationToken: ct));
        }
        else
        {
            const string upd = """
                UPDATE RepoReemplazos SET ARTCOD=@artOrig, ARTCODReemplazo=@artReemp, idUbicacion=@idUbi,
                       idMapeoLogistica=@idMapLog, IDMapeoLocal=@idMapLoc, Auditoria=@audit, Accion=@accion
                WHERE ID=@id;
                """;
            await cn.ExecuteAsync(new CommandDefinition(upd, new
            {
                artOrig, artReemp, idUbi = req.IdUbicacion,
                idMapLog = idMapeoLogistica, idMapLoc = idMapeoLocal, audit, accion, id = req.Id
            }, cancellationToken: ct));
        }
    }

    public async Task EliminarAsync(int id, string usuario, CancellationToken ct = default)
    {
        var audit = $"Registro eliminado | {usuario} | {DateTime.Now}";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE RepoReemplazos SET Eliminado=1, Auditoria=@audit WHERE ID=@id AND Eliminado=0;",
            new { id, audit }, cancellationToken: ct));
    }
}
