using System.Data;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Reposicion;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Reposicion;

/// <summary>
/// Porteo de frmRepoArticulos (modo LOGISTICA): reporte de artículos por sp_ConsultaArticulos y
/// edición masiva de packs en ArticulosDatosAdiciones (CantPack / ReemplazoPacks). Combos desde Dragonfish CENTRAL.
/// </summary>
public sealed class ReporteArticulosService : IReporteArticulosService
{
    private readonly ISqlConnectionFactory _db;
    private const string DF = "DRAGONFISH_CENTRAL.ZooLogic";

    public ReporteArticulosService(ISqlConnectionFactory db) => _db = db;

    public async Task<ReporteArticulosCombosDto> CombosAsync(CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var dto = new ReporteArticulosCombosDto
        {
            Tipos = await ListaAsync(cn, $"SELECT DISTINCT TIPO.DESCRIP AS V FROM {DF}.ART ART LEFT JOIN {DF}.TIPOART TIPO ON TIPO.COD=ART.TIPOARTI WHERE TIPO.DESCRIP<>'' ORDER BY TIPO.DESCRIP", ct),
            Combos = await ListaAsync(cn, $"SELECT DISTINCT ART.CLASIFART AS V FROM {DF}.ART ART WHERE ART.CLASIFART<>'' ORDER BY ART.CLASIFART", ct),
            Familias = await ListaAsync(cn, $"SELECT DISTINCT FAM.DESCRIP AS V FROM {DF}.ART ART LEFT JOIN {DF}.FAMILIA FAM ON FAM.COD=ART.FAMILIA WHERE FAM.DESCRIP<>'' ORDER BY FAM.DESCRIP", ct),
            Anios = await ListaAsync(cn, $"SELECT DISTINCT ANO AS V FROM {DF}.ART ART WHERE ART.ANO<>0 ORDER BY ANO DESC", ct),
            Temporadas = await ListaAsync(cn, $"SELECT DISTINCT TEM.TDES AS V FROM {DF}.ART ART LEFT JOIN {DF}.TEMPORADA TEM ON TEM.TCOD=ART.ATEMPORADA WHERE TEM.TDES<>'' ORDER BY TEM.TDES", ct),
            Categorias = await ListaAsync(cn, $"SELECT DISTINCT CATE.DESCRIP AS V FROM {DF}.ART ART LEFT JOIN {DF}.CATEGART CATE ON CATE.COD=ART.CATEARTI WHERE CATE.DESCRIP<>'' ORDER BY CATE.DESCRIP", ct),
            Subfamilias = await ListaAsync(cn, $"SELECT DISTINCT GRU.DESCRIP AS V FROM {DF}.ART ART LEFT JOIN {DF}.GRUPO GRU ON GRU.COD=ART.GRUPO WHERE GRU.DESCRIP<>'' ORDER BY GRU.DESCRIP", ct),
        };

        await using (var cmd = new SqlCommand(
            $"SELECT DISTINCT PRO.CLCOD AS Cod, PRO.CLNOM AS Nombre FROM {DF}.ART ART LEFT JOIN {DF}.PROV PRO ON PRO.CLCOD=ART.ARTFAB WHERE PRO.CLNOM<>'' ORDER BY PRO.CLNOM", cn) { CommandTimeout = 60 })
        {
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
                dto.Proveedores.Add(new ProveedorComboDto
                {
                    Cod = (rdr["Cod"]?.ToString() ?? "").Trim(),
                    Nombre = (rdr["Nombre"]?.ToString() ?? "").Trim()
                });
        }
        return dto;
    }

