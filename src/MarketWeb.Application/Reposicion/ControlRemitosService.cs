using System.Data;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Reposicion;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Reposicion;

/// <summary>
/// Porteo de frmRepoControlRemitos / frmRepoControlRemitosDetalle: estado de control de remitos
/// (generados/despachados/recibidos/no recibidos/aceptados) y detalle remito por remito con sus
/// estados, contenido y acciones (reasignar destino / eliminar despacho). Se apoya en los SPs existentes.
/// Quedan fuera (nicho) la foto del QR de pantalla y el log de QR generados.
/// </summary>
public sealed class ControlRemitosService : IControlRemitosService
{
    private readonly ISqlConnectionFactory _db;
    public ControlRemitosService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<ControlEstadoDto>> EstadoAsync(DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SP_RemitosControlEstado", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("@FechaDesde", desde.Date);
        cmd.Parameters.AddWithValue("@FechaHasta", hasta.Date);

        var lista = new List<ControlEstadoDto>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            lista.Add(new ControlEstadoDto
            {
                FechaRemito = Convert.ToDateTime(rdr["FechaRemito"]),
                LocalDestino = rdr["LocalDestino"]?.ToString() ?? "",
                Generados = Convert.ToInt32(rdr["Generados"]),
                Despachados = Convert.ToInt32(rdr["Despachados"]),
                Recibidos = Convert.ToInt32(rdr["Recibidos"]),
                NoRecibidos = Convert.ToInt32(rdr["NoRecibidos"]),
                Aceptados = Convert.ToInt32(rdr["Aceptados"])
            });
        }
        return lista;
    }

    public async Task<IReadOnlyList<RemitoControlDto>> ListadoAsync(DateTime desde, DateTime hasta, int idDestino, string estado, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SP_RemitosControlListado", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };
        cmd.Parameters.AddWithValue("@FechaDesde", desde.Date);
        cmd.Parameters.AddWithValue("@FechaHasta", hasta.Date);
        cmd.Parameters.AddWithValue("@IDLocalDestino", idDestino > 0 ? idDestino : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", string.IsNullOrWhiteSpace(estado) || estado == "TODOS" ? (object)DBNull.Value : estado);

        var lista = new List<RemitoControlDto>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            var estadoTxt = rdr["Estado"]?.ToString() ?? "";
            var idDest = Int(rdr, "IDLocalDestino");
            var despachoId = Int(rdr, "DespachoID");
            var idDestDespacho = Int(rdr, "IDLocalDestinoDespacho");
            var esQR = !IsNull(rdr, "EsQRDePantalla") && Convert.ToBoolean(rdr["EsQRDePantalla"]);
            var estadoDragon = IsNull(rdr, "EstadoDragon") ? "NO ACEPTADO" : rdr["EstadoDragon"].ToString()!;
            var estadoDespacho = rdr["EstadoDespacho"]?.ToString() ?? "";

            var esCruzado = despachoId > 0 && idDestDespacho != idDest && idDestDespacho > 0;
            if (esCruzado)
            {
                var destDespacho = IsNull(rdr, "DestinoDespacho") ? "" : rdr["DestinoDespacho"].ToString();
                estadoDespacho = "CRUZADO → " + destDespacho;
            }

            var estadoMostrado = estadoTxt;
            if (esQR && estadoTxt == "RECIBIDO") estadoMostrado = "RECIBIDO POR QR PC";

            lista.Add(new RemitoControlDto
            {
                NroRemito = (rdr["NroRemito"]?.ToString() ?? "").Trim(),
                FechaRemito = Fecha(rdr, "FechaRemito"),
                Origen = rdr["Origen"]?.ToString() ?? "",
                Destino = rdr["Destino"]?.ToString() ?? "",
                EstadoDespacho = estadoDespacho,
                FechaDespacho = Fecha(rdr, "FechaDespacho"),
                UsuarioDespacho = IsNull(rdr, "UsuarioAppDespacho") ? "" : rdr["UsuarioAppDespacho"].ToString()!.Trim(),
                Estado = estadoMostrado,
                FechaEscaneo = Fecha(rdr, "FechaEscaneo"),
                UsuarioApp = IsNull(rdr, "UsuarioApp") ? "" : rdr["UsuarioApp"].ToString()!.Trim(),
                RemitoId = (rdr["RemitoID"]?.ToString() ?? "").Trim(),
                DespachoId = despachoId,
                IdLocalDestinoDespacho = idDestDespacho,
                ColorHint = ColorHint(estadoTxt, esQR, esCruzado, estadoDragon)
            });
        }
        return lista;
    }

    private static string ColorHint(string estado, bool esQR, bool esCruzado, string estadoDragon)
    {
        if (esCruzado) return "cruzado";
        if (esQR && estado == "RECIBIDO") return "qrpc";
        return estado switch
        {
            "RECIBIDO" => estadoDragon == "ACEPTADO COMPLETO" ? "aceptado" : "recibido",
            "NO RECIBIDO" => "norecibido",
            _ => "normal"
        };
    }

    public async Task<IReadOnlyList<EventoItemDto>> ContenidoAsync(string remitoId, string origen, CancellationToken ct = default)
    {
        var items = new List<EventoItemDto>();
        if (string.IsNullOrWhiteSpace(remitoId)) return items;

        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        switch ((origen ?? "").ToUpperInvariant())
        {
            case "LURO":
                await LeerAsync(cn, "DRAGONFISH_LURO", remitoId, items, ct);
                break;
            case "PERALTA":
                await LeerAsync(cn, "DRAGONFISH_PERALTA", remitoId, items, ct);
                break;
            default: // CENTRAL (incluye CCENTRAL unificado)
                await LeerAsync(cn, "DRAGONFISH_CENTRAL", remitoId, items, ct);
                if (items.Count == 0) await LeerAsync(cn, "DRAGONFISH_CCENTRAL", remitoId, items, ct);
                break;
        }
        return items;
    }

    private static async Task LeerAsync(SqlConnection cn, string baseDF, string remitoId, List<EventoItemDto> items, CancellationToken ct)
    {
        var sql =
            $"SELECT DET.FART, ART.ARTDES, DET.FCOLTXT, DET.TALLE, SUM(DET.FCANT) AS CANT " +
            $"FROM {baseDF}.[ZooLogic].[COMPROBANTEVDET] DET WITH(NOLOCK) " +
            $"LEFT JOIN {baseDF}.[ZooLogic].[ART] ART WITH(NOLOCK) ON DET.FART = ART.ARTCOD " +
            "WHERE DET.CODIGO = @COD GROUP BY DET.FART, ART.ARTDES, DET.FCOLTXT, DET.TALLE " +
            "ORDER BY DET.FART, DET.FCOLTXT, DET.TALLE";
        try
        {
            await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@COD", remitoId);
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                items.Add(new EventoItemDto
                {
                    ArtCod = (rdr["FART"]?.ToString() ?? "").Trim(),
                    Descripcion = IsNull(rdr, "ARTDES") ? "" : rdr["ARTDES"].ToString()!.Trim(),
                    Color = IsNull(rdr, "FCOLTXT") ? "" : rdr["FCOLTXT"].ToString()!.Trim(),
                    Talle = IsNull(rdr, "TALLE") ? "" : rdr["TALLE"].ToString()!.Trim(),
                    Cantidad = Convert.ToInt32(rdr["CANT"]).ToString("N0")
                });
            }
        }
        catch { /* base sin ese remito o no disponible */ }
    }

    public async Task ReasignarDestinoAsync(int despachoId, int nuevoIdDestino, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SP_RemitosDespachadosReasignarDestino", cn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@ID", despachoId);
        cmd.Parameters.AddWithValue("@IDLocalDestino", nuevoIdDestino);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task EliminarDespachoAsync(int despachoId, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SP_RemitosDespachadosEliminar", cn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@ID", despachoId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static bool IsNull(SqlDataReader r, string c) => r[c] is DBNull or null;
    private static int Int(SqlDataReader r, string c) => IsNull(r, c) ? 0 : Convert.ToInt32(r[c]);
    private static DateTime? Fecha(SqlDataReader r, string c) => IsNull(r, c) ? null : Convert.ToDateTime(r[c]);
}
