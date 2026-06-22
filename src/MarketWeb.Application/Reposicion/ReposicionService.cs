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
}
