using System.Data;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Reposicion;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Reposicion;

/// <summary>
/// Porteo de frmRepoReposicion.Calcular(): ejecuta SP_RepoCalcularPacks, arma las filas,
/// detecta los huérfanos insertados en ESTA corrida (RepoReemplazos con Fecha >= inicioRun)
/// y calcula los totales del footer (solo sobre reposición, no huérfanos).
/// </summary>
public sealed class ReposicionService : IReposicionService
{
    private readonly ISqlConnectionFactory _db;
    public ReposicionService(ISqlConnectionFactory db) => _db = db;

    public async Task<ReposicionResultadoDto> CalcularAsync(ReposicionCalcularRequest req, string machineName, CancellationToken ct = default)
    {
        var local = string.IsNullOrWhiteSpace(req.Local) ? "TODOS" : req.Local.Trim().ToUpperInvariant();

        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        // Reloj del servidor ANTES del SP. El SP usa GETDATE() al insertar en RepoReemplazos,
        // así que todo lo que aparezca con Fecha >= este marker es lo nuevo de esta corrida.
        DateTime inicioRun;
        await using (var cmdNow = new SqlCommand("SELECT GETDATE()", cn))
            inicioRun = Convert.ToDateTime(await cmdNow.ExecuteScalarAsync(ct));

        var filas = new List<ReposicionFilaDto>();
        await using (var cmd = new SqlCommand("SP_RepoCalcularPacks", cn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 300
        })
        {
            cmd.Parameters.AddWithValue("@Local", local);
            if (req.FechaCorte.HasValue)
                cmd.Parameters.Add("@FechaCorte", SqlDbType.Date).Value = req.FechaCorte.Value.Date;
            else
                cmd.Parameters.Add("@FechaCorte", SqlDbType.Date).Value = DBNull.Value;
            cmd.Parameters.Add("@GenerarReemplazos", SqlDbType.Bit).Value = req.GenerarReemplazos ? 1 : 0;
            cmd.Parameters.Add("@MachineName", SqlDbType.NVarChar, 100).Value = machineName;

            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var esVirtual = Convert.ToBoolean(rdr["EsVirtual"]);
                var ultNro = (rdr["UltRemitoNro"]?.ToString() ?? "").Trim();
                var ultFecha = Convert.ToDateTime(rdr["UltRemitoFecha"]);
                var ultHora = (rdr["UltRemitoHora"]?.ToString() ?? "").Trim();

                string ultTexto;
                if (esVirtual)
                {
                    ultTexto = "(virtual) " + ultFecha.ToString("dd/MM/yyyy");
                }
                else
                {
                    ultTexto = ultNro + "  " + ultFecha.ToString("dd/MM/yyyy");
                    if (ultHora != "") ultTexto += " " + ultHora;
                }

                filas.Add(new ReposicionFilaDto
                {
                    EsVirtual = esVirtual,
                    CantPack = Convert.ToInt32(rdr["CantPack"]),
                    Pendiente = Convert.ToInt32(rdr["Pendiente"]),
                    Packs = Convert.ToInt32(rdr["PacksAReponer"]),
                    UltRemitoNro = ultNro,
                    UltRemitoFecha = ultFecha,
                    UltRemitoHora = ultHora,
                    UltRemitoTexto = ultTexto,
                    EsHuerfano = Convert.ToBoolean(rdr["EsHuerfano"]),
                    LocalDestino = (rdr["LocalDestino"]?.ToString() ?? "").Trim(),
                    ArtCod = (rdr["ARTCOD"]?.ToString() ?? "").Trim(),
                    ArtDes = rdr["ARTDES"]?.ToString() ?? "",
                    TipoArt = Str(rdr, "TipoArt"),
                    Categoria = Str(rdr, "Categoria"),
                    Combo = Str(rdr, "Combo"),
                    Mobiliario = Str(rdr, "Mobiliario"),
                    UbicacionesLocal = Str(rdr, "UbicacionesLocal"),
                    UbicacionDeposito = rdr["UbicacionDeposito"]?.ToString() ?? ""
                });
            }
        }

