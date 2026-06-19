using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Mapeo;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Mapeo;

/// <summary>
/// Estructura de mapeo. Reescritura de frmRepoMapeo + frmABMMapeo(Detalle) +
/// frmABMMapeoPosicion/Registro. Consultas parametrizadas, baja lógica (Eliminado=1).
/// La descripción del artículo se resuelve cross-DB contra DRAGONFISH_CENTRAL.
/// </summary>
public sealed class MapeoService : IMapeoService
{
    private readonly ISqlConnectionFactory _db;
    public MapeoService(ISqlConnectionFactory db) => _db = db;

    private const int IdDeposito = 1; // DEPÓSITO se administra desde Logística, no acá.

    // ---------------- Ubicaciones ----------------
    public async Task<IReadOnlyList<MapeoUbicacionDto>> ListarUbicacionesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT  U.ID          AS IdUbicacion,
                    U.Descripcion AS Ubicacion,
                    UT.Descripcion AS Tipo,
                    Posiciones = (SELECT COUNT(1) FROM Mapeo M WHERE M.IDUbicacion = U.ID AND M.Eliminado = 0)
            FROM    Ubicaciones U
            INNER JOIN UbicacionesTipo UT ON U.IDTipo = UT.ID
            WHERE   U.Eliminado = 0 AND UT.Eliminado = 0 AND U.ID <> @dep
            ORDER BY U.Descripcion;
            """;
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<MapeoUbicacionDto>(new CommandDefinition(sql, new { dep = IdDeposito }, cancellationToken: ct));
        return rows.ToList();
    }

    // ---------------- Posiciones (tabla Mapeo) ----------------
    private const string SelectPosicion = """
        SELECT  M.ID            AS Id,
                M.IDUbicacion   AS IdUbicacion,
                M.Pasillo       AS Sector,
                M.Modulo        AS Modulo,
                M.Mobiliario    AS Mobiliario,
                M.Fila          AS Fila,
                M.Posicion      AS Posicion,
                M.Panel         AS Panel,
                M.OrdenPasillo  AS OrdenPasillo,
                M.FilaOrden     AS FilaOrden,
                NoReposicion = CAST(CASE WHEN ISNULL(M.NoReposicion, 0) = 1 THEN 1 ELSE 0 END AS BIT),
                Articulos = (SELECT COUNT(1) FROM MapeoRegistro R WHERE R.IDMapeo = M.ID AND R.Eliminado = 0),
                ArtCods = STUFF((SELECT ' ' + R.ARTCOD FROM MapeoRegistro R WHERE R.IDMapeo = M.ID AND R.Eliminado = 0 FOR XML PATH('')), 1, 1, ''),
                CoordX = ISNULL(M.CoordX, 0), CoordY = ISNULL(M.CoordY, 0),
                CoordXLenceria = ISNULL(M.CoordXLenceria, 0), CoordYLenceria = ISNULL(M.CoordYLenceria, 0),
                CoordXCodigo = ISNULL(M.CoordXCodigo, 0), CoordYCodigo = ISNULL(M.CoordYCodigo, 0),
                CoordXDesc = ISNULL(M.CoordXDesc, 0), CoordYDesc = ISNULL(M.CoordYDesc, 0)
        FROM    Mapeo M
        """;

    public async Task<IReadOnlyList<MapeoPosicionDto>> ListarPosicionesAsync(int idUbicacion, CancellationToken ct = default)
    {
        var sql = SelectPosicion + """
             WHERE M.Eliminado = 0 AND M.IDUbicacion = @idUbicacion
             ORDER BY ISNULL(M.OrdenPasillo, 999999), M.Pasillo, ISNULL(M.FilaOrden, M.Fila), M.Posicion;
            """;
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<MapeoPosicionDto>(new CommandDefinition(sql, new { idUbicacion }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<MapeoPosicionDto?> ObtenerPosicionAsync(int id, CancellationToken ct = default)
    {
        var sql = SelectPosicion + " WHERE M.ID = @id AND M.Eliminado = 0;";
        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<MapeoPosicionDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<int> CrearPosicionAsync(MapeoPosicionSaveRequest req, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        var modulo = (req.Modulo ?? "").Trim();
        await ValidarModuloDuplicadoAsync(cn, req.IdUbicacion, modulo, null, ct);

        const string sql = """
            INSERT INTO Mapeo (IDUbicacion, Modulo, Mobiliario, CoordX, CoordY, Pasillo, Fila, Posicion,
                CoordXLenceria, CoordYLenceria, Panel, CoordXCodigo, CoordYCodigo, FilaOrden,
                CoordXDesc, CoordYDesc, Eliminado, Auditoria, OrdenPasillo, NoReposicion)
            OUTPUT INSERTED.ID
            VALUES (@IdUbicacion, @Modulo, @Mobiliario, @CoordX, @CoordY, @Sector, @Fila, @Posicion,
                @CoordXLenceria, @CoordYLenceria, @Panel, @CoordXCodigo, @CoordYCodigo, @FilaOrden,
                @CoordXDesc, @CoordYDesc, 0, @Auditoria, @OrdenPasillo, @NoRepo);
            """;
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, Params(req, modulo, "Alta de registro"), cancellationToken: ct));
    }

    public async Task ModificarPosicionAsync(int id, MapeoPosicionSaveRequest req, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        var modulo = (req.Modulo ?? "").Trim();
        await ValidarModuloDuplicadoAsync(cn, req.IdUbicacion, modulo, id, ct);

        const string sql = """
            UPDATE Mapeo SET
                Modulo = @Modulo, Mobiliario = @Mobiliario, CoordX = @CoordX, CoordY = @CoordY,
                Pasillo = @Sector, Fila = @Fila, Posicion = @Posicion,
                CoordXLenceria = @CoordXLenceria, CoordYLenceria = @CoordYLenceria, Panel = @Panel,
                CoordXCodigo = @CoordXCodigo, CoordYCodigo = @CoordYCodigo, FilaOrden = @FilaOrden,
                CoordXDesc = @CoordXDesc, CoordYDesc = @CoordYDesc, Auditoria = @Auditoria,
                OrdenPasillo = @OrdenPasillo, NoReposicion = @NoRepo
            WHERE ID = @Id;
            """;
        var p = Params(req, modulo, "Modificación de registro");
        p.Add("Id", id);
        await cn.ExecuteAsync(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task EliminarPosicionAsync(int id, CancellationToken ct = default)
    {
        const string sql = "UPDATE Mapeo SET Eliminado = 1 WHERE ID = @id;";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    private static DynamicParameters Params(MapeoPosicionSaveRequest req, string modulo, string accion)
    {
        var p = new DynamicParameters();
        p.Add("IdUbicacion", req.IdUbicacion);
        p.Add("Modulo", modulo);
        p.Add("Mobiliario", string.IsNullOrWhiteSpace(req.Mobiliario) ? null : req.Mobiliario.Trim());
        p.Add("Sector", (req.Sector ?? "").Trim());
        p.Add("Fila", req.Fila);
        p.Add("Posicion", req.Posicion);
        p.Add("Panel", string.IsNullOrWhiteSpace(req.Panel) ? null : req.Panel.Trim());
        p.Add("OrdenPasillo", req.OrdenPasillo);
        p.Add("FilaOrden", req.FilaOrden);
        p.Add("NoRepo", req.NoReposicion ? 1 : 0);
        p.Add("CoordX", req.CoordX);
        p.Add("CoordY", req.CoordY);
        p.Add("CoordXLenceria", req.CoordXLenceria);
        p.Add("CoordYLenceria", req.CoordYLenceria);
        p.Add("CoordXCodigo", req.CoordXCodigo);
        p.Add("CoordYCodigo", req.CoordYCodigo);
        p.Add("CoordXDesc", req.CoordXDesc);
        p.Add("CoordYDesc", req.CoordYDesc);
        p.Add("Auditoria", ConstruirAuditoria(accion));
        return p;
    }

    private static async Task ValidarModuloDuplicadoAsync(SqlConnection cn, int idUbicacion, string modulo, int? idExcluir, CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(1) FROM Mapeo
            WHERE Eliminado = 0 AND IDUbicacion = @idUbicacion AND Modulo = @modulo
              AND (@idExcluir IS NULL OR ID <> @idExcluir);
            """;
        var existe = await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { idUbicacion, modulo, idExcluir }, cancellationToken: ct));
        if (existe > 0)
            throw new BusinessException("Ya existe un módulo con esa descripción en la ubicación.");
    }

    // ---------------- Combos ----------------
    public async Task<IReadOnlyList<string>> ListarSectoresAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT DISTINCT Pasillo FROM Mapeo WHERE Eliminado = 0 AND Pasillo IS NOT NULL AND LTRIM(RTRIM(Pasillo)) <> '' ORDER BY Pasillo;";
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<string>> ListarMobiliariosAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT DISTINCT Mobiliario FROM Mapeo WHERE Eliminado = 0 AND Mobiliario IS NOT NULL AND LTRIM(RTRIM(Mobiliario)) <> '' ORDER BY Mobiliario;";
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    // ---------------- Artículos por posición (MapeoRegistro) ----------------
    public async Task<IReadOnlyList<MapeoArticuloDto>> ListarArticulosAsync(int idMapeo, CancellationToken ct = default)
    {
        const string sql = """
            SELECT  R.ID AS Id,
                    R.ARTCOD AS ArtCod,
                    Descripcion = A.ARTDES
            FROM    MapeoRegistro R
            LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART A ON A.ARTCOD = R.ARTCOD
            WHERE   R.IDMapeo = @idMapeo AND R.Eliminado = 0
            ORDER BY R.ID;
            """;
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<MapeoArticuloDto>(new CommandDefinition(sql, new { idMapeo }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> CrearArticuloAsync(MapeoArticuloSaveRequest req, CancellationToken ct = default)
    {
        var artcod = (req.ArtCod ?? "").Trim();
        using var cn = _db.Create();
        await ValidarArticuloDuplicadoAsync(cn, req.IdMapeo, artcod, null, ct);

        const string sql = """
            INSERT INTO MapeoRegistro (IDMapeo, ARTCOD, FechaHora, Historico, Eliminado, Auditoria)
            OUTPUT INSERTED.ID
            VALUES (@idMapeo, @artcod, GETDATE(), 0, 0, @auditoria);
            """;
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { idMapeo = req.IdMapeo, artcod, auditoria = ConstruirAuditoria("Alta de artículo") }, cancellationToken: ct));
    }

    public async Task ModificarArticuloAsync(int id, MapeoArticuloSaveRequest req, CancellationToken ct = default)
    {
        var artcod = (req.ArtCod ?? "").Trim();
        using var cn = _db.Create();
        await ValidarArticuloDuplicadoAsync(cn, req.IdMapeo, artcod, id, ct);

        const string sql = "UPDATE MapeoRegistro SET ARTCOD = @artcod, Auditoria = @auditoria WHERE ID = @id;";
        await cn.ExecuteAsync(new CommandDefinition(
            sql, new { id, artcod, auditoria = ConstruirAuditoria("Modificación de artículo") }, cancellationToken: ct));
    }

    public async Task EliminarArticuloAsync(int id, CancellationToken ct = default)
    {
        const string sql = "UPDATE MapeoRegistro SET Eliminado = 1, Auditoria = @auditoria WHERE ID = @id;";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(
            sql, new { id, auditoria = ConstruirAuditoria("Baja de artículo") }, cancellationToken: ct));
    }

    private static async Task ValidarArticuloDuplicadoAsync(SqlConnection cn, int idMapeo, string artcod, int? idExcluir, CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(1) FROM MapeoRegistro
            WHERE Eliminado = 0 AND IDMapeo = @idMapeo AND ARTCOD = @artcod
              AND (@idExcluir IS NULL OR ID <> @idExcluir);
            """;
        var existe = await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { idMapeo, artcod, idExcluir }, cancellationToken: ct));
        if (existe > 0)
            throw new BusinessException("Ese artículo ya está cargado en la posición.");
    }

    private static string ConstruirAuditoria(string accion) =>
        $"{accion} | WEB | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

    // ===================== REPORTE (Logística) =====================

    public async Task<IReadOnlyList<MapeoUbicacionDto>> ListarUbicacionesReporteAsync(CancellationToken ct = default)
    {
        // Incluye DEPÓSITO (a diferencia de ListarUbicacionesAsync).
        const string sql = """
            SELECT U.ID AS IdUbicacion, U.Descripcion AS Ubicacion, UT.Descripcion AS Tipo, Posiciones = 0
            FROM Ubicaciones U INNER JOIN UbicacionesTipo UT ON U.IDTipo = UT.ID
            WHERE U.Eliminado = 0 AND UT.Eliminado = 0
            ORDER BY U.Descripcion;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<MapeoUbicacionDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<string>> ListarTiposAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT DISTINCT TIPO.DESCRIP FROM DRAGONFISH_CENTRAL.ZooLogic.ART ART LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.TIPOART TIPO ON TIPO.COD = ART.TIPOARTI WHERE TIPO.DESCRIP <> '' ORDER BY TIPO.DESCRIP;";
        using var cn = _db.Create();
        return (await cn.QueryAsync<string>(new CommandDefinition(sql, commandTimeout: 60, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<string>> ListarCategoriasAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT DISTINCT CATE.DESCRIP FROM DRAGONFISH_CENTRAL.ZooLogic.ART ART LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.CATEGART CATE ON CATE.COD = ART.CATEARTI WHERE CATE.DESCRIP <> '' ORDER BY CATE.DESCRIP;";
        using var cn = _db.Create();
        return (await cn.QueryAsync<string>(new CommandDefinition(sql, commandTimeout: 60, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<MapeoReporteDto>> ReporteAsync(MapeoReporteRequest req, CancellationToken ct = default)
    {
        // El SP usa @X IS NULL = "sin filtro": los textos vacíos van como NULL.
        static string? Nz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        var p = new DynamicParameters();
        p.Add("@FiltroUbicacion", string.IsNullOrWhiteSpace(req.FiltroUbicacion) ? "LOCALES" : req.FiltroUbicacion);
        p.Add("@IDUbicacion", req.IdUbicacion);
        p.Add("@Sector", Nz(req.Sector));
        p.Add("@Mobiliario", Nz(req.Mobiliario));
        p.Add("@Fila", req.Fila);
        p.Add("@Posicion", req.Posicion);
        p.Add("@CodArt", Nz(req.CodArt));
        p.Add("@SoloVacios", req.SoloVacios);
        p.Add("@Combo", (string?)null);
        p.Add("@CalculaStock", req.CalculaStock);
        p.Add("@Tipo", Nz(req.Tipo));
        p.Add("@Familia", (string?)null);
        p.Add("@Material", (string?)null);
        p.Add("@Temporada", (string?)null);
        p.Add("@Año", req.Anio);
        p.Add("@Categoria", Nz(req.Categoria));
        p.Add("@CodProveedor", (string?)null);
        p.Add("@Descripcion", Nz(req.Descripcion));
        p.Add("@FiltraFechaAlta", false);
        p.Add("@FiltraFechaDesde", (DateTime?)null);
        p.Add("@FiltraFechaHasta", (DateTime?)null);

        using var cn = _db.Create();
        return (await cn.QueryAsync<MapeoReporteDto>(new CommandDefinition(
            "SP_ReporteMapeo_Generar", p, commandType: System.Data.CommandType.StoredProcedure,
            commandTimeout: 180, cancellationToken: ct))).ToList();
    }
}
