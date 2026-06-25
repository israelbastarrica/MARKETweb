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

    public async Task<IReadOnlyList<ImpresoraColaDto>> ListarImpresorasAsync(CancellationToken ct = default)
    {
        // Impresoras (SALTAFW) presentes en la cola, con una IP representativa. Para el
        // selector "esta PC" de logística (cada equipo elige su impresora).
        const string sql = """
            SELECT SALTAFW AS Saltafw, MAX(RTRIM(IPImpresora)) AS Ip
            FROM   ImpresorRemito_Cola
            WHERE  ID > 776 AND SALTAFW IS NOT NULL
            GROUP BY SALTAFW
            ORDER BY SALTAFW;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<ImpresoraColaDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<RemitoColaDto>> ListarAsync(
        DateTime desde, DateTime hasta, string? localOrigen, string? estado, bool soloErrores, int? saltafw,
        bool soloAnulados = false, CancellationToken ct = default)
    {
        // Solo errores ignora el filtro de estado.
        var estadoFiltro = soloErrores ? null : (string.IsNullOrWhiteSpace(estado) || estado == "TODOS" ? null : estado);

        // Anulado = existe pedido de rechazo (Accion='RECHAZAR') en RemitoRecepcion para este
        // remito hacia su LocalDestino. soloAnulados=1 trae solo esos; =0 los excluye de la cola.
        const string sql = """
            SELECT TOP 2000
                   c.ID AS Id, RTRIM(c.RemitoCODIGO) AS RemitoCodigo, c.LocalOrigen, c.LocalDestino,
                   c.FPTOVEN AS Punto, c.FNUMCOMP AS NroComp, c.FechaEmision, c.Estado, c.Intentos,
                   c.ErrorMsg, c.FechaDetectado, c.FechaImpreso, c.IPImpresora AS IpImpresora, c.Reimpresiones, c.SALTAFW AS Saltafw,
                   Anulado       = CASE WHEN rr.RemitoCODIGO IS NULL THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END,
                   EstadoRechazo = rr.Estado,
                   FechaAnulado  = rr.FechaRecepcion
            FROM   ImpresorRemito_Cola c
            OUTER APPLY (
                SELECT TOP 1 RR.RemitoCODIGO, RR.Estado, RR.FechaRecepcion
                FROM   dbo.RemitoRecepcion RR WITH(NOLOCK)
                WHERE  RTRIM(RR.RemitoCODIGO) = RTRIM(c.RemitoCODIGO)
                  AND  RR.LocalRecepcion = RTRIM(c.LocalDestino)
                  AND  RR.Accion = 'RECHAZAR'
                ORDER BY RR.FechaRecepcion DESC
            ) rr
            WHERE  c.ID > 776
              AND  c.FechaDetectado >= @desde AND c.FechaDetectado < @hastaExcl
              AND  (@local IS NULL OR UPPER(RTRIM(c.LocalOrigen)) = UPPER(@local))
              AND  (@soloErrores = 0 OR c.Estado = 'ERROR')
              AND  (@estado IS NULL OR c.Estado = @estado)
              AND  (@saltafw IS NULL OR c.SALTAFW = @saltafw)
              AND  ( (@soloAnulados = 1 AND rr.RemitoCODIGO IS NOT NULL)
                  OR (@soloAnulados = 0 AND rr.RemitoCODIGO IS NULL) )
            ORDER BY FechaDetectado DESC, ID DESC;
            """;

        using var cn = _db.Create();
        var rows = await cn.QueryAsync<RemitoColaDto>(new CommandDefinition(sql, new
        {
            desde = desde.Date,
            hastaExcl = hasta.Date.AddDays(1),
            local = string.IsNullOrWhiteSpace(localOrigen) ? null : localOrigen.Trim(),
            estado = estadoFiltro,
            soloErrores = soloErrores ? 1 : 0,
            saltafw,
            soloAnulados = soloAnulados ? 1 : 0
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

    public async Task<bool> AnularRemitoAsync(int id, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        var row = await cn.QuerySingleOrDefaultAsync(new CommandDefinition(
            "SELECT RTRIM(RemitoCODIGO) AS Codigo, RTRIM(ISNULL(LocalOrigen,'')) AS Origen, RTRIM(ISNULL(LocalDestino,'')) AS Destino " +
            "FROM ImpresorRemito_Cola WHERE ID = @id", new { id }, cancellationToken: ct));
        if (row is null) return false;
        string codigo = (string)row.Codigo;
        string origen = (string)row.Origen;
        string destino = (string)row.Destino;
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(destino)) return false;

        // Deja en RemitoRecepcion el pedido de RECHAZO: el agente, cuando vea el remito ya
        // sincronizado en el local destino, llamará a RechazarMercaderia y el stock vuelve al origen.
        // Upsert por la UNIQUE (RemitoCODIGO, LocalRecepcion).
        var filas = await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.RemitoRecepcion SET Estado = 'PENDIENTE_API', Accion = 'RECHAZAR', Intentos = 0, " +
            "FechaRecepcion = GETDATE(), FechaProcesado = NULL WHERE RemitoCODIGO = @c AND LocalRecepcion = @d",
            new { c = codigo, d = destino }, cancellationToken: ct));
        if (filas == 0)
            await cn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.RemitoRecepcion (RemitoCODIGO, LocalOrigen, LocalRecepcion, FechaRecepcion, Estado, Accion) " +
                "VALUES (@c, @o, @d, GETDATE(), 'PENDIENTE_API', 'RECHAZAR')",
                new { c = codigo, o = origen, d = destino }, cancellationToken: ct));
        return true;
    }
}