        // Huérfanos que el SP insertó en RepoReemplazos durante esta corrida.
        var nuevos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd2 = new SqlCommand(
            "SELECT UPPER(RTRIM(U.Descripcion)) AS LocalDestino, RTRIM(R.ARTCOD) AS ARTCOD " +
            "FROM MARKET.dbo.RepoReemplazos R " +
            "INNER JOIN MARKET.dbo.Ubicaciones U ON U.ID = R.IDUbicacion " +
            "WHERE R.Fecha >= @inicio AND R.Eliminado = 0", cn))
        {
            cmd2.Parameters.Add("@inicio", SqlDbType.DateTime).Value = inicioRun;
            await using var rdr2 = await cmd2.ExecuteReaderAsync(ct);
            while (await rdr2.ReadAsync(ct))
                nuevos.Add(rdr2.GetString(0).Trim() + "|" + rdr2.GetString(1).Trim());
        }

        int totArt = 0, totPacks = 0, totPrendas = 0;
        foreach (var f in filas)
        {
            if (f.EsHuerfano)
            {
                f.NuevoEstaCorrida = nuevos.Contains(f.LocalDestino.Trim() + "|" + f.ArtCod.Trim());
            }
            else
            {
                totArt++;
                totPacks += f.Packs;
                totPrendas += f.Packs * f.CantPack;
            }
        }

        return new ReposicionResultadoDto
        {
            Filas = filas,
            TotalArticulos = totArt,
            TotalPacks = totPacks,
            TotalPrendas = totPrendas
        };
    }

    private static string Str(SqlDataReader r, string col)
        => r[col] is DBNull or null ? "" : r[col].ToString()!.Trim();

    public async Task<IReadOnlyList<CorridaDto>> ListarCorridasAsync(CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        var lista = new List<CorridaDto>();
        const string sql =
            "SELECT TOP 200 ID, FechaHoraCorrida, LocalParam, TotalArticulos, TotalPacks, TotalPrendas, MachineName " +
            "FROM MARKET.dbo.Reposicion WHERE ISNULL(Eliminado, 0) = 0 ORDER BY FechaHoraCorrida DESC";
        await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            lista.Add(new CorridaDto
            {
                Id = Convert.ToInt32(rdr["ID"]),
                FechaHoraCorrida = Convert.ToDateTime(rdr["FechaHoraCorrida"]),
                LocalParam = rdr["LocalParam"]?.ToString() ?? "",
                TotalArticulos = Convert.ToInt32(rdr["TotalArticulos"]),
                TotalPacks = Convert.ToInt32(rdr["TotalPacks"]),
                TotalPrendas = Convert.ToInt32(rdr["TotalPrendas"]),
                MachineName = rdr["MachineName"] is DBNull or null ? "" : rdr["MachineName"].ToString()!
            });
        }
        return lista;
    }

    public async Task<ReposicionResultadoDto?> ReconstruirCorridaAsync(int idReposicion, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        // Cabecera (LocalParam acota los reemplazos; la fecha de la corrida los filtra).
        DateTime fechaCorrida;
        string localParam;
        await using (var cmdCab = new SqlCommand(
            "SELECT FechaHoraCorrida, LocalParam FROM MARKET.dbo.Reposicion WHERE ID = @ID", cn))
        {
            cmdCab.Parameters.Add("@ID", SqlDbType.Int).Value = idReposicion;
            await using var rc = await cmdCab.ExecuteReaderAsync(ct);
            if (!await rc.ReadAsync(ct)) return null;
            fechaCorrida = Convert.ToDateTime(rc["FechaHoraCorrida"]);
            localParam = rc["LocalParam"]?.ToString() ?? "";
        }

        var filas = new List<ReposicionFilaDto>();

        // Detalle (reposición) ordenado por ubicación de depósito real (mapeo vigente, IDUbicacion=1).
        const string sqlDet =
            "SELECT d.LocalDestino, d.UbicacionDeposito, d.UbicacionesLocal, d.ARTCOD, d.ARTDES, " +
            "       d.TipoArt, d.Categoria, d.Combo, d.Mobiliario, d.CantPack, " +
            "       d.UltRemitoNro, d.UltRemitoFecha, d.UltRemitoHora, d.EsVirtual, d.Pendiente, d.PacksAReponer " +
            "FROM MARKET.dbo.ReposicionDetalle d " +
            "OUTER APPLY ( " +
            "    SELECT OrdenPasillo = MIN(m.OrdenPasillo), Fila = MIN(m.Fila), Posicion = MIN(m.Posicion) " +
            "    FROM MARKET.dbo.Mapeo m " +
            "    WHERE m.IDUbicacion = 1 AND m.Eliminado = 0 AND RTRIM(m.Modulo) = RTRIM(d.UbicacionDeposito) " +
            ") u " +
            "WHERE d.IDReposicion = @ID " +
            "ORDER BY d.LocalDestino, ISNULL(u.OrdenPasillo, 2147483647), d.UbicacionDeposito, " +
            "         ISNULL(u.Fila, 2147483647), ISNULL(u.Posicion, 2147483647), d.ARTCOD";
        await using (var cmd = new SqlCommand(sqlDet, cn) { CommandTimeout = 120 })
        {
            cmd.Parameters.Add("@ID", SqlDbType.Int).Value = idReposicion;
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var esVirtual = Convert.ToBoolean(rdr["EsVirtual"]);
                var ultNro = (rdr["UltRemitoNro"]?.ToString() ?? "").Trim();
                var ultFecha = Convert.ToDateTime(rdr["UltRemitoFecha"]);
                var ultHora = (rdr["UltRemitoHora"]?.ToString() ?? "").Trim();
                string ultTexto = esVirtual
                    ? "(virtual) " + ultFecha.ToString("dd/MM/yyyy")
                    : ultNro + "  " + ultFecha.ToString("dd/MM/yyyy") + (ultHora != "" ? " " + ultHora : "");

                filas.Add(new ReposicionFilaDto
                {
                    EsHuerfano = false,
                    EsVirtual = esVirtual,
                    LocalDestino = (rdr["LocalDestino"]?.ToString() ?? "").Trim(),
                    UbicacionDeposito = rdr["UbicacionDeposito"]?.ToString() ?? "",
                    UbicacionesLocal = Str(rdr, "UbicacionesLocal"),
                    ArtCod = (rdr["ARTCOD"]?.ToString() ?? "").Trim(),
                    ArtDes = rdr["ARTDES"]?.ToString() ?? "",
                    TipoArt = Str(rdr, "TipoArt"),
                    Categoria = Str(rdr, "Categoria"),
                    Combo = Str(rdr, "Combo"),
                    Mobiliario = Str(rdr, "Mobiliario"),
                    CantPack = Convert.ToInt32(rdr["CantPack"]),
                    Pendiente = Convert.ToInt32(rdr["Pendiente"]),
                    Packs = Convert.ToInt32(rdr["PacksAReponer"]),
                    UltRemitoNro = ultNro,
                    UltRemitoFecha = ultFecha,
                    UltRemitoHora = ultHora,
                    UltRemitoTexto = ultTexto
                });
            }
        }

        // Reemplazos (huérfanos) de RepoReemplazos del día de la corrida. ARTDES/Tipo/Categoría/Combo de
        // DRAGONFISH_CENTRAL.ART, Mobiliario del Mapeo del local. PacksAReponer embebido en Auditoria.
        const string sqlReemp =
            "SELECT LocalDestino = U.Descripcion, R.ARTCOD, " +
            "       ARTDES = ISNULL(RTRIM(ART.ARTDES), ''), TipoArt = ISNULL(RTRIM(TIPO.DESCRIP), ''), " +
            "       Categoria = ISNULL(RTRIM(CATE.DESCRIP), ''), Combo = ISNULL(RTRIM(ART.CLASIFART), ''), " +
            "       Mobiliario = ISNULL(MAP.Mobiliario, ''), " +
            "       PacksAReponer = TRY_CAST(SUBSTRING(R.Auditoria, CHARINDEX('PacksAReponer=', R.Auditoria) + 14, 10) AS INT) " +
            "FROM MARKET.dbo.RepoReemplazos R " +
            "INNER JOIN MARKET.dbo.Ubicaciones U ON U.ID = R.IDUbicacion " +
            "LEFT JOIN MARKET.dbo.Mapeo MAP ON MAP.ID = R.IDMapeoLocal " +
            "LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD) = RTRIM(R.ARTCOD) " +
            "LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.TIPOART TIPO ON TIPO.COD = ART.TIPOARTI " +
            "LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.CATEGART CATE ON CATE.COD = ART.CATEARTI " +
            "WHERE R.Eliminado = 0 AND CAST(R.Fecha AS DATE) = @Fecha " +
            "  AND (@LocalParam = 'TODOS' OR UPPER(RTRIM(U.Descripcion)) = UPPER(@LocalParam)) " +
            "  AND R.ARTCODReemplazo IS NOT NULL AND RTRIM(R.ARTCODReemplazo) <> '' AND ISNULL(R.Procesado, 0) = 0 " +
            "ORDER BY U.Descripcion, R.ARTCOD";
        try
        {
            await using var cmd = new SqlCommand(sqlReemp, cn) { CommandTimeout = 120 };
            cmd.Parameters.Add("@Fecha", SqlDbType.Date).Value = fechaCorrida.Date;
            cmd.Parameters.Add("@LocalParam", SqlDbType.NVarChar, 20).Value =
                string.IsNullOrWhiteSpace(localParam) ? "TODOS" : localParam.Trim();
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                filas.Add(new ReposicionFilaDto
                {
                    EsHuerfano = true,
                    NuevoEstaCorrida = true,     // para que el PDF lo incluya
                    LocalDestino = (rdr["LocalDestino"]?.ToString() ?? "").Trim(),
                    ArtCod = (rdr["ARTCOD"]?.ToString() ?? "").Trim(),
                    ArtDes = rdr["ARTDES"]?.ToString() ?? "",
                    TipoArt = Str(rdr, "TipoArt"),
                    Categoria = Str(rdr, "Categoria"),
                    Combo = Str(rdr, "Combo"),
                    Mobiliario = Str(rdr, "Mobiliario"),
                    Packs = rdr["PacksAReponer"] is DBNull or null ? 0 : Convert.ToInt32(rdr["PacksAReponer"])
                    // UltRemitoFecha queda default → el PDF lo muestra en blanco para reemplazos reimpresos
                });
            }
        }
        catch
        {
            // Los reemplazos son la sección extra: si falla, devolvemos solo reposición (igual que el desktop).
        }

        var repo = filas.Where(f => !f.EsHuerfano).ToList();
        return new ReposicionResultadoDto
        {
            Filas = filas,
            TotalArticulos = repo.Count,
            TotalPacks = repo.Sum(f => f.Packs),
            TotalPrendas = repo.Sum(f => f.Packs * f.CantPack)
        };
    }
}
