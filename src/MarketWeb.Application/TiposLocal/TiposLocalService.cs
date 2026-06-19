using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Locales;

namespace MarketWeb.Application.TiposLocal;

/// <summary>
/// ABM de Tipos de Local (tabla UbicacionesTipo). Equivale a frmRepoSimple +
/// frmABMSimple con Pantalla="UbicacionesTipo". Agrega validación de duplicado
/// (el ABM simple del desktop no la hacía para este caso) y baja lógica.
/// </summary>
public sealed class TiposLocalService : ITiposLocalService
{
    private readonly ISqlConnectionFactory _db;

    public TiposLocalService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<LocalTipoDto>> ListarAsync(string? filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID AS Id, Descripcion
            FROM   UbicacionesTipo
            WHERE  Eliminado = 0
              AND  (@filtro IS NULL OR Descripcion LIKE '%' + @filtro + '%')
            ORDER BY Descripcion;
            """;

        using var cn = _db.Create();
        var f = string.IsNullOrWhiteSpace(filtro) ? null : filtro.Trim();
        var rows = await cn.QueryAsync<LocalTipoDto>(new CommandDefinition(sql, new { filtro = f }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<LocalTipoDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        const string sql = "SELECT ID AS Id, Descripcion FROM UbicacionesTipo WHERE ID = @id AND Eliminado = 0;";
        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<LocalTipoDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<int> CrearAsync(TipoLocalSaveRequest req, CancellationToken ct = default)
    {
        var descripcion = (req.Descripcion ?? "").Trim();
        using var cn = _db.Create();
        await ValidarDuplicadoAsync(cn, descripcion, idExcluir: null, ct);

        var auditoria = ConstruirAuditoria("Alta de registro");
        const string sql = """
            INSERT INTO UbicacionesTipo (Descripcion, Eliminado, Auditoria)
            OUTPUT INSERTED.ID
            VALUES (@descripcion, 0, @auditoria);
            """;
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { descripcion, auditoria }, cancellationToken: ct));
    }

    public async Task ModificarAsync(int id, TipoLocalSaveRequest req, CancellationToken ct = default)
    {
        var descripcion = (req.Descripcion ?? "").Trim();
        using var cn = _db.Create();
        await ValidarDuplicadoAsync(cn, descripcion, idExcluir: id, ct);

        const string sql = "UPDATE UbicacionesTipo SET Descripcion = @descripcion WHERE ID = @id;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id, descripcion }, cancellationToken: ct));
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var auditoria = ConstruirAuditoria("Eliminación de registro");
        const string sql = "UPDATE UbicacionesTipo SET Eliminado = 1, Auditoria = @auditoria WHERE ID = @id;";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id, auditoria }, cancellationToken: ct));
    }

    private static async Task ValidarDuplicadoAsync(
        Microsoft.Data.SqlClient.SqlConnection cn, string descripcion, int? idExcluir, CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(1) FROM UbicacionesTipo
            WHERE Eliminado = 0 AND Descripcion = @descripcion
              AND (@idExcluir IS NULL OR ID <> @idExcluir);
            """;
        var existe = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { descripcion, idExcluir }, cancellationToken: ct));
        if (existe > 0)
            throw new BusinessException("Ya existe un registro con la misma descripción.");
    }

    private static string ConstruirAuditoria(string accion) =>
        $"{accion} | WEB | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
}
