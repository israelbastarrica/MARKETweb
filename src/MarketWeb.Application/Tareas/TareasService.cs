using System.Text.Json;
using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Application.Reposicion;
using MarketWeb.Shared.Reposicion;
using MarketWeb.Shared.Tareas;

namespace MarketWeb.Application.Tareas;

/// <summary>
/// Programador de tareas propio (reemplaza al Programador de Windows + MARKET.exe -AUTOREPO).
/// CRUD de tareas, chequeo de vencimiento y ejecución (dispatch por tipo). Hoy: REPOSICION =
/// correr SP + generar PDF + mandar mail con adjunto.
/// </summary>
public sealed class TareasService : ITareasService
{
    private readonly ISqlConnectionFactory _db;
    private readonly IReposicionService _repo;
    private readonly IReposicionPdf _pdf;
    private readonly ISmtpSender _smtp;

    public TareasService(ISqlConnectionFactory db, IReposicionService repo, IReposicionPdf pdf, ISmtpSender smtp)
    {
        _db = db;
        _repo = repo;
        _pdf = pdf;
        _smtp = smtp;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        const string ddl = @"
IF OBJECT_ID('MARKET.dbo.TareasProgramadas') IS NULL
CREATE TABLE MARKET.dbo.TareasProgramadas (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(120) NOT NULL,
    Tipo VARCHAR(40) NOT NULL,
    Parametros NVARCHAR(MAX) NULL,
    Hora VARCHAR(5) NOT NULL,
    DiasSemana VARCHAR(20) NOT NULL,
    Activa BIT NOT NULL CONSTRAINT DF_TareasProg_Activa DEFAULT 1,
    UltimaEjecucion DATETIME NULL,
    UltimoOk BIT NULL,
    UltimoResultado NVARCHAR(500) NULL,
    CreadoPor NVARCHAR(120) NULL,
    FechaCreacion DATETIME NOT NULL CONSTRAINT DF_TareasProg_Fecha DEFAULT GETDATE(),
    Eliminado BIT NOT NULL CONSTRAINT DF_TareasProg_Elim DEFAULT 0
);
IF OBJECT_ID('MARKET.dbo.TareasProgramadasLog') IS NULL
CREATE TABLE MARKET.dbo.TareasProgramadasLog (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdTarea INT NOT NULL,
    Inicio DATETIME NOT NULL,
    Fin DATETIME NULL,
    Ok BIT NOT NULL CONSTRAINT DF_TareasLog_Ok DEFAULT 0,
    Origen VARCHAR(20) NOT NULL,
    Resultado NVARCHAR(MAX) NULL
);";
        await cn.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TareaProgramadaDto>> ListarAsync(CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var rows = await cn.QueryAsync<TareaProgramadaDto>(new CommandDefinition(
            @"SELECT Id, Nombre, Tipo, Hora, DiasSemana, Activa, UltimaEjecucion, UltimoOk, UltimoResultado
              FROM MARKET.dbo.TareasProgramadas
              WHERE Eliminado = 0
              ORDER BY Activa DESC, Nombre", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<TareaProgramadaEditorDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var row = await cn.QuerySingleOrDefaultAsync(new CommandDefinition(
            @"SELECT Id, Nombre, Tipo, Hora, DiasSemana, Activa, Parametros
              FROM MARKET.dbo.TareasProgramadas WHERE Id = @id AND Eliminado = 0",
            new { id }, cancellationToken: ct));
        if (row is null) return null;

        var p = ParseParametros((string?)row.Parametros);
        return new TareaProgramadaEditorDto
        {
            Id = row.Id,
            Nombre = row.Nombre,
            Tipo = row.Tipo,
            Hora = row.Hora,
            DiasSemana = row.DiasSemana,
            Activa = row.Activa,
            Local = p.Local,
            GenerarReemplazos = p.GenerarReemplazos,
            Destinatarios = p.Destinatarios
        };
    }

    public async Task<int> GuardarAsync(TareaSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var parametros = JsonSerializer.Serialize(new ParametrosReposicion
        {
            Local = string.IsNullOrWhiteSpace(req.Local) ? "TODOS" : req.Local.Trim().ToUpperInvariant(),
            GenerarReemplazos = req.GenerarReemplazos,
            Destinatarios = (req.Destinatarios ?? "").Trim()
        });

        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        if (req.Id > 0)
        {
            await cn.ExecuteAsync(new CommandDefinition(
                @"UPDATE MARKET.dbo.TareasProgramadas
                  SET Nombre = @Nombre, Tipo = @Tipo, Hora = @Hora, DiasSemana = @DiasSemana,
                      Activa = @Activa, Parametros = @Parametros
                  WHERE Id = @Id",
                new { req.Id, req.Nombre, req.Tipo, req.Hora, req.DiasSemana, req.Activa, Parametros = parametros },
                cancellationToken: ct));
            return req.Id;
        }

        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO MARKET.dbo.TareasProgramadas (Nombre, Tipo, Parametros, Hora, DiasSemana, Activa, CreadoPor)
              VALUES (@Nombre, @Tipo, @Parametros, @Hora, @DiasSemana, @Activa, @CreadoPor);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { req.Nombre, req.Tipo, Parametros = parametros, req.Hora, req.DiasSemana, req.Activa, CreadoPor = usuario },
            cancellationToken: ct));
    }

    public async Task EliminarAsync(int id, string usuario, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE MARKET.dbo.TareasProgramadas SET Eliminado = 1 WHERE Id = @id",
            new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<int>> PendientesAsync(DateTime ahora, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var rows = await cn.QueryAsync(new CommandDefinition(
            @"SELECT Id, Hora, DiasSemana, UltimaEjecucion
              FROM MARKET.dbo.TareasProgramadas
              WHERE Eliminado = 0 AND Activa = 1", cancellationToken: ct));

        var hoyIso = IsoDow(ahora);
        var pendientes = new List<int>();
        foreach (var r in rows)
        {
            string dias = r.DiasSemana ?? "";
            if (!dias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(hoyIso.ToString()))
                continue;
            if (!TimeSpan.TryParse((string)(r.Hora ?? "00:00"), out var hora)) continue;
            if (ahora.TimeOfDay < hora) continue;
            DateTime? ultima = r.UltimaEjecucion;
            if (ultima is not null && ultima.Value.Date >= ahora.Date) continue;   // ya corrió hoy
            pendientes.Add((int)r.Id);
        }
        return pendientes;
    }

    public async Task<IReadOnlyList<TareaLogDto>> HistorialAsync(int idTarea, int top = 20, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var rows = await cn.QueryAsync<TareaLogDto>(new CommandDefinition(
            @"SELECT TOP (@top) Id, Inicio, Fin, Ok, Origen, Resultado
              FROM MARKET.dbo.TareasProgramadasLog
              WHERE IdTarea = @idTarea
              ORDER BY Inicio DESC",
            new { idTarea, top }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task EjecutarAsync(int id, string origen, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        var tarea = await cn.QuerySingleOrDefaultAsync(new CommandDefinition(
            "SELECT Id, Nombre, Tipo, Parametros FROM MARKET.dbo.TareasProgramadas WHERE Id = @id AND Eliminado = 0",
            new { id }, cancellationToken: ct));
        if (tarea is null) return;

        // AUTO: marcamos UltimaEjecucion al arrancar para no re-disparar (loop de 60s / reinicio).
        if (origen == "AUTO")
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE MARKET.dbo.TareasProgramadas SET UltimaEjecucion = GETDATE() WHERE Id = @id",
                new { id }, cancellationToken: ct));

        var logId = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO MARKET.dbo.TareasProgramadasLog (IdTarea, Inicio, Ok, Origen)
              VALUES (@id, GETDATE(), 0, @origen);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { id, origen }, cancellationToken: ct));

        bool ok;
        string resultado;
        try
        {
            (ok, resultado) = (string)tarea.Tipo switch
            {
                TipoTarea.Reposicion => await EjecutarReposicionAsync((string?)tarea.Parametros, ct),
                _ => (false, $"Tipo de tarea no soportado: {tarea.Tipo}")
            };
        }
        catch (Exception ex)
        {
            ok = false;
            resultado = "Error: " + ex.Message;
        }

        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE MARKET.dbo.TareasProgramadasLog SET Fin = GETDATE(), Ok = @ok, Resultado = @resultado WHERE Id = @logId",
            new { ok, resultado, logId }, cancellationToken: ct));

        await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE MARKET.dbo.TareasProgramadas SET UltimoOk = @ok, UltimoResultado = @resultado
              WHERE Id = @id",
            new { ok, resultado = Recortar(resultado, 500), id }, cancellationToken: ct));
    }

    private async Task<(bool Ok, string Resultado)> EjecutarReposicionAsync(string? parametrosJson, CancellationToken ct)
    {
        var p = ParseParametros(parametrosJson);

        var datos = await _repo.CalcularAsync(new ReposicionCalcularRequest
        {
            Local = p.Local,
            FechaCorte = null,
            GenerarReemplazos = p.GenerarReemplazos
        }, "MARKETWEB-SCHED", ct);

        if (datos.TotalArticulos == 0)
            return (true, "Sin packs a reponer — no se envió mail.");

        var dest = (p.Destinatarios ?? "").Replace(',', ';');
        if (string.IsNullOrWhiteSpace(dest))
            return (false, $"{datos.TotalArticulos} artículos · {datos.TotalPacks} packs, pero la tarea no tiene destinatarios.");

        if (!_smtp.Configurado)
            return (false, $"{datos.TotalArticulos} artículos · {datos.TotalPacks} packs, pero el SMTP no está configurado en el servidor.");

        var pdf = await _pdf.GenerarAsync(datos, null, ct);
        var asunto = "Reposición " + DateTime.Now.ToString("dd/MM/yyyy");
        var body =
            "<p>Se adjunta el listado de packs a reponer generado automáticamente.</p>" +
            "<p>Generado el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + " desde MARKET Web (tarea programada).</p>";
        var nombre = $"Reposicion_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

        var enviado = await _smtp.EnviarAsync(dest, asunto, body, pdf, nombre, ct);
        return enviado
            ? (true, $"{datos.TotalArticulos} artículos · {datos.TotalPacks} packs · PDF enviado.")
            : (false, $"{datos.TotalArticulos} artículos · {datos.TotalPacks} packs · falló el envío del mail.");
    }

    private static ParametrosReposicion ParseParametros(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ParametrosReposicion();
        try { return JsonSerializer.Deserialize<ParametrosReposicion>(json) ?? new ParametrosReposicion(); }
        catch { return new ParametrosReposicion(); }
    }

    // ISO: lunes=1 .. domingo=7.
    private static int IsoDow(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];
}
