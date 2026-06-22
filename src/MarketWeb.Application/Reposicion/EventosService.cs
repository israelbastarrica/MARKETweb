using System.Data;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Reposicion;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Reposicion;

/// <summary>
/// Porteo de frmRepoEventos / frmRepoEventoRemito: listado de eventos sobrante/faltante que cargan los
/// locales, su detalle (foto + items del remito) y las acciones procesar / eliminar.
/// Para los items del remito usa las bases DRAGONFISH del server principal (las réplicas LURO/PERALTA).
/// </summary>
public sealed class EventosService : IEventosService
{
    private readonly ISqlConnectionFactory _db;
    public EventosService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<EventoDto>> ListarAsync(string local, DateTime desde, DateTime hasta, bool verTodos, CancellationToken ct = default)
    {
        var filtraLocal = !string.IsNullOrWhiteSpace(local) && local != "TODOS";

        var sql =
            "SELECT ER.ID, RTRIM(ER.Local) AS Local, ER.FechaEvento, " +
            "       RTRIM(ISNULL(ER.ARTCOD,'')) AS ARTCOD, ER.DescripcionArt, " +
            "       RTRIM(ISNULL(ART.ARTDES,'')) AS ARTDESActual, ER.RemitoDisplay, " +
            "       RTRIM(ER.TipoDiferencia) AS TipoDiferencia, ER.CantidadPacks, " +
            "       CASE WHEN ER.Foto IS NULL THEN 0 ELSE 1 END AS TieneFoto, " +
            "       ER.Procesado " +
            "FROM MARKET.dbo.EventosReposicion ER " +
            "LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD) = RTRIM(ER.ARTCOD) " +
            "WHERE ER.FechaEvento >= @Desde AND ER.FechaEvento < DATEADD(DAY, 1, @Hasta) " +
            "  AND ISNULL(ER.Eliminado, 0) = 0 ";
        if (!verTodos) sql += "  AND ISNULL(ER.Procesado, 0) = 0 ";
        if (filtraLocal) sql += "  AND UPPER(RTRIM(ER.Local)) = UPPER(@Local) ";
        sql += "ORDER BY ER.FechaEvento DESC";

        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
        cmd.Parameters.Add("@Desde", SqlDbType.Date).Value = desde.Date;
        cmd.Parameters.Add("@Hasta", SqlDbType.Date).Value = hasta.Date;
        if (filtraLocal) cmd.Parameters.Add("@Local", SqlDbType.VarChar, 20).Value = local;

        var lista = new List<EventoDto>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            var descSnap = Str(rdr, "DescripcionArt");
            var descAct = Str(rdr, "ARTDESActual");
            var descripcion = descSnap;
            if (descAct != "" && !string.Equals(descAct, descSnap, StringComparison.OrdinalIgnoreCase))
                descripcion = descSnap + "  ►  " + descAct;

            var procesado = Convert.ToInt32(rdr["Procesado"]) == 1;

            lista.Add(new EventoDto
            {
                Id = Convert.ToInt32(rdr["ID"]),
                Fecha = Convert.ToDateTime(rdr["FechaEvento"]),
                Local = Str(rdr, "Local"),
                ArtCod = Str(rdr, "ARTCOD"),
                Descripcion = descripcion,
                RemitoDisplay = Str(rdr, "RemitoDisplay"),
                TipoDiferencia = Str(rdr, "TipoDiferencia"),
                CantidadPacks = rdr["CantidadPacks"] is DBNull or null ? "" : Convert.ToInt32(rdr["CantidadPacks"]).ToString("N0"),
                TieneFoto = Convert.ToInt32(rdr["TieneFoto"]) == 1,
                Estado = procesado ? "PROCESADO" : "PENDIENTE"
            });
        }
        return lista;
    }

    public async Task<EventoDetalleDto?> DetalleAsync(int id, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        EventoDetalleDto dto;
        string remitoCodigo, origen;
        const string sql =
            "SELECT ER.FechaEvento, RTRIM(ER.Local) AS Local, RTRIM(ER.TipoCodigo) AS TipoCodigo, " +
            "       ER.CodigoEscaneado, ER.DescripcionArt, ER.RemitoCODIGO, ER.RemitoDisplay, " +
            "       RTRIM(ER.TipoDiferencia) AS TipoDiferencia, ER.CantidadPacks, " +
            "       ER.Procesado, ER.Eliminado, ER.UsuarioApp, " +
            "       CASE WHEN ER.Foto IS NULL THEN 0 ELSE 1 END AS TieneFoto, " +
            "       UPPER(RTRIM(ISNULL(RD.LocalOrigen, ''))) AS OrigenRemito " +
            "FROM MARKET.dbo.EventosReposicion ER " +
            "LEFT JOIN MARKET.dbo.RemitosDespachados RD ON RD.CODIGO = ER.RemitoCODIGO AND RD.Eliminado = 0 " +
            "WHERE ER.ID = @ID";
        await using (var cmd = new SqlCommand(sql, cn))
        {
            cmd.Parameters.Add("@ID", SqlDbType.Int).Value = id;
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            if (!await rdr.ReadAsync(ct)) return null;

            dto = new EventoDetalleDto
            {
                Id = id,
                Fecha = Convert.ToDateTime(rdr["FechaEvento"]),
                Local = Str(rdr, "Local"),
                TipoCodigo = Str(rdr, "TipoCodigo"),
                TipoDiferencia = Str(rdr, "TipoDiferencia"),
                CodigoEscaneado = Str(rdr, "CodigoEscaneado"),
                DescripcionArt = Str(rdr, "DescripcionArt"),
                RemitoDisplay = Str(rdr, "RemitoDisplay"),
                CantidadPacks = rdr["CantidadPacks"] is DBNull or null ? "" : Convert.ToInt32(rdr["CantidadPacks"]).ToString("N0"),
                Usuario = Str(rdr, "UsuarioApp"),
                Procesado = Convert.ToInt32(rdr["Procesado"]) == 1,
                Eliminado = Convert.ToInt32(rdr["Eliminado"]) == 1,
                TieneFoto = Convert.ToInt32(rdr["TieneFoto"]) == 1
            };
            remitoCodigo = rdr["RemitoCODIGO"] is DBNull or null ? "" : rdr["RemitoCODIGO"].ToString()!.Trim();
            origen = Str(rdr, "OrigenRemito").ToUpperInvariant();
        }

        if (remitoCodigo != "")
            dto.Items = await CargarItemsAsync(cn, remitoCodigo, origen, ct);

        return dto;
    }

    // Bases DRAGONFISH del server principal según el origen del remito (réplicas para LURO/PERALTA).
    private static async Task<List<EventoItemDto>> CargarItemsAsync(SqlConnection cn, string remitoCodigo, string origen, CancellationToken ct)
    {
        var items = new List<EventoItemDto>();
        switch (origen)
        {
            case "CCENTRAL":
                await LeerItemsAsync(cn, "DRAGONFISH_CCENTRAL", remitoCodigo, items, ct);
                break;
            case "LURO":
                await LeerItemsAsync(cn, "DRAGONFISH_LURO", remitoCodigo, items, ct);
                break;
            case "PERALTA":
                await LeerItemsAsync(cn, "DRAGONFISH_PERALTA", remitoCodigo, items, ct);
                break;
            default: // CENTRAL o vacío
                await LeerItemsAsync(cn, "DRAGONFISH_CENTRAL", remitoCodigo, items, ct);
                if (items.Count == 0) await LeerItemsAsync(cn, "DRAGONFISH_CCENTRAL", remitoCodigo, items, ct);
                break;
        }
        return items;
    }

    private static async Task LeerItemsAsync(SqlConnection cn, string baseDF, string remitoCodigo, List<EventoItemDto> items, CancellationToken ct)
    {
        var sql =
            $"SELECT DET.FART, ART.ARTDES, DET.FCOLTXT, DET.TALLE, SUM(DET.FCANT) AS CANT " +
            $"FROM {baseDF}.[ZooLogic].[COMPROBANTEVDET] DET WITH(NOLOCK) " +
            $"LEFT JOIN {baseDF}.[ZooLogic].[ART] ART WITH(NOLOCK) ON DET.FART = ART.ARTCOD " +
            "WHERE DET.CODIGO = @COD " +
            "GROUP BY DET.FART, ART.ARTDES, DET.FCOLTXT, DET.TALLE " +
            "ORDER BY DET.FART, DET.FCOLTXT, DET.TALLE";
        try
        {
            await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@COD", remitoCodigo);
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                items.Add(new EventoItemDto
                {
                    ArtCod = (rdr["FART"]?.ToString() ?? "").Trim(),
                    Descripcion = rdr["ARTDES"] is DBNull or null ? "" : rdr["ARTDES"].ToString()!.Trim(),
                    Color = rdr["FCOLTXT"] is DBNull or null ? "" : rdr["FCOLTXT"].ToString()!.Trim(),
                    Talle = rdr["TALLE"] is DBNull or null ? "" : rdr["TALLE"].ToString()!.Trim(),
                    Cantidad = Convert.ToInt32(rdr["CANT"]).ToString("N0")
                });
            }
        }
        catch
        {
            // Items informativos: si la base no responde, dejamos la lista como está.
        }
    }

    public async Task<byte[]?> FotoAsync(int id, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT Foto FROM MARKET.dbo.EventosReposicion WHERE ID = @ID", cn);
        cmd.Parameters.Add("@ID", SqlDbType.Int).Value = id;
        var obj = await cmd.ExecuteScalarAsync(ct);
        return obj is DBNull or null ? null : (byte[])obj;
    }

    public async Task MarcarProcesadoAsync(int id, CancellationToken ct = default)
        => await EjecutarAsync("UPDATE MARKET.dbo.EventosReposicion SET Procesado = 1 WHERE ID = @ID", id, ct);

    public async Task EliminarAsync(int id, CancellationToken ct = default)
        => await EjecutarAsync("UPDATE MARKET.dbo.EventosReposicion SET Eliminado = 1 WHERE ID = @ID", id, ct);

    private async Task EjecutarAsync(string sql, int id, CancellationToken ct)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@ID", SqlDbType.Int).Value = id;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string Str(SqlDataReader r, string col)
        => r[col] is DBNull or null ? "" : r[col].ToString()!.Trim();
}
