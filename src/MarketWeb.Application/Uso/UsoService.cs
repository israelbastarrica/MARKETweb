using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Uso;

namespace MarketWeb.Application.Uso;

/// <summary>
/// Registro de uso por usuario en RegistroUsoWeb (Mail, Ruta, Contador, UltimoUso).
/// Tabla aditiva; solo se inserta/incrementa, nunca se borra.
/// </summary>
public sealed class UsoService : IUsoService
{
    private readonly ISqlConnectionFactory _db;
    public UsoService(ISqlConnectionFactory db) => _db = db;

    public async Task RegistrarAsync(string mail, string ruta, CancellationToken ct = default)
    {
        var m = (mail ?? "").Trim().ToLowerInvariant();
        var r = (ruta ?? "").Trim();
        if (m.Length == 0 || r.Length == 0 || r.Length > 200) return;

        const string sql = """
            UPDATE dbo.RegistroUsoWeb SET Contador = Contador + 1, UltimoUso = GETDATE()
            WHERE Mail = @m AND Ruta = @r;
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.RegistroUsoWeb (Mail, Ruta, Contador, UltimoUso)
                VALUES (@m, @r, 1, GETDATE());
            """;
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { m, r }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<string>> TopAsync(string mail, int n, CancellationToken ct = default)
    {
        var m = (mail ?? "").Trim().ToLowerInvariant();
        if (m.Length == 0) return Array.Empty<string>();
        if (n <= 0) n = 6;

        // Solo PANTALLAS (Titulo NULL); los registros puntuales (Titulo no nulo) van por Recientes.
        const string sql = """
            SELECT TOP (@n) Ruta
            FROM   dbo.RegistroUsoWeb
            WHERE  Mail = @m AND Titulo IS NULL
            ORDER BY Contador DESC, UltimoUso DESC;
            """;
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<string>(new CommandDefinition(sql, new { m, n }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task RegistrarRegistroAsync(string mail, string ruta, string titulo, CancellationToken ct = default)
    {
        var m = (mail ?? "").Trim().ToLowerInvariant();
        var r = (ruta ?? "").Trim();
        var t = (titulo ?? "").Trim();
        if (m.Length == 0 || r.Length == 0 || t.Length == 0 || r.Length > 200) return;
        if (t.Length > 200) t = t[..200];

        const string sql = """
            UPDATE dbo.RegistroUsoWeb SET Contador = Contador + 1, UltimoUso = GETDATE(), Titulo = @t
            WHERE Mail = @m AND Ruta = @r;
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.RegistroUsoWeb (Mail, Ruta, Contador, UltimoUso, Titulo)
                VALUES (@m, @r, 1, GETDATE(), @t);
            """;
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { m, r, t }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<RegistroRecienteDto>> RecientesAsync(string mail, int n, CancellationToken ct = default)
    {
        var m = (mail ?? "").Trim().ToLowerInvariant();
        if (m.Length == 0) return Array.Empty<RegistroRecienteDto>();
        if (n <= 0) n = 5;

        // Registros puntuales (con título) abiertos en los últimos 14 días, por recencia.
        const string sql = """
            SELECT TOP (@n) Ruta, Titulo, UltimoUso
            FROM   dbo.RegistroUsoWeb
            WHERE  Mail = @m AND Titulo IS NOT NULL
              AND  UltimoUso >= DATEADD(DAY, -14, GETDATE())
            ORDER BY UltimoUso DESC;
            """;
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<RegistroRecienteDto>(new CommandDefinition(sql, new { m, n }, cancellationToken: ct));
        return rows.ToList();
    }
}
