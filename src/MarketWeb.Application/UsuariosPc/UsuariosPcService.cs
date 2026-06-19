using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Shared.UsuariosPc;

namespace MarketWeb.Application.UsuariosPc;

/// <summary>
/// ABM de UsuariosPC (mapeo PC → Perfil). El desktop no tenía pantalla para esto
/// (se cargaba a mano). Valida PC única y respeta baja lógica.
/// </summary>
public sealed class UsuariosPcService : IUsuariosPcService
{
    private readonly ISqlConnectionFactory _db;

    public UsuariosPcService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<UsuarioPcDto>> ListarAsync(string? filtro, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID AS Id, PC AS Pc, PERFIL AS Perfil, Mail, MailAprobado
            FROM   UsuariosPC
            WHERE  Eliminado = 0
              AND  (@filtro IS NULL OR PC LIKE '%' + @filtro + '%' OR PERFIL LIKE '%' + @filtro + '%' OR Mail LIKE '%' + @filtro + '%')
            ORDER BY PC;
            """;
        using var cn = _db.Create();
        var f = string.IsNullOrWhiteSpace(filtro) ? null : filtro.Trim();
        var rows = await cn.QueryAsync<UsuarioPcDto>(new CommandDefinition(sql, new { filtro = f }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<string>> ListarPerfilesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT DISTINCT PERFIL
            FROM   UsuariosPC
            WHERE  Eliminado = 0 AND PERFIL IS NOT NULL AND LTRIM(RTRIM(PERFIL)) <> ''
            ORDER BY PERFIL;
            """;
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<UsuarioPcDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        const string sql = "SELECT ID AS Id, PC AS Pc, PERFIL AS Perfil, Mail, MailAprobado FROM UsuariosPC WHERE ID = @id AND Eliminado = 0;";
        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<UsuarioPcDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<UsuarioPcDto?> ObtenerPorMailAsync(string mail, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 ID AS Id, PC AS Pc, PERFIL AS Perfil, Mail, MailAprobado
            FROM   UsuariosPC
            WHERE  Eliminado = 0 AND Mail = @mail
            ORDER BY ID;
            """;
        using var cn = _db.Create();
        var m = (mail ?? "").Trim().ToLowerInvariant();
        return await cn.QuerySingleOrDefaultAsync<UsuarioPcDto>(new CommandDefinition(sql, new { mail = m }, cancellationToken: ct));
    }

    public async Task<int> CrearAsync(UsuarioPcSaveRequest req, CancellationToken ct = default)
    {
        var pc = (req.Pc ?? "").Trim();
        var perfil = (req.Perfil ?? "").Trim();
        var mail = NormalizarMail(req.Mail);
        using var cn = _db.Create();
        await ValidarPcDuplicadaAsync(cn, pc, idExcluir: null, ct);

        var auditoria = ConstruirAuditoria("Alta de registro");
        var aprobado = mail is not null; // si lo carga el admin, queda aprobado
        const string sql = """
            INSERT INTO UsuariosPC (PC, PERFIL, Mail, MailAprobado, Eliminado, Auditoria)
            OUTPUT INSERTED.ID
            VALUES (@pc, @perfil, @mail, @aprobado, 0, @auditoria);
            """;
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { pc, perfil, mail, aprobado, auditoria }, cancellationToken: ct));
    }

    public async Task ModificarAsync(int id, UsuarioPcSaveRequest req, CancellationToken ct = default)
    {
        var pc = (req.Pc ?? "").Trim();
        var perfil = (req.Perfil ?? "").Trim();
        var mail = NormalizarMail(req.Mail);
        using var cn = _db.Create();
        await ValidarPcDuplicadaAsync(cn, pc, idExcluir: id, ct);

        var aprobado = mail is not null; // editado por el admin → aprobado
        const string sql = "UPDATE UsuariosPC SET PC = @pc, PERFIL = @perfil, Mail = @mail, MailAprobado = @aprobado WHERE ID = @id;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id, pc, perfil, mail, aprobado }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<UsuarioPcDto>> ListarDisponiblesAsync(CancellationToken ct = default)
    {
        // PCs sin mail asignado (para que el usuario elija la suya en el onboarding).
        const string sql = "SELECT ID AS Id, PC AS Pc FROM UsuariosPC WHERE Eliminado = 0 AND Mail IS NULL ORDER BY PC;";
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<UsuarioPcDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task ReclamarPcAsync(int pcId, string mail, CancellationToken ct = default)
    {
        var m = (mail ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(m)) throw new BusinessException("Mail inválido.");

        using var cn = _db.Create();

        // El mail no puede estar ya asociado a otra PC.
        var yaAsignado = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM UsuariosPC WHERE Eliminado = 0 AND Mail = @m;", new { m }, cancellationToken: ct));
        if (yaAsignado > 0)
            throw new BusinessException("Tu cuenta ya está asociada a una PC. Avisá a Sistemas si necesitás cambiarla.");

        // Toma la PC solo si sigue libre (sin mail). Queda PENDIENTE de aprobación.
        var aud = ConstruirAuditoria("Auto-asignación web (pendiente)");
        var filas = await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE UsuariosPC SET Mail = @m, MailAprobado = 0, Auditoria = @aud WHERE ID = @pcId AND Eliminado = 0 AND Mail IS NULL;",
            new { m, pcId, aud }, cancellationToken: ct));
        if (filas == 0)
            throw new BusinessException("Esa PC ya fue tomada o no existe. Elegí otra.");
    }

    public async Task SolicitarAccesoAsync(string mail, string perfil, CancellationToken ct = default)
    {
        var m = NormalizarMail(mail);
        if (m is null) throw new BusinessException("Mail inválido.");
        var p = (perfil ?? "").Trim();
        if (p.Length == 0) throw new BusinessException("Elegí tu área o local.");

        using var cn = _db.Create();

        // El mail no puede tener ya una fila (solicitud o cuenta).
        var existe = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM UsuariosPC WHERE Eliminado = 0 AND Mail = @m;", new { m }, cancellationToken: ct));
        if (existe > 0)
            throw new BusinessException("Ya hay una solicitud o cuenta con este mail. Avisá a Sistemas.");

        // Alta como solicitud web (sin PC física). PC = mail como referencia legible
        // (entra en varchar(100)); queda PENDIENTE para que el admin revise el perfil y apruebe.
        var pc = m.Length <= 100 ? m : null;
        var aud = ConstruirAuditoria("Solicitud de acceso web (pendiente)");
        const string sql = """
            INSERT INTO UsuariosPC (PC, PERFIL, Mail, MailAprobado, Eliminado, Auditoria)
            VALUES (@pc, @p, @m, 0, 0, @aud);
            """;
        await cn.ExecuteAsync(new CommandDefinition(sql, new { pc, p, m, aud }, cancellationToken: ct));
    }

    public async Task AprobarAsync(int id, CancellationToken ct = default)
    {
        const string sql = "UPDATE UsuariosPC SET MailAprobado = 1 WHERE ID = @id;";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<AccesoResultado> ResolverAccesoAsync(string mail, CancellationToken ct = default)
    {
        var match = await ObtenerPorMailAsync(mail, ct);
        if (match is null)
            return new AccesoResultado("onboarding", null, null);

        if (match.MailAprobado)
            return new AccesoResultado("ok", match.Perfil, match.Pc);

        // Bootstrap: si la fila es ADMIN y todavía no hay ningún ADMIN aprobado,
        // se auto-aprueba (para no quedar nunca sin un admin que apruebe al resto).
        if (string.Equals(match.Perfil, "ADMIN", StringComparison.OrdinalIgnoreCase)
            && !await ExisteAdminAprobadoAsync(ct))
        {
            await AprobarAsync(match.Id, ct);
            return new AccesoResultado("ok", match.Perfil, match.Pc);
        }

        return new AccesoResultado("pendiente", null, match.Pc);
    }

    private async Task<bool> ExisteAdminAprobadoAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(1) FROM UsuariosPC
            WHERE Eliminado = 0 AND PERFIL = 'ADMIN' AND MailAprobado = 1 AND Mail IS NOT NULL;
            """;
        using var cn = _db.Create();
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, cancellationToken: ct)) > 0;
    }

    // Mail vacío -> NULL; si viene, normalizado a minúsculas.
    private static string? NormalizarMail(string? mail)
    {
        var m = (mail ?? "").Trim().ToLowerInvariant();
        return m.Length == 0 ? null : m;
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var auditoria = ConstruirAuditoria("Eliminación de registro");
        const string sql = "UPDATE UsuariosPC SET Eliminado = 1, Auditoria = @auditoria WHERE ID = @id;";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id, auditoria }, cancellationToken: ct));
    }

    private static async Task ValidarPcDuplicadaAsync(
        Microsoft.Data.SqlClient.SqlConnection cn, string pc, int? idExcluir, CancellationToken ct)
    {
        const string sql = """
            SELECT COUNT(1) FROM UsuariosPC
            WHERE Eliminado = 0 AND PC = @pc
              AND (@idExcluir IS NULL OR ID <> @idExcluir);
            """;
        var existe = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { pc, idExcluir }, cancellationToken: ct));
        if (existe > 0)
            throw new BusinessException("Ya existe un registro para esa PC.");
    }

    private static string ConstruirAuditoria(string accion) =>
        $"{accion} | WEB | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
}
