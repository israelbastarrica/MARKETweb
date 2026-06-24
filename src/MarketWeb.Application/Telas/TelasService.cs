using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Telas;

namespace MarketWeb.Application.Telas;

/// <summary>
/// Telas + catálogos (depósitos/textiles/colores). Consultas parametrizadas, baja lógica.
/// Gramos/m² lo calcula la base (columna computada). El nombre de tabla de catálogo sale de
/// un whitelist (Mapa), nunca del input directo.
/// </summary>
public sealed class TelasService : ITelasService
{
    private readonly ISqlConnectionFactory _db;
    public TelasService(ISqlConnectionFactory db) => _db = db;

    private const string SelectTela = """
        SELECT  T.Id, T.IdDeposito, D.Nombre AS Deposito, T.Material,
                T.IdTextil, X.Nombre AS Textil,
                T.AnchoCm, T.Composicion, T.RindeMKg,
                T.IdColor, C.Nombre AS Color, C.Codigo AS ColorCodigo,
                T.GramosM2
        FROM    Telas T
        INNER JOIN TelasDepositos D ON D.Id = T.IdDeposito
        LEFT  JOIN TelasTextiles  X ON X.Id = T.IdTextil
        LEFT  JOIN TelasColores   C ON C.Id = T.IdColor
        """;

    public async Task<IReadOnlyList<TelaDto>> ListarAsync(int? idDeposito, string? material, int? idTextil, int? idColor, CancellationToken ct = default)
    {
        var sql = SelectTela + " WHERE T.Eliminado = 0";
        var mat = string.IsNullOrWhiteSpace(material) ? null : material.Trim();
        if (idDeposito is > 0) sql += " AND T.IdDeposito = @idDeposito";
        if (idTextil is > 0) sql += " AND T.IdTextil = @idTextil";
        if (idColor is > 0) sql += " AND T.IdColor = @idColor";
        if (mat is not null) sql += " AND T.Material LIKE '%' + @mat + '%'";
        sql += " ORDER BY D.Nombre, T.Material;";

        using var cn = _db.Create();
        var rows = await cn.QueryAsync<TelaDto>(new CommandDefinition(sql, new { idDeposito, idTextil, idColor, mat }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<TelaDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        var sql = SelectTela + " WHERE T.Id = @id AND T.Eliminado = 0;";
        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<TelaDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<int> CrearAsync(TelaSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var p = TelaParams(req);
        p.Add("aud", Auditoria("Alta de tela", usuario));
        const string sql = """
            INSERT INTO Telas (IdDeposito, Material, IdTextil, AnchoCm, Composicion, RindeMKg, IdColor, Eliminado, Auditoria)
            OUTPUT INSERTED.Id
            VALUES (@IdDeposito, @Material, @IdTextil, @AnchoCm, @Composicion, @RindeMKg, @IdColor, 0, @aud);
            """;
        using var cn = _db.Create();
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task ModificarAsync(int id, TelaSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var p = TelaParams(req);
        p.Add("id", id);
        p.Add("aud", Auditoria("Modificación de tela", usuario));
        const string sql = """
            UPDATE Telas SET IdDeposito=@IdDeposito, Material=@Material, IdTextil=@IdTextil,
                   AnchoCm=@AnchoCm, Composicion=@Composicion, RindeMKg=@RindeMKg, IdColor=@IdColor, Auditoria=@aud
            WHERE Id=@id AND Eliminado=0;
            """;
        using var cn = _db.Create();
        var n = await cn.ExecuteAsync(new CommandDefinition(sql, p, cancellationToken: ct));
        if (n == 0) throw new BusinessException("No se encontró la tela a modificar.");
    }

    public async Task EliminarAsync(int id, string usuario, CancellationToken ct = default)
    {
        const string sql = "UPDATE Telas SET Eliminado=1, Auditoria=@aud WHERE Id=@id;";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id, aud = Auditoria("Baja de tela", usuario) }, cancellationToken: ct));
    }

    private static DynamicParameters TelaParams(TelaSaveRequest req)
    {
        if (req.IdDeposito <= 0) throw new BusinessException("Debe elegir el depósito.");
        var material = (req.Material ?? "").Trim();
        if (material.Length == 0) throw new BusinessException("Debe ingresar el material.");

        var p = new DynamicParameters();
        p.Add("IdDeposito", req.IdDeposito);
        p.Add("Material", material);
        p.Add("IdTextil", req.IdTextil is > 0 ? req.IdTextil : null);
        p.Add("AnchoCm", req.AnchoCm);
        p.Add("Composicion", string.IsNullOrWhiteSpace(req.Composicion) ? null : req.Composicion.Trim());
        p.Add("RindeMKg", req.RindeMKg);
        p.Add("IdColor", req.IdColor is > 0 ? req.IdColor : null);
        return p;
    }

    // ===================== Catálogos =====================

    // Whitelist tipo -> (tabla, tieneCodigo). Evita inyección en el nombre de tabla.
    private static (string Tabla, bool Codigo) Mapa(string tipo) => tipo switch
    {
        CatalogoTela.Depositos => ("TelasDepositos", false),
        CatalogoTela.Textiles => ("TelasTextiles", false),
        CatalogoTela.Colores => ("TelasColores", true),
        _ => throw new BusinessException("Catálogo inválido.")
    };

    public async Task<IReadOnlyList<CatalogoItemDto>> ListarCatalogoAsync(string tipo, CancellationToken ct = default)
    {
        var (tabla, codigo) = Mapa(tipo);
        var colCodigo = codigo ? "Codigo" : "CAST(NULL AS NVARCHAR(20)) AS Codigo";
        var sql = $"SELECT Id, Nombre, {colCodigo} FROM {tabla} WHERE Eliminado = 0 ORDER BY Nombre;";
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<CatalogoItemDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> CrearCatalogoAsync(string tipo, CatalogoSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var (tabla, codigo) = Mapa(tipo);
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new BusinessException("Debe ingresar el nombre.");
        using var cn = _db.Create();
        await ValidarDuplicadoAsync(cn, tabla, nombre, null, ct);

        var cod = string.IsNullOrWhiteSpace(req.Codigo) ? null : req.Codigo.Trim();
        var aud = Auditoria("Alta de catálogo", usuario);
        var sql = codigo
            ? $"INSERT INTO {tabla} (Nombre, Codigo, Eliminado, Auditoria) OUTPUT INSERTED.Id VALUES (@nombre, @cod, 0, @aud);"
            : $"INSERT INTO {tabla} (Nombre, Eliminado, Auditoria) OUTPUT INSERTED.Id VALUES (@nombre, 0, @aud);";
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { nombre, cod, aud }, cancellationToken: ct));
    }

