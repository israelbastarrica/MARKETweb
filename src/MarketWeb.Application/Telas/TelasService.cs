using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Telas;

namespace MarketWeb.Application.Telas;

/// <summary>
/// Stock de telas por rollo (TelasRollos) + catálogos. Consultas parametrizadas, baja lógica.
/// El "stock por color" agrupa por nuestro color estandarizado (TelasColores); los rollos sin
/// color cargado caen en "(sin color)".
/// </summary>
public sealed class TelasService : ITelasService
{
    private readonly ISqlConnectionFactory _db;
    public TelasService(ISqlConnectionFactory db) => _db = db;

    // ---------------- Catálogos (combos) ----------------
    public async Task<IReadOnlyList<CatalogoItemDto>> ListarCatalogoAsync(string tipo, CancellationToken ct = default)
    {
        var (tabla, colNombre, orderBy) = tipo switch
        {
            CatalogoTela.Materiales => ("TelasMateriales", "Nombre", "Nombre"),
            // Colores ordenados por código ascendente (código es texto con ceros a la izquierda).
            CatalogoTela.Colores => ("TelasColores", "Descripcion", "TRY_CONVERT(int, Codigo), Codigo"),
            CatalogoTela.Depositos => ("TelasDepositos", "Nombre", "Nombre"),
            CatalogoTela.Teleras => ("TelasTeleras", "Nombre", "Nombre"),
            _ => throw new BusinessException("Catálogo inválido.")
        };
        var sql = $"SELECT Id, Codigo, {colNombre} AS Nombre FROM {tabla} WHERE Eliminado = 0 ORDER BY {orderBy};";
        using var cn = _db.Create();
        return (await cn.QueryAsync<CatalogoItemDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    // ---------------- Tablero ----------------
    public async Task<IReadOnlyList<DepoStockDto>> StockPorDepositoAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT  D.Id, D.Codigo, Deposito = D.Nombre,
                    CantRollos = CAST(COUNT(*) AS int),
                    Cantidad = CAST(SUM(ISNULL(R.Cantidad,0)) AS decimal(18,2))
            FROM TelasRollos R
            INNER JOIN TelasDepositos D ON D.Id = R.IdDeposito
            WHERE R.Eliminado = 0
            GROUP BY D.Id, D.Nombre, D.Codigo
            ORDER BY COUNT(*) DESC;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<DepoStockDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<PedidoBarraDto>> ResumenPorPedidoAsync(int? idDeposito, int top, CancellationToken ct = default)
    {
        if (top <= 0) top = 20;
        var sql = $"""
            SELECT TOP ({top})
                   Pedido = ISNULL(NULLIF(LTRIM(RTRIM(R.NumPedido)), ''), '(sin pedido)'),
                   CantRollos = CAST(COUNT(*) AS int),
                   Cantidad = CAST(SUM(ISNULL(R.Cantidad,0)) AS decimal(18,2))
            FROM TelasRollos R
            WHERE R.Eliminado = 0 {(idDeposito is > 0 ? "AND R.IdDeposito = @idDeposito" : "")}
            GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(R.NumPedido)), ''), '(sin pedido)')
            ORDER BY COUNT(*) DESC;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<PedidoBarraDto>(new CommandDefinition(sql, new { idDeposito }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<DepoMaterialDto>> MaterialesPorDepositoAsync(int idDeposito, CancellationToken ct = default)
    {
        const string sql = """
            SELECT  M.Id AS IdMaterial, M.Nombre AS Material,
                    Pedido = ISNULL(NULLIF(LTRIM(RTRIM(R.NumPedido)), ''), '(sin pedido)'),
                    Telera = T.Nombre,
                    CantRollos = CAST(COUNT(*) AS int),
                    Cantidad = CAST(SUM(ISNULL(R.Cantidad,0)) AS decimal(18,2))
            FROM    TelasRollos R
            INNER JOIN TelasMateriales M ON M.Id = R.IdMaterial
            LEFT  JOIN TelasTeleras   T ON T.Id = R.IdTelera
            WHERE   R.Eliminado = 0 AND R.IdDeposito = @idDeposito
            GROUP BY M.Id, M.Nombre, ISNULL(NULLIF(LTRIM(RTRIM(R.NumPedido)), ''), '(sin pedido)'), T.Nombre
            ORDER BY M.Nombre, COUNT(*) DESC;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<DepoMaterialDto>(new CommandDefinition(sql, new { idDeposito }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<ColorStockDto>> ColoresStockAsync(int idDeposito, int idMaterial, CancellationToken ct = default)
    {
        const string sql = """
            SELECT  C.Codigo AS ColorCod,
                    Color = ISNULL(C.Descripcion, '(sin color)'),
                    Pedido = ISNULL(NULLIF(LTRIM(RTRIM(R.NumPedido)), ''), '(sin pedido)'),
                    CantRollos = CAST(COUNT(*) AS int),
                    Cantidad = CAST(SUM(ISNULL(R.Cantidad,0)) AS decimal(18,2))
            FROM    TelasRollos R
            LEFT JOIN TelasColores C ON C.Id = R.IdColor
            WHERE   R.Eliminado = 0 AND R.IdDeposito = @idDeposito AND R.IdMaterial = @idMaterial
            GROUP BY C.Codigo, C.Descripcion, ISNULL(NULLIF(LTRIM(RTRIM(R.NumPedido)), ''), '(sin pedido)')
            ORDER BY CASE WHEN C.Codigo IS NULL THEN 1 ELSE 0 END, C.Codigo, COUNT(*) DESC;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<ColorStockDto>(new CommandDefinition(sql, new { idDeposito, idMaterial }, cancellationToken: ct))).ToList();
    }

    // ---------------- ABM de stock (rollos) ----------------
    private const string SelectRollo = """
        SELECT  R.Id, R.IdMaterial, M.Nombre AS Material,
                R.IdColor, C.Descripcion AS Color, R.ColorTelera,
                R.IdDeposito, D.Nombre AS Deposito,
                R.IdTelera, T.Nombre AS Telera,
                R.NumPedido, R.NumRemito, R.Cantidad, R.Unidad
        FROM    TelasRollos R
        INNER JOIN TelasMateriales M ON M.Id = R.IdMaterial
        INNER JOIN TelasDepositos  D ON D.Id = R.IdDeposito
        LEFT  JOIN TelasColores    C ON C.Id = R.IdColor
        LEFT  JOIN TelasTeleras    T ON T.Id = R.IdTelera
        """;

    public async Task<IReadOnlyList<TelaRolloDto>> ListarRollosAsync(int? idDeposito, int? idMaterial, int? idColor, int? idTelera, string? numPedido, bool sinColor = false, CancellationToken ct = default)
    {
        var sql = SelectRollo + " WHERE R.Eliminado = 0";
        var ped = string.IsNullOrWhiteSpace(numPedido) ? null : numPedido.Trim();
        if (idDeposito is > 0) sql += " AND R.IdDeposito = @idDeposito";
        if (idMaterial is > 0) sql += " AND R.IdMaterial = @idMaterial";
        if (sinColor) sql += " AND R.IdColor IS NULL";          // fuera de carta (sin color de nuestra paleta)
        else if (idColor is > 0) sql += " AND R.IdColor = @idColor";
        if (idTelera is > 0) sql += " AND R.IdTelera = @idTelera";
        if (ped is not null) sql += " AND R.NumPedido LIKE '%' + @ped + '%'";
        sql += " ORDER BY M.Nombre, R.Id;";
        using var cn = _db.Create();
        return (await cn.QueryAsync<TelaRolloDto>(new CommandDefinition(sql, new { idDeposito, idMaterial, idColor, idTelera, ped }, cancellationToken: ct))).ToList();
    }

    public async Task<int> CrearRolloAsync(RolloSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var p = Params(req);
        p.Add("aud", Auditoria("Alta de rollo", usuario));
        const string sql = """
            INSERT INTO TelasRollos (IdMaterial, IdColor, ColorTelera, IdDeposito, IdTelera, NumPedido, NumRemito, Cantidad, Unidad, Eliminado, Auditoria)
            OUTPUT INSERTED.Id
            VALUES (@IdMaterial, @IdColor, @ColorTelera, @IdDeposito, @IdTelera, @NumPedido, @NumRemito, @Cantidad, @Unidad, 0, @aud);
            """;
        using var cn = _db.Create();
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task ModificarRolloAsync(int id, RolloSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var p = Params(req);
        p.Add("id", id);
        p.Add("aud", Auditoria("Modificación de rollo", usuario));
        const string sql = """
            UPDATE TelasRollos SET IdMaterial=@IdMaterial, IdColor=@IdColor, ColorTelera=@ColorTelera,
                   IdDeposito=@IdDeposito, IdTelera=@IdTelera, NumPedido=@NumPedido, NumRemito=@NumRemito,
                   Cantidad=@Cantidad, Unidad=@Unidad, Auditoria=@aud
            WHERE Id=@id AND Eliminado=0;
            """;
        using var cn = _db.Create();
        var n = await cn.ExecuteAsync(new CommandDefinition(sql, p, cancellationToken: ct));
        if (n == 0) throw new BusinessException("No se encontró el rollo a modificar.");
    }

    public async Task EliminarRolloAsync(int id, string usuario, CancellationToken ct = default)
    {
        const string sql = "UPDATE TelasRollos SET Eliminado=1, Auditoria=@aud WHERE Id=@id;";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id, aud = Auditoria("Baja de rollo", usuario) }, cancellationToken: ct));
    }

    private static DynamicParameters Params(RolloSaveRequest req)
    {
        if (req.IdMaterial <= 0) throw new BusinessException("Debe elegir el material.");
        if (req.IdDeposito <= 0) throw new BusinessException("Debe elegir el depósito.");
        var p = new DynamicParameters();
        p.Add("IdMaterial", req.IdMaterial);
        p.Add("IdDeposito", req.IdDeposito);
        p.Add("IdColor", req.IdColor is > 0 ? req.IdColor : null);
        p.Add("IdTelera", req.IdTelera is > 0 ? req.IdTelera : null);
        p.Add("ColorTelera", Nz(req.ColorTelera));
        p.Add("NumPedido", Nz(req.NumPedido));
        p.Add("NumRemito", Nz(req.NumRemito));
        p.Add("Cantidad", req.Cantidad);
        p.Add("Unidad", Nz(req.Unidad));
        return p;
    }

    private static string? Nz(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Auditoria(string accion, string usuario) =>
        $"{accion} | {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
}
