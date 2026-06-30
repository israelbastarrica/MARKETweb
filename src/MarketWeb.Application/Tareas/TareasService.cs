using System.Diagnostics;
using System.Text.Json;
using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Application.Reposicion;
using MarketWeb.Shared.Reposicion;
using MarketWeb.Shared.Tareas;
using Microsoft.Extensions.Configuration;

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
    private readonly IConfiguration _cfg;
    private readonly MarketWeb.Application.Marketing.IMarketingCollector _redes;

    public TareasService(ISqlConnectionFactory db, IReposicionService repo, IReposicionPdf pdf, ISmtpSender smtp, IConfiguration cfg,
        MarketWeb.Application.Marketing.IMarketingCollector redes)
    {
        _db = db;
        _repo = repo;
        _pdf = pdf;
        _smtp = smtp;
        _cfg = cfg;
        _redes = redes;
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
);
IF COL_LENGTH('MARKET.dbo.TareasProgramadas','UltimaEjecucionAuto') IS NULL
    ALTER TABLE MARKET.dbo.TareasProgramadas ADD UltimaEjecucionAuto DATETIME NULL;";
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

        var dto = new TareaProgramadaEditorDto
        {
            Id = row.Id,
            Nombre = row.Nombre,
            Tipo = row.Tipo,
            Hora = row.Hora,
            DiasSemana = row.DiasSemana,
            Activa = row.Activa
        };

        if ((string)row.Tipo == TipoTarea.Backup)
        {
            var pb = ParseParametrosBackup((string?)row.Parametros);
            dto.BackupCarpeta = pb.Carpeta;
            dto.BackupRetencionDias = pb.RetencionDias;
            dto.BackupMail = pb.MailFallo;
        }
        else
        {
            var p = ParseParametros((string?)row.Parametros);
            dto.Local = p.Local;
            dto.GenerarReemplazos = p.GenerarReemplazos;
            dto.Destinatarios = p.Destinatarios;
        }
        return dto;
    }

    public async Task<int> GuardarAsync(TareaSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var parametros = req.Tipo switch
        {
            TipoTarea.Backup => JsonSerializer.Serialize(new ParametrosBackup
            {
                Carpeta = (req.BackupCarpeta ?? "").Trim(),
                RetencionDias = req.BackupRetencionDias,
                MailFallo = (req.BackupMail ?? "").Trim()
            }),
            TipoTarea.Redes => JsonSerializer.Serialize(new ParametrosRedes()),  // cada 4 h, 25 posts/red
            _ => JsonSerializer.Serialize(new ParametrosReposicion
            {
                Local = string.IsNullOrWhiteSpace(req.Local) ? "TODOS" : req.Local.Trim().ToUpperInvariant(),
                GenerarReemplazos = req.GenerarReemplazos,
                Destinatarios = (req.Destinatarios ?? "").Trim()
            })
        };

        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        if (req.Id > 0)
        {
            await cn.ExecuteAsync(new CommandDefinition(
                @"UPDATE MARKET.dbo.TareasProgramadas
                  SET Nombre = @Nombre, Tipo = @Tipo, Hora = @Hora, DiasSemana = @DiasSemana,
                      Activa = @Activa, Parametros = @Parametros,
                      UltimaEjecucionAuto = NULL   -- reconfigurar = vuelve a correr a la hora nueva
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
            @"SELECT Id, Tipo, Parametros, Hora, DiasSemana, UltimaEjecucionAuto
              FROM MARKET.dbo.TareasProgramadas
              WHERE Eliminado = 0 AND Activa = 1", cancellationToken: ct));

        var hoyIso = IsoDow(ahora);
        var pendientes = new List<int>();
        foreach (var r in rows)
        {
            string dias = r.DiasSemana ?? "";
            if (!dias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(hoyIso.ToString()))
                continue;

            // REDES corre por INTERVALO (cada N horas), no a una hora fija.
            if ((string)(r.Tipo ?? "") == TipoTarea.Redes)
            {
                int horas = 4;
                try
                {
                    var pr = JsonSerializer.Deserialize<ParametrosRedes>((string?)r.Parametros ?? "");
                    if (pr is not null && pr.IntervaloHoras > 0) horas = pr.IntervaloHoras;
                }
                catch { }
                DateTime? ult = r.UltimaEjecucionAuto;
                if (ult is null || (ahora - ult.Value) >= TimeSpan.FromHours(horas))
                    pendientes.Add((int)r.Id);
                continue;
            }

            if (!TimeSpan.TryParse((string)(r.Hora ?? "00:00"), out var hora)) continue;
            if (ahora.TimeOfDay < hora) continue;
            // Corre una vez por día a su hora. Al GUARDAR la tarea se resetea esta marca (UltimaEjecucionAuto=NULL),
            // así vuelve a correr a la hora nueva configurada — haya salido bien o mal antes.
            DateTime? ultimaAuto = r.UltimaEjecucionAuto;
            if (ultimaAuto is not null && ultimaAuto.Value.Date >= ahora.Date) continue;
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

        // AUTO: marcamos UltimaEjecucionAuto al arrancar para que el scheduler no re-dispare hoy
        // (loop de 60s / reinicio). El manual NO la toca, así no cancela la corrida nocturna.
        if (origen == "AUTO")
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE MARKET.dbo.TareasProgramadas SET UltimaEjecucionAuto = GETDATE() WHERE Id = @id",
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
                TipoTarea.Backup => await EjecutarBackupAsync((string?)tarea.Parametros, ct),
                TipoTarea.Redes => await EjecutarRedesAsync((string?)tarea.Parametros, ct),
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

        // La fecha visible (UltimaEjecucion) se actualiza SIEMPRE, manual o automática.
        await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE MARKET.dbo.TareasProgramadas
              SET UltimaEjecucion = GETDATE(), UltimoOk = @ok, UltimoResultado = @resultado
              WHERE Id = @id",
            new { ok, resultado = Recortar(resultado, 500), id }, cancellationToken: ct));

        // Aviso por mail si falló (el Backup ya manda su propio aviso, no duplicamos).
        if (!ok && (string)tarea.Tipo != TipoTarea.Backup)
            await EnviarAlertaFallaAsync((string)tarea.Nombre, (string)tarea.Tipo, resultado, ct);
    }

    // Manda un mail de alerta cuando una tarea falla. Destinatario: config Tareas:MailErrores
    // (env Tareas__MailErrores); si no está, cae al remitente del SMTP. Best-effort (si el SMTP
    // es justo lo que falla, no se puede avisar — queda en el log igual).
    private async Task EnviarAlertaFallaAsync(string nombre, string tipo, string error, CancellationToken ct)
    {
        try
        {
            if (!_smtp.Configurado) return;
            var dest = _cfg["Tareas:MailErrores"];
            if (string.IsNullOrWhiteSpace(dest)) dest = _cfg["Smtp:From"];
            if (string.IsNullOrWhiteSpace(dest)) return;

            var body =
                $"<p>La tarea programada <b>{System.Net.WebUtility.HtmlEncode(nombre)}</b> ({tipo}) <b>falló</b>.</p>" +
                $"<p>Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}</p>" +
                $"<p>Detalle:</p><pre>{System.Net.WebUtility.HtmlEncode(error ?? "")}</pre>" +
                "<p>— MARKET Web (programador de tareas)</p>";
            await _smtp.EnviarAsync(dest.Replace(',', ';'), $"⚠ Tarea MARKET falló: {nombre}", body, ct);
        }
        catch { /* best-effort: el fallo ya quedó en el log */ }
    }

    private async Task<(bool Ok, string Resultado)> EjecutarRedesAsync(string? parametrosJson, CancellationToken ct)
    {
        var p = string.IsNullOrWhiteSpace(parametrosJson)
            ? new ParametrosRedes()
            : (System.Text.Json.JsonSerializer.Deserialize<ParametrosRedes>(parametrosJson!) ?? new ParametrosRedes());
        return await _redes.RecolectarAsync(p.Limite, ct);
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

    // Reenvía SOLO el PDF + mail de la ÚLTIMA corrida guardada, SIN correr el SP de nuevo
    // (cuando la repo calculó bien pero el mail falló, ej. SMTP no estaba listo en ese arranque).
    public async Task<(bool Ok, string Resultado)> ReenviarReposicionAsync(int id, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        var tarea = await cn.QuerySingleOrDefaultAsync(new CommandDefinition(
            "SELECT Id, Tipo, Parametros FROM MARKET.dbo.TareasProgramadas WHERE Id = @id AND Eliminado = 0",
            new { id }, cancellationToken: ct));
        if (tarea is null) return (false, "Tarea no encontrada.");
        if ((string)tarea.Tipo != TipoTarea.Reposicion) return (false, "El reenvío es solo para tareas de Reposición.");

        var logId = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO MARKET.dbo.TareasProgramadasLog (IdTarea, Inicio, Ok, Origen)
              VALUES (@id, GETDATE(), 0, 'REENVIO');
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { id }, cancellationToken: ct));

        bool ok; string resultado;
        try { (ok, resultado) = await ReconstruirYEnviarAsync((string?)tarea.Parametros, ct); }
        catch (Exception ex) { ok = false; resultado = "Error: " + ex.Message; }

        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE MARKET.dbo.TareasProgramadasLog SET Fin = GETDATE(), Ok = @ok, Resultado = @resultado WHERE Id = @logId",
            new { ok, resultado, logId }, cancellationToken: ct));
        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE MARKET.dbo.TareasProgramadas SET UltimaEjecucion = GETDATE(), UltimoOk = @ok, UltimoResultado = @resultado WHERE Id = @id",
            new { ok, resultado = Recortar(resultado, 500), id }, cancellationToken: ct));

        return (ok, resultado);
    }

    // Reconstruye la última corrida real (no agente) desde el snapshot y manda PDF + mail. NO corre el SP.
    private async Task<(bool Ok, string Resultado)> ReconstruirYEnviarAsync(string? parametrosJson, CancellationToken ct)
    {
        var p = ParseParametros(parametrosJson);
        var dest = (p.Destinatarios ?? "").Replace(',', ';');
        if (string.IsNullOrWhiteSpace(dest)) return (false, "La tarea no tiene destinatarios.");
        if (!_smtp.Configurado) return (false, "El SMTP no está configurado en el servidor.");

        var corridas = await _repo.ListarCorridasAsync(ct);
        var ultima = corridas.FirstOrDefault(c => !string.Equals(c.MachineName, "CLAUDE-AGENT", StringComparison.OrdinalIgnoreCase))
                     ?? corridas.FirstOrDefault();
        if (ultima is null) return (false, "No hay ninguna corrida de reposición guardada para reenviar.");

        var datos = await _repo.ReconstruirCorridaAsync(ultima.Id, ct);
        if (datos is null || datos.TotalArticulos == 0)
            return (false, $"La corrida #{ultima.Id} no tiene datos para reenviar.");

        var pdf = await _pdf.GenerarAsync(datos, null, ct);
        var asunto = "Reposición " + ultima.FechaHoraCorrida.ToString("dd/MM/yyyy");
        var body =
            "<p>Se reenvía el listado de packs a reponer de la corrida del " + ultima.FechaHoraCorrida.ToString("dd/MM/yyyy HH:mm") + ".</p>" +
            "<p>Reenviado el " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + " desde MARKET Web.</p>";
        var nombre = $"Reposicion_{ultima.FechaHoraCorrida:yyyyMMdd_HHmmss}.pdf";

        var enviado = await _smtp.EnviarAsync(dest, asunto, body, pdf, nombre, ct);
        return enviado
            ? (true, $"Corrida #{ultima.Id} ({datos.TotalArticulos} art · {datos.TotalPacks} packs) · PDF reenviado por mail.")
            : (false, "Falló el envío del mail.");
    }

    // === BACKUP: BACKUP DATABASE MARKET → comprimir el .bak a .rar (WinRAR) → retención + aviso si falla ===
    private async Task<(bool Ok, string Resultado)> EjecutarBackupAsync(string? parametrosJson, CancellationToken ct)
    {
        var p = ParseParametrosBackup(parametrosJson);
        var carpeta = (p.Carpeta ?? "").Trim();
        if (string.IsNullOrWhiteSpace(carpeta))
            return (false, "La tarea no tiene carpeta destino configurada.");

        var ts = DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss");
        var bak = Path.Combine(carpeta, $"MARKET_{ts}.bak");

        // 1) BACKUP DATABASE (lo escribe la cuenta de servicio de SQL; sin COMPRESSION, comprime WinRAR después).
        try
        {
            await using var cn = _db.Create();
            await cn.OpenAsync(ct);
            var sql = $"BACKUP DATABASE [MARKET] TO DISK = N'{bak.Replace("'", "''")}' WITH INIT, STATS = 10";
            await cn.ExecuteAsync(new CommandDefinition(sql, commandTimeout: 0, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            return await FallaBackupAsync(p, $"Falló el BACKUP DATABASE: {ex.Message}", ct);
        }

        if (!File.Exists(bak))
            return await FallaBackupAsync(p, "El BACKUP no generó el .bak (¿la cuenta de SQL puede escribir en la carpeta?).", ct);

        // 2) Comprimir a .rar con WinRAR (-df borra el .bak suelto al terminar).
        var rar = _cfg["Backup:RarPath"];
        if (string.IsNullOrWhiteSpace(rar)) rar = @"C:\Program Files\WinRAR\rar.exe";
        if (!File.Exists(rar))
            return await FallaBackupAsync(p, $"No se encontró rar.exe en '{rar}'. Configurá Backup:RarPath en el server.", ct);

        var destRar = Path.ChangeExtension(bak, ".rar");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = rar,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("a");     // agregar al archivo
            psi.ArgumentList.Add("-ep");   // sin rutas dentro del .rar
            psi.ArgumentList.Add("-df");   // borrar el .bak suelto al archivar
            psi.ArgumentList.Add("-m3");   // compresión normal
            psi.ArgumentList.Add("-y");    // asumir Sí
            psi.ArgumentList.Add(destRar);
            psi.ArgumentList.Add(bak);

            using var proc = Process.Start(psi)!;
            var outT = proc.StandardOutput.ReadToEndAsync(ct);
            var errT = proc.StandardError.ReadToEndAsync(ct);
            using var to = CancellationTokenSource.CreateLinkedTokenSource(ct);
            to.CancelAfter(TimeSpan.FromMinutes(60));
            try { await proc.WaitForExitAsync(to.Token); }
            catch (OperationCanceledException) { try { proc.Kill(true); } catch { } return await FallaBackupAsync(p, "WinRAR tardó demasiado (timeout 60 min).", ct); }
            var stderr = await errT; _ = await outT;
            if (proc.ExitCode != 0)
                return await FallaBackupAsync(p, $"WinRAR devolvió código {proc.ExitCode}. {stderr}".Trim(), ct);
        }
        catch (Exception ex)
        {
            return await FallaBackupAsync(p, $"Falló la compresión con WinRAR: {ex.Message}", ct);
        }

        // 3) Retención: borrar .rar más viejos que X días.
        int borrados = 0;
        if (p.RetencionDias > 0)
        {
            try
            {
                var limite = DateTime.Now.AddDays(-p.RetencionDias);
                foreach (var f in Directory.EnumerateFiles(carpeta, "MARKET_*.rar"))
                    if (File.GetLastWriteTime(f) < limite) { File.Delete(f); borrados++; }
            }
            catch { /* la retención no debe tumbar la tarea */ }
        }

        var mb = new FileInfo(destRar).Length / 1024d / 1024d;
        var resumen = $"Backup OK: {Path.GetFileName(destRar)} ({mb:N1} MB)";
        if (p.RetencionDias > 0) resumen += $" · retención {p.RetencionDias}d, {borrados} viejo(s) borrado(s)";
        return (true, resumen);
    }

    // Aviso por mail SÓLO si falla (si hay destinatarios y SMTP configurado). Siempre devuelve el fallo.
    private async Task<(bool Ok, string Resultado)> FallaBackupAsync(ParametrosBackup p, string msg, CancellationToken ct)
    {
        var dest = (p.MailFallo ?? "").Replace(',', ';').Trim();
        if (!string.IsNullOrWhiteSpace(dest) && _smtp.Configurado)
        {
            var body =
                "<p>El backup automático de la base <b>MARKET</b> falló.</p>" +
                $"<p>{System.Net.WebUtility.HtmlEncode(msg)}</p>" +
                $"<p>{DateTime.Now:dd/MM/yyyy HH:mm} — MARKET Web (tarea programada).</p>";
            try { await _smtp.EnviarAsync(dest, "⚠ Backup MARKET falló", body, ct); } catch { }
        }
        return (false, msg);
    }

    private static ParametrosReposicion ParseParametros(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ParametrosReposicion();
        try { return JsonSerializer.Deserialize<ParametrosReposicion>(json) ?? new ParametrosReposicion(); }
        catch { return new ParametrosReposicion(); }
    }

    private static ParametrosBackup ParseParametrosBackup(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ParametrosBackup();
        try { return JsonSerializer.Deserialize<ParametrosBackup>(json) ?? new ParametrosBackup(); }
        catch { return new ParametrosBackup(); }
    }

    // ISO: lunes=1 .. domingo=7.
    private static int IsoDow(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];
}