    public async Task ModificarCatalogoAsync(string tipo, int id, CatalogoSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var (tabla, codigo) = Mapa(tipo);
        var nombre = (req.Nombre ?? "").Trim();
        if (nombre.Length == 0) throw new BusinessException("Debe ingresar el nombre.");
        using var cn = _db.Create();
        await ValidarDuplicadoAsync(cn, tabla, nombre, id, ct);

        var cod = string.IsNullOrWhiteSpace(req.Codigo) ? null : req.Codigo.Trim();
        var aud = Auditoria("Modificación de catálogo", usuario);
        var sql = codigo
            ? $"UPDATE {tabla} SET Nombre=@nombre, Codigo=@cod, Auditoria=@aud WHERE Id=@id AND Eliminado=0;"
            : $"UPDATE {tabla} SET Nombre=@nombre, Auditoria=@aud WHERE Id=@id AND Eliminado=0;";
        var n = await cn.ExecuteAsync(new CommandDefinition(sql, new { nombre, cod, aud, id }, cancellationToken: ct));
        if (n == 0) throw new BusinessException("No se encontró el ítem a modificar.");
    }

    public async Task EliminarCatalogoAsync(string tipo, int id, string usuario, CancellationToken ct = default)
    {
        var (tabla, _) = Mapa(tipo);
        var sql = $"UPDATE {tabla} SET Eliminado=1, Auditoria=@aud WHERE Id=@id;";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id, aud = Auditoria("Baja de catálogo", usuario) }, cancellationToken: ct));
    }

    private static async Task ValidarDuplicadoAsync(Microsoft.Data.SqlClient.SqlConnection cn, string tabla, string nombre, int? idExcluir, CancellationToken ct)
    {
        var sql = $"SELECT COUNT(1) FROM {tabla} WHERE Eliminado=0 AND Nombre=@nombre AND (@idExcluir IS NULL OR Id<>@idExcluir);";
        var existe = await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { nombre, idExcluir }, cancellationToken: ct));
        if (existe > 0) throw new BusinessException("Ya existe un ítem con ese nombre.");
    }

    private static string Auditoria(string accion, string usuario) =>
        $"{accion} | {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
}
