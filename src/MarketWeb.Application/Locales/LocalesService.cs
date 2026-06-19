using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Locales;

namespace MarketWeb.Application.Locales;

/// <summary>
/// Lógica de Locales. Reemplaza lo que en el desktop estaba en frmRepoLocales
/// + frmABMLocales. Diferencias clave vs. el original:
///  - Consultas 100% parametrizadas (el desktop concatenaba strings → inyección).
///  - Baja lógica respetada (Eliminado=1, nunca DELETE).
///  - Corrige el bug del UPDATE a "Ubicacioness" (doble s) del frmRepoLocales.
/// </summary>
public sealed class LocalesService : ILocalesService
{
    private readonly ISqlConnectionFactory _db;

    public LocalesService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<LocalDto>> ListarAsync(string? filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT  U.ID          AS Id,
                    U.Descripcion AS Local,
                    UT.Descripcion AS Tipo,
                    U.IDTipo      AS IdTipo
            FROM    Ubicaciones U
            INNER JOIN UbicacionesTipo UT ON U.IDTipo = UT.ID
            WHERE   U.Eliminado = 0
              AND   UT.Eliminado = 0
              AND   (@filtro IS NULL OR U.Descripcion LIKE '%' + @filtro + '%')
            ORDER BY U.Descripcion;
            """;

        using var cn = _db.Create();
        var filtroLimpio = string.IsNullOrWhiteSpace(filtro) ? null : filtro.Trim();
        var rows = await cn.QueryAsync<LocalDto>(new CommandDefinition(sql, new { filtro = filtroLimpio }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<LocalTipoDto>> ListarTiposAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID AS Id, Descripcion
            FROM   UbicacionesTipo
            WHERE  Eliminado = 0
            ORDER BY Descripcion;
            """;

        using var cn = _db.Create();
        var rows = await cn.QueryAsync<LocalTipoDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<LocalDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT  U.ID          AS Id,
                    U.Descripcion AS Local,
                    UT.Descripcion AS Tipo,
                    U.IDTipo      AS IdTipo
            FROM    Ubicaciones U
            INNER JOIN UbicacionesTipo UT ON U.IDTipo = UT.ID
            WHERE   U.ID = @id AND U.Eliminado = 0;
            """;

        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<LocalDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<int> CrearAsync(LocalSaveRequest req, CancellationToken ct = default)
    {
        var descripcion = (req.Descripcion ?? "").Trim();
        using var cn = _db.Create();

        await ValidarDuplicadoAsync(cn, descripcion, idExcluir: null, ct);

        var auditoria = ConstruirAuditoria("Alta de registro");
        const string sql = """
            INSERT INTO Ubicaciones (IDTipo, Descripcion, Eliminado, Auditoria)
            OUTPUT INSERTED.ID
            VALUES (@idTipo, @descripcion, 0, @auditoria);
            """;

        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { idTipo = req.IdTipo, descripcion, auditoria }, cancellationToken: ct));
    }

    public async Task ModificarAsync(int id, LocalSaveRequest req, CancellationToken ct = default)
    {
        var descripcion = (req.Descripcion ?? "").Trim();
        using var cn = _db.Create();

        await ValidarDuplicadoAsync(cn, descripcion, idExcluir: id, ct);

        const string sql = """
            UPDATE Ubicaciones
            SET    IDTipo = @idTipo,
                   Descripcion = @descripcion
            WHERE  ID = @id;
            """;

        await cn.ExecuteAsync(new CommandDefinition(
            sql, new { id, idTipo = req.IdTipo, descripcion }, cancellationToken: ct));
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        // Baja lógica: en MARKET nunca se borra físicamente.
        const string sql = "UPDATE Ubicaciones SET Eliminado = 1 WHERE ID = @id;";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    private static async Task ValidarDuplicadoAsync(
        Microsoft.Data.SqlClient.SqlConnection cn, string descripcion, int? idExcluir, CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM   Ubicaciones
            WHERE  Eliminado = 0
              AND  Descripcion = @descripcion
              AND  (@idExcluir IS NULL OR ID <> @idExcluir);
            """;

        var existe = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { descripcion, idExcluir }, cancellationToken: ct));

        if (existe > 0)
            throw new BusinessException("Ya existe un registro con la misma descripción.");
    }

    // Mantiene el formato de Auditoria del desktop. El "WEB" se reemplazará por
    // el usuario autenticado cuando incorporemos auth.
    private static string ConstruirAuditoria(string accion) =>
        $"{accion} | WEB | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
}
