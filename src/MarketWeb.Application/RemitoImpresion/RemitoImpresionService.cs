using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.RemitoImpresion;

namespace MarketWeb.Application.RemitoImpresion;

public sealed class RemitoImpresionService : IRemitoImpresionService
{
    private readonly ISqlConnectionFactory _db;
    public RemitoImpresionService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<string>> ListarLocalesAsync(CancellationToken ct = default)
    {
        // Orígenes reales de la cola (CENTRAL/CCENTRAL/LURO/PERALTA). Logística filtra por CENTRAL.
        const string sql = "SELECT DISTINCT LocalOrigen FROM ImpresorRemito_Cola WHERE ID > 776 AND LocalOrigen IS NOT NULL AND LTRIM(RTRIM(LocalOrigen)) <> '' ORDER BY LocalOrigen;";
        using var cn = _db.Create();
        return (await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<RemitoColaDto>> ListarAsync(
        DateTime desde, DateTime hasta, string? localOrigen, string? estado, bool soloErrores, CancellationToken ct = default)
    {
        // Solo errores ignora el filtro de estado.
        var estadoFiltro = soloErrores ? null : (string.IsNullOrWhiteSpace(estado) || estado == "TODOS" ? null : estado);

        const string sql = """
            SELECT TOP 2000
                   ID AS Id, RTRIM(RemitoCODIGO) AS RemitoCodigo, LocalOrigen, LocalDestino,
                   FPTOVEN AS Punto, FNUMCOMP AS NroComp, FechaEmision, Estado, Intentos,
                   ErrorMsg, FechaDetectado, FechaImpreso, IPImpresora AS IpImpresora, Reimpresiones
            FROM   ImpresorRemito_Cola
            WHERE  ID > 776
              AND  FechaDetectado >= @desde AND FechaDetectado < @hastaExcl
              AND  (@local IS NULL OR UPPER(RTRIM(LocalOrigen)) = UPPER(@local))
              AND  (@soloErrores = 0 OR Estado = 'ERROR')
              AND  (@estado IS NULL OR Estado = @estado)
            ORDER BY FechaDetectado DESC, ID DESC;
            """;

        using var cn = _db.Create();
        var rows = await cn.QueryAsync<RemitoColaDto>(new CommandDefinition(sql, new
        {
            desde = desde.Date,
            hastaExcl = hasta.Date.AddDays(1),
            local = string.IsNullOrWhiteSpace(localOrigen) ? null : localOrigen.Trim(),
            estado = estadoFiltro,
            soloErrores = soloErrores ? 1 : 0
        }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<bool> ReimprimirAsync(int id, string? localOrigen, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE ImpresorRemito_Cola
            SET    Estado = 'PENDIENTE', Intentos = 0, ErrorMsg = NULL, Reimpresiones = Reimpresiones + 1
            WHERE  ID = @id
              AND  (@local IS NULL OR UPPER(RTRIM(LocalOrigen)) = UPPER(@local));
            """;
        using var cn = _db.Create();
        var filas = await cn.ExecuteAsync(new CommandDefinition(
            sql, new { id, local = string.IsNullOrWhiteSpace(localOrigen) ? null : localOrigen.Trim() }, cancellationToken: ct));
        return filas > 0;
    }

    public async Task<IReadOnlyList<RemitoEstadoDto>> EstadoAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0) return new List<RemitoEstadoDto>();
        const string sql = """
            SELECT ID AS Id, Estado, Intentos, FechaImpreso, ErrorMsg
            FROM   ImpresorRemito_Cola
            WHERE  ID IN @ids;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<RemitoEstadoDto>(new CommandDefinition(sql, new { ids }, cancellationToken: ct))).ToList();
    }
}