    private static async Task<List<string>> ListaAsync(SqlConnection cn, string sql, CancellationToken ct)
    {
        var lista = new List<string>();
        await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            var v = rdr["V"]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(v)) lista.Add(v);
        }
        return lista;
    }

    public async Task<IReadOnlyList<ArticuloReporteDto>> ListarAsync(ReporteArticulosFiltro f, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand("sp_ConsultaArticulos", cn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };

        static object Todos(string? s) => string.IsNullOrWhiteSpace(s) || s == "TODOS" ? DBNull.Value : s.Trim();
        cmd.Parameters.AddWithValue("@CodArt", string.IsNullOrWhiteSpace(f.CodArt) ? DBNull.Value : f.CodArt.Trim());
        cmd.Parameters.AddWithValue("@Descripcion", string.IsNullOrWhiteSpace(f.Descripcion) ? DBNull.Value : f.Descripcion.Trim());
        cmd.Parameters.AddWithValue("@SoloEnLocales", f.SoloEnLocales);
        cmd.Parameters.AddWithValue("@LocalesSinFotoGoogle", false);
        cmd.Parameters.AddWithValue("@Combo", Todos(f.Combo));
        cmd.Parameters.AddWithValue("@Tipo", Todos(f.Tipo));
        cmd.Parameters.AddWithValue("@Temporada", Todos(f.Temporada));
        cmd.Parameters.AddWithValue("@Familia", Todos(f.Familia));
        cmd.Parameters.AddWithValue("@Año", f.Anio);
        cmd.Parameters.AddWithValue("@Categoria", Todos(f.Categoria));
        cmd.Parameters.AddWithValue("@Stock", f.Stock);
        cmd.Parameters.AddWithValue("@CodProveedor", string.IsNullOrWhiteSpace(f.ProveedorCod) ? "" : f.ProveedorCod.Trim());
        cmd.Parameters.AddWithValue("@FiltraFechaAlta", f.FiltraFechaAlta);
        cmd.Parameters.AddWithValue("@FiltraFechaDesde", f.FechaDesde);
        cmd.Parameters.AddWithValue("@FiltraFechaHasta", f.FechaHasta);
        cmd.Parameters.AddWithValue("@StockNegativo", 0);
        cmd.Parameters.AddWithValue("@MasDeUnaUbicacion", 0);
        cmd.Parameters.AddWithValue("@SoloCentral", 0);

        var lista = new List<ArticuloReporteDto>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rdr.FieldCount; i++) cols.Add(rdr.GetName(i));

        string S(string n) => cols.Contains(n) && rdr[n] is not (DBNull or null) ? rdr[n].ToString()!.Trim() : "";
        int? I(string n) => cols.Contains(n) && rdr[n] is not (DBNull or null) ? Convert.ToInt32(rdr[n]) : null;
        DateTime? D(string n) => cols.Contains(n) && rdr[n] is not (DBNull or null) ? Convert.ToDateTime(rdr[n]) : null;

        while (await rdr.ReadAsync(ct))
        {
            lista.Add(new ArticuloReporteDto
            {
                Orden = I("Orden") ?? 0,
                Codigo = S("Código"),
                Descripcion = S("Descripción"),
                Proveedor = S("Proveedor"),
                Combo = S("Combo"),
                Familia = S("Familia"),
                Tipo = S("Tipo"),
                Temporada = S("Temporada"),
                Anio = S("Año"),
                Categoria = S("Categoría"),
                CantPack = I("CantPack"),
                PacksReemplazo = I("PacksReemplazo"),
                FechaPack = D("FechaPack")
            });
        }
        rdr.Close();

        // Subfamilia (Grupo) no la trae el SP: la traemos de Dragon para los códigos del resultado,
        // así podemos mostrarla y filtrar por ella sin tocar sp_ConsultaArticulos.
        var codigos = lista.Select(x => x.Codigo).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (codigos.Count > 0)
        {
            var grupos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // IN (...) con parámetros por lote (por si son muchos códigos).
            for (int off = 0; off < codigos.Count; off += 1000)
            {
                var lote = codigos.Skip(off).Take(1000).ToList();
                var ps = lote.Select((_, i) => "@c" + i).ToList();
                await using var cmd2 = new SqlCommand(
                    $"SELECT RTRIM(ART.ARTCOD) AS Cod, ISNULL(GRU.DESCRIP,'') AS Grupo FROM {DF}.ART ART " +
                    $"LEFT JOIN {DF}.GRUPO GRU ON GRU.COD=ART.GRUPO WHERE RTRIM(ART.ARTCOD) IN ({string.Join(",", ps)})", cn)
                { CommandTimeout = 60 };
                for (int i = 0; i < lote.Count; i++) cmd2.Parameters.AddWithValue(ps[i], lote[i]);
                await using var r2 = await cmd2.ExecuteReaderAsync(ct);
                while (await r2.ReadAsync(ct))
                    grupos[(r2["Cod"]?.ToString() ?? "").Trim()] = (r2["Grupo"]?.ToString() ?? "").Trim();
            }
            foreach (var it in lista)
                if (grupos.TryGetValue(it.Codigo.Trim(), out var g)) it.Subfamilia = g;
        }

        // Filtro por Subfamilia (si se eligió una en el combo).
        if (!string.IsNullOrWhiteSpace(f.Subfamilia) && f.Subfamilia != "TODOS")
            lista = lista.Where(x => string.Equals(x.Subfamilia, f.Subfamilia, StringComparison.OrdinalIgnoreCase)).ToList();

        return lista;
    }

    public async Task<int> GuardarPacksAsync(GuardarPacksRequest req, string usuario, CancellationToken ct = default)
    {
        if (!req.ModificarCantPack && !req.ModificarPacksReemplazo) return 0;
        if (req.ArtCods is null || req.ArtCods.Count == 0) return 0;

        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        var afectados = 0;
        foreach (var raw in req.ArtCods)
        {
            var art = (raw ?? "").Trim();
            if (art == "") continue;

            // ¿Existe registro vigente?
            bool existe;
            await using (var chk = new SqlCommand(
                "SELECT COUNT(1) FROM MARKET.dbo.ArticulosDatosAdiciones WHERE ARTCOD = @a AND Eliminado = 0", cn))
            {
                chk.Parameters.Add("@a", SqlDbType.VarChar, 50).Value = art;
                existe = Convert.ToInt32(await chk.ExecuteScalarAsync(ct)) > 0;
            }

            // Defaults como el desktop: si no se modifica un campo, en el alta va 60 / 1.
            var cant = req.ModificarCantPack ? req.CantPack : 60;
            var reemp = req.ModificarPacksReemplazo ? req.PacksReemplazo : 1;

            if (!existe)
            {
                var aud = $"Registro Agregado LOGISTICA | {DateTime.Now} por {usuario}";
                await using var ins = new SqlCommand(
                    "INSERT INTO MARKET.dbo.ArticulosDatosAdiciones (ARTCOD, CantPack, Eliminado, Auditoria, Fecha, ReemplazoPacks) " +
                    "VALUES (@a, @cant, 0, @aud, GETDATE(), @reemp)", cn);
                ins.Parameters.Add("@a", SqlDbType.VarChar, 50).Value = art;
                ins.Parameters.Add("@cant", SqlDbType.Int).Value = cant;
                ins.Parameters.Add("@reemp", SqlDbType.Int).Value = reemp;
                ins.Parameters.Add("@aud", SqlDbType.NVarChar, 500).Value = aud;
                afectados += await ins.ExecuteNonQueryAsync(ct);
            }
            else
            {
                var aud = $"Registro Modificado LOGISTICA | {DateTime.Now} por {usuario}";
                var sets = new List<string>();
                if (req.ModificarCantPack) sets.Add("CantPack = @cant");
                if (req.ModificarPacksReemplazo) sets.Add("ReemplazoPacks = @reemp");
                sets.Add("Auditoria = @aud");
                sets.Add("Fecha = GETDATE()");
                await using var upd = new SqlCommand(
                    $"UPDATE MARKET.dbo.ArticulosDatosAdiciones SET {string.Join(", ", sets)} WHERE Eliminado = 0 AND ARTCOD = @a", cn);
                upd.Parameters.Add("@a", SqlDbType.VarChar, 50).Value = art;
                if (req.ModificarCantPack) upd.Parameters.Add("@cant", SqlDbType.Int).Value = cant;
                if (req.ModificarPacksReemplazo) upd.Parameters.Add("@reemp", SqlDbType.Int).Value = reemp;
                upd.Parameters.Add("@aud", SqlDbType.NVarChar, 500).Value = aud;
                afectados += await upd.ExecuteNonQueryAsync(ct);
            }
        }
        return afectados;
    }
}
