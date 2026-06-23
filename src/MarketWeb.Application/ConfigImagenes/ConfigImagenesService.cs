using System.Data;
using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Shared.ConfigImagenes;

namespace MarketWeb.Application.ConfigImagenes;

/// <summary>
/// Reescritura de frmRepoCatalogosConfigImagenes + frmABMCatalogoConfigImagenes.
/// Tabla MARKET.dbo.CatalogosConfigImagenes. Consultas parametrizadas, baja logica
/// (Eliminado = 1). La imagen vive en la columna varbinary(max) Imagen.
/// </summary>
public sealed class ConfigImagenesService : IConfigImagenesService
{
    private readonly ISqlConnectionFactory _db;
    public ConfigImagenesService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<ConfigImagenDto>> ListarAsync(string? tipo, string? descripcion, CancellationToken ct = default)
    {
        // Como el desktop (Procesar): filtra por Tipo exacto y Descripcion LIKE. La columna
        // Imagen NO se trae aca (puede pesar): solo si hay bytes (TieneImagen).
        var sql = """
            SELECT  ID            AS Id,
                    Tipo          AS Tipo,
                    Descripcion   AS Descripcion,
                    MatchArticulo AS MatchArticulo,
                    TieneImagen = CAST(CASE WHEN Imagen IS NOT NULL AND DATALENGTH(Imagen) > 0 THEN 1 ELSE 0 END AS BIT)
            FROM    CatalogosConfigImagenes
            WHERE   ISNULL(Eliminado, 0) = 0
            """;
        var t = Nz(tipo);
        var d = Nz(descripcion);
        if (t is not null) sql += " AND Tipo = @t";
        if (d is not null) sql += " AND Descripcion LIKE '%' + @d + '%'";
        sql += " ORDER BY Tipo, Descripcion;";

        using var cn = _db.Create();
        var rows = await cn.QueryAsync<ConfigImagenDto>(new CommandDefinition(sql, new { t, d }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<ConfigImagenDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT  ID            AS Id,
                    Tipo          AS Tipo,
                    Descripcion   AS Descripcion,
                    MatchArticulo AS MatchArticulo,
                    TieneImagen = CAST(CASE WHEN Imagen IS NOT NULL AND DATALENGTH(Imagen) > 0 THEN 1 ELSE 0 END AS BIT)
            FROM    CatalogosConfigImagenes
            WHERE   ID = @id AND ISNULL(Eliminado, 0) = 0;
            """;
        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<ConfigImagenDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<byte[]?> ObtenerImagenAsync(int id, CancellationToken ct = default)
    {
        const string sql = "SELECT Imagen FROM CatalogosConfigImagenes WHERE ID = @id AND ISNULL(Eliminado, 0) = 0;";
        using var cn = _db.Create();
        return await cn.ExecuteScalarAsync<byte[]?>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<int> CrearAsync(ConfigImagenSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var (tipo, desc, match) = Validar(req, esAlta: true);

        const string sql = """
            INSERT INTO CatalogosConfigImagenes (Tipo, Descripcion, MatchArticulo, Imagen, Auditoria, Eliminado)
            OUTPUT INSERTED.ID
            VALUES (@Tipo, @Descripcion, @MatchArticulo, @Imagen, @Auditoria, 0);
            """;
        var p = new DynamicParameters();
        p.Add("Tipo", tipo);
        p.Add("Descripcion", desc);
        p.Add("MatchArticulo", match);
        p.Add("Imagen", LeerImagen(req), DbType.Binary, size: -1);
        p.Add("Auditoria", Auditoria("Registro agregado", usuario));

        using var cn = _db.Create();
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task ModificarAsync(int id, ConfigImagenSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var (tipo, desc, match) = Validar(req, esAlta: false);

        // Si no mandaron imagen nueva, COALESCE conserva la actual (igual que el desktop).
        const string sql = """
            UPDATE CatalogosConfigImagenes
            SET    Tipo = @Tipo, Descripcion = @Descripcion, MatchArticulo = @MatchArticulo,
                   Imagen = COALESCE(@Imagen, Imagen), Auditoria = @Auditoria
            WHERE  ID = @Id AND ISNULL(Eliminado, 0) = 0;
            """;
        var p = new DynamicParameters();
        p.Add("Id", id);
        p.Add("Tipo", tipo);
        p.Add("Descripcion", desc);
        p.Add("MatchArticulo", match);
        p.Add("Imagen", LeerImagen(req), DbType.Binary, size: -1);
        p.Add("Auditoria", Auditoria("Registro modificado", usuario));

        using var cn = _db.Create();
        var filas = await cn.ExecuteAsync(new CommandDefinition(sql, p, cancellationToken: ct));
        if (filas == 0) throw new BusinessException("No se encontro la imagen a modificar.");
    }

    public async Task EliminarAsync(int id, string usuario, CancellationToken ct = default)
    {
        // Baja logica (regla del proyecto: nunca DELETE).
        const string sql = "UPDATE CatalogosConfigImagenes SET Eliminado = 1, Auditoria = @Auditoria WHERE ID = @id;";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(
            sql, new { id, Auditoria = Auditoria("Registro eliminado", usuario) }, cancellationToken: ct));
    }

    // ---------------- Helpers ----------------

    private static (string Tipo, string Desc, string? Match) Validar(ConfigImagenSaveRequest req, bool esAlta)
    {
        var tipo = (req.Tipo ?? "").Trim();
        var desc = (req.Descripcion ?? "").Trim();
        var match = string.IsNullOrWhiteSpace(req.MatchArticulo) ? null : req.MatchArticulo.Trim().ToUpperInvariant();

        if (desc.Length == 0) throw new BusinessException("Debe ingresar la descripcion.");
        if (tipo.Length == 0) throw new BusinessException("Debe ingresar el tipo de la imagen.");

        // En ETIQUETAS PRENDAS el match es obligatorio (lo usa el matcheo dinamico de frmABMCatalogo).
        if (string.Equals(tipo, TiposConfigImagen.EtiquetasPrendas, StringComparison.OrdinalIgnoreCase) && match is null)
            throw new BusinessException("Para tipo ETIQUETAS PRENDAS debe ingresar las palabras clave (Match Articulo). Ej: REM,SW,CARD");

        // En el alta la imagen es obligatoria (igual que el desktop).
        if (esAlta && string.IsNullOrWhiteSpace(req.ImagenBase64))
            throw new BusinessException("Debe seleccionar una imagen.");

        return (tipo, desc, match);
    }

    private static byte[]? LeerImagen(ConfigImagenSaveRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ImagenBase64)) return null;
        var b64 = req.ImagenBase64.Trim();
        // Por las dudas, sacamos un eventual prefijo data:...;base64,
        var coma = b64.IndexOf(',');
        if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && coma >= 0)
            b64 = b64[(coma + 1)..];
        try { return Convert.FromBase64String(b64); }
        catch (FormatException) { throw new BusinessException("La imagen enviada no es valida."); }
    }

    private static string? Nz(string? s)
    {
        var t = s?.Trim();
        if (string.IsNullOrEmpty(t) || string.Equals(t, "TODOS", StringComparison.OrdinalIgnoreCase)) return null;
        return t;
    }

    // Auditoria: mail del usuario (SSO) primero; el controller pasa User.Identity.Name.
    private static string Auditoria(string accion, string usuario) =>
        $"{accion} | {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
}
