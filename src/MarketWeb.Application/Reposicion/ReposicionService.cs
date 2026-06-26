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
        // El SP lee los locales EN VIVO por OPENQUERY; si un local responde lento, tarda más.
        // La corrida automática (scheduler) es de fondo → más timeout; la interactiva falla rápido (5 min).
        int cmdTimeout = machineName == "MARKETWEB-SCHED" ? 1200 : 300;
        await using (var cmd = new SqlCommand("SP_RepoCalcularPacks", cn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = cmdTimeout
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
            cmd2.CommandTimeout = cmdTimeout;
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

    public async Task<ExplicarDto> ExplicarAsync(string local, string artCod, bool historiaCompleta = false, CancellationToken ct = default)
    {
        local = (local ?? "").Trim();
        artCod = (artCod ?? "").Trim();
        var dto = new ExplicarDto { Local = local, ArtCod = artCod };

        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        // Descripción (el SP no la trae).
        await using (var cmdArt = new SqlCommand(
            "SELECT TOP 1 ISNULL(RTRIM(ARTDES), 'Sin descripción') FROM DRAGONFISH_CENTRAL.Zoologic.ART WITH(NOLOCK) WHERE RTRIM(ARTCOD) = @cod", cn))
        {
            cmdArt.Parameters.AddWithValue("@cod", artCod);
            var o = await cmdArt.ExecuteScalarAsync(ct);
            dto.ArtDes = o is null or DBNull ? "SIN DESCRIPCIÓN" : o.ToString()!;
        }

        await using var cmd = new SqlCommand("MARKET.dbo.SP_RepoExplicarArticulo", cn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };
        cmd.Parameters.Add("@Local", SqlDbType.NVarChar, 20).Value = local;
        cmd.Parameters.Add("@ARTCOD", SqlDbType.VarChar, 20).Value = artCod;
        // Solo lo mandamos cuando se pide: con el check apagado, el SP corre como siempre (no requiere
        // la versión nueva del SP). Con historiaCompleta=1 el RS3 baja el piso a 1900 (ledger completo).
        if (historiaCompleta)
            cmd.Parameters.Add("@HistoriaCompleta", SqlDbType.Bit).Value = true;

        await using var rdr = await cmd.ExecuteReaderAsync(ct);

        // --- RS1: resumen / veredicto (lectura defensiva por nombre) ---
        if (await rdr.ReadAsync(ct))
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rdr.FieldCount; i++) cols.Add(rdr.GetName(i));
            int GetI(string n) => cols.Contains(n) && rdr[n] is not (DBNull or null) ? Convert.ToInt32(rdr[n]) : 0;
            string GetS(string n) => cols.Contains(n) && rdr[n] is not (DBNull or null) ? rdr[n].ToString()!.Trim() : "";
            DateTime? GetD(string n) => cols.Contains(n) && rdr[n] is not (DBNull or null) ? Convert.ToDateTime(rdr[n]) : null;

            dto.Resumen = new ExplicarResumenDto
            {
                HayDatos = true,
                Explicacion = GetS("Explicacion"),
                Clasificacion = GetS("Clasificacion"),
                CantPack = cols.Contains("CantPack") && rdr["CantPack"] is not (DBNull or null) ? Convert.ToInt32(rdr["CantPack"]) : 1,
                Venta = GetI("Venta"),
                FallasRotacion = GetI("FallasRotacion"),
                Ajuste = GetI("Ajuste"),
                ReposEnviadas = GetI("ReposEnviadas"),
                Pendiente = GetI("Pendiente"),
                Packs = GetI("Packs"),
                Ancla = GetD("Ancla"),
                UltRemitoCant = GetI("UltRemitoCant"),
                UltRemitoFecha = GetD("UltRemitoFecha"),
                EventosPendientes = GetI("EventosPendientes"),
                EventosSobrantePacks = GetI("EventosSobrantePacks"),
                EventosFaltantePacks = GetI("EventosFaltantePacks"),
                SobranteAplicadoPacks = GetI("SobranteAplicadoPacks"),
                SobranteAplicadoUnidades = GetI("SobranteAplicadoUnidades")
            };
        }

        // --- RS2: ubicaciones ---
        if (await rdr.NextResultAsync(ct))
        {
            while (await rdr.ReadAsync(ct))
            {
                var ubi = Str(rdr, "Ubicacion");
                dto.Ubicaciones.Add(new ExplicarUbicacionDto
                {
                    Ubicacion = ubi,
                    Modulo = Str(rdr, "Modulo"),
                    Mobiliario = Str(rdr, "Mobiliario"),
                    Pasillo = rdr["OrdenPasillo"] is DBNull or null ? "" : rdr["OrdenPasillo"].ToString()!,
                    Fila = rdr["Fila"] is DBNull or null ? "" : rdr["Fila"].ToString()!,
                    Posicion = rdr["Posicion"] is DBNull or null ? "" : rdr["Posicion"].ToString()!,
                    FechaHora = rdr["FechaHora"] is DBNull or null ? null : Convert.ToDateTime(rdr["FechaHora"]),
                    EsDeposito = ubi.ToUpperInvariant() == "DEPOSITO"
                });
            }
        }

        // --- RS3: movimientos con saldo corrido ---
        var pendiente = dto.Resumen.Pendiente;
        if (await rdr.NextResultAsync(ct))
        {
            var saldo = 0;
            while (await rdr.ReadAsync(ct))
            {
                var orden = Convert.ToInt32(rdr["Orden"]);
                var delta = rdr["SaldoDelta"] is DBNull or null ? 0 : (int)Math.Truncate(Convert.ToDecimal(rdr["SaldoDelta"]));
                saldo += delta;
                var remito = Str(rdr, "Remito");
                var mov = new ExplicarMovimientoDto
                {
                    Fecha = rdr["Fecha"] is DBNull or null ? null : Convert.ToDateTime(rdr["Fecha"]),
                    Hora = Str(rdr, "Hora"),
                    Remito = remito,
                    Motivo = Str(rdr, "Motivo"),
                    Cantidad = rdr["Cantidad"] is DBNull or null ? null : Convert.ToDecimal(rdr["Cantidad"]),
                    Orden = orden,
                    SaldoDelta = delta,
                    Saldo = orden < 8 ? saldo : null,
                    Origen = Str(rdr, "Origen"),
                    Tipo = Str(rdr, "Tipo")
                };
                if (orden == 6)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(remito, @"EVT\s*#(\d+)");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out var evtId)) mov.EventoId = evtId;
                }
                dto.Movimientos.Add(mov);
            }
            dto.SaldoCuadra = !dto.Resumen.HayDatos || saldo == pendiente;
        }

        // --- RS4: eventos de piso pendientes (ID → TieneFoto) ---
        if (await rdr.NextResultAsync(ct))
        {
            var fotos = new Dictionary<int, bool>();
            while (await rdr.ReadAsync(ct))
            {
                var id = rdr["ID"] is DBNull or null ? 0 : Convert.ToInt32(rdr["ID"]);
                var tf = rdr["TieneFoto"] is not (DBNull or null) && Convert.ToInt32(rdr["TieneFoto"]) == 1;
                if (id > 0) fotos[id] = tf;
            }
            foreach (var mv in dto.Movimientos)
                if (mv.EventoId > 0 && fotos.TryGetValue(mv.EventoId, out var tf)) mv.EventoTieneFoto = tf;
        }

        // ¿Está en un palet activo? Solo si NO tiene ubicación de depósito (si la tiene, ya está en
        // depósito → evitamos el chequeo costoso contra Dragonfish, que es lo único que aporta el palet).
        if (!dto.Ubicaciones.Any(u => u.EsDeposito))
            dto.EnPalet = await EnPaletAsync(artCod, ct);

        return dto;
    }

    public async Task<ResetResultadoDto> ResetearDesdeRemitoAsync(ResetRemitoRequest req, string usuario, CancellationToken ct = default)
    {
        var local = (req.Local ?? "").Trim();
        var artCod = (req.ArtCod ?? "").Trim();
        if (local == "" || artCod == "") return new ResetResultadoDto { Ok = false, Mensaje = "Faltan datos (local / artículo)." };

        // Fecha+hora del remito = ancla. La hora es clave (v53): sin ella se pierde la venta del mismo día.
        var fechaCompleta = req.Fecha.Date;
        if (!string.IsNullOrWhiteSpace(req.Hora) && TimeSpan.TryParse(req.Hora, out var ts)) fechaCompleta = fechaCompleta.Add(ts);

        // Reset manual desde remito: packs siempre POSITIVO (demanda detectada al ver el historial).
        var cantPack = req.CantPack > 0 ? req.CantPack : 1;
        var packs = Math.Max(1, (int)Math.Round(Math.Abs(req.Cantidad) / (double)cantPack));

        return await AplicarResetAsync(local, artCod, fechaCompleta, packs, req.Comentario, usuario, ct);
    }

    public async Task<ResetResultadoDto> ResetearDesdeEventoAsync(int idEvento, string comentario, string usuario, CancellationToken ct = default)
    {
        // Datos del evento: artículo, local, tipo (signo) y packs reportados.
        string artCod, local, tipoDif;
        int cantPacks;
        await using (var cn = _db.Create())
        {
            await cn.OpenAsync(ct);
            await using var cmd = new SqlCommand(
                "SELECT RTRIM(ISNULL(ARTCOD,'')) AS ARTCOD, RTRIM(Local) AS Local, RTRIM(ISNULL(TipoDiferencia,'')) AS TipoDiferencia, " +
                "ISNULL(CantidadPacks,0) AS CantidadPacks FROM MARKET.dbo.EventosReposicion WHERE ID = @id AND Eliminado = 0", cn);
            cmd.Parameters.Add("@id", SqlDbType.Int).Value = idEvento;
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            if (!await rdr.ReadAsync(ct)) return new ResetResultadoDto { Ok = false, Mensaje = "No se encontró el evento." };
            artCod = rdr["ARTCOD"].ToString()!.Trim();
            local = rdr["Local"].ToString()!.Trim();
            tipoDif = rdr["TipoDiferencia"].ToString()!.Trim();
            cantPacks = Convert.ToInt32(rdr["CantidadPacks"]);
        }

        if (string.IsNullOrEmpty(artCod))
            return new ResetResultadoDto { Ok = false, Mensaje = "El evento no tiene un artículo único; no se puede resetear desde acá." };
        if (!string.Equals(local, "LURO", StringComparison.OrdinalIgnoreCase) && !string.Equals(local, "PERALTA", StringComparison.OrdinalIgnoreCase))
            return new ResetResultadoDto { Ok = false, Mensaje = "El evento no es de un local con reposición (LURO/PERALTA)." };
        if (cantPacks <= 0)
            return new ResetResultadoDto { Ok = false, Mensaje = "El evento no tiene cantidad de packs para aplicar el reset." };

        // El reset por evento se ancla al ÚLTIMO REMITO (lo trae el explain del SP, igual que el desktop).
        var exp = await ExplicarAsync(local, artCod, false, ct);
        if (exp.Resumen.UltRemitoFecha is null)
            return new ResetResultadoDto { Ok = false, Mensaje = "No hay último remito para este artículo/local; no se puede anclar el reset." };

        // Packs CON SIGNO: FALTANTE positivo (demanda/cobertura), SOBRANTE negativo (sobre-envío, v51).
        var packs = string.Equals(tipoDif, "SOBRANTE", StringComparison.OrdinalIgnoreCase) ? -cantPacks : cantPacks;

        return await AplicarResetAsync(local, artCod, exp.Resumen.UltRemitoFecha.Value, packs, comentario, usuario, ct);
    }

    // Reset centralizado (porteo de AplicarResetVenta): idempotencia + transacción que re-ancla RepoResto
    // (FechaAncla + AnclaHora + Pendiente/Resto=0) y registra en RepoReposicionArticulosReseteados.
    private async Task<ResetResultadoDto> AplicarResetAsync(string local, string artCod, DateTime fechaCompleta,
        int packs, string? comentExtra, string usuario, CancellationToken ct)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        // Idempotencia: si ya hay un reset vigente con fecha >= la del ancla, no insertamos.
        await using (var cmdChk = new SqlCommand(
            "SELECT TOP 1 Fecha, PacksDetectados, Comentario FROM MARKET.dbo.RepoReposicionArticulosReseteados " +
            "WHERE Local = @l AND RTRIM(ARTCOD) = @a AND Eliminado = 0 AND CAST(Fecha AS DATE) >= CAST(@f AS DATE) ORDER BY Fecha DESC", cn))
        {
            cmdChk.Parameters.Add("@l", SqlDbType.VarChar, 20).Value = local;
            cmdChk.Parameters.Add("@a", SqlDbType.VarChar, 20).Value = artCod;
            cmdChk.Parameters.Add("@f", SqlDbType.DateTime).Value = fechaCompleta;
            await using var rdr = await cmdChk.ExecuteReaderAsync(ct);
            if (await rdr.ReadAsync(ct))
            {
                var fchYa = Convert.ToDateTime(rdr["Fecha"]);
                var packsYa = rdr["PacksDetectados"] is DBNull or null ? 0 : Convert.ToInt32(rdr["PacksDetectados"]);
                var coment = rdr["Comentario"] is DBNull or null ? "" : rdr["Comentario"].ToString()!;
                var msg = $"Ya hay un reset vigente para este artículo/local desde {fchYa:dd/MM/yyyy HH:mm} (Packs: {packsYa:N0}). No se inserta de nuevo.";
                if (coment != "") msg += "\n\nComentario: " + coment;
                return new ResetResultadoDto { Ok = false, Mensaje = msg };
            }
        }

        var comentFinal = $"RESETEO VENTA · {usuario} · {DateTime.Now:dd/MM/yyyy HH:mm}";
        if (!string.IsNullOrWhiteSpace(comentExtra)) comentFinal += " — " + comentExtra.Trim();
        if (comentFinal.Length > 500) comentFinal = comentFinal[..500];

        var fechaAncla = fechaCompleta.Date;
        var anclaHora = fechaCompleta.ToString("HH:mm:ss");
        int filasAncladas;

        await using (var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct))
        {
            try
            {
                // (A) Re-anclar el estado: lo que REALMENTE resetea.
                await using (var cmdUpd = new SqlCommand(
                    "UPDATE MARKET.dbo.RepoResto SET FechaAncla = @FechaAncla, AnclaHora = @AnclaHora, " +
                    "Pendiente = 0, Resto = 0, FechaActualizacion = GETDATE() " +
                    "WHERE Local = @l AND RTRIM(ARTCOD) = @a AND Eliminado = 0", cn, tx))
                {
                    cmdUpd.Parameters.Add("@FechaAncla", SqlDbType.Date).Value = fechaAncla;
                    cmdUpd.Parameters.Add("@AnclaHora", SqlDbType.VarChar, 8).Value = anclaHora;
                    cmdUpd.Parameters.Add("@l", SqlDbType.VarChar, 20).Value = local;
                    cmdUpd.Parameters.Add("@a", SqlDbType.VarChar, 20).Value = artCod;
                    filasAncladas = await cmdUpd.ExecuteNonQueryAsync(ct);
                }

                // (B) Log de auditoría (Mobiliario NULL: el SP solo filtra por Local+ARTCOD).
                await using (var cmdIns = new SqlCommand(
                    "INSERT INTO MARKET.dbo.RepoReposicionArticulosReseteados (Fecha, Local, Mobiliario, ARTCOD, PacksDetectados, Comentario) " +
                    "VALUES (@f, @l, NULL, @a, @p, @c)", cn, tx))
                {
                    cmdIns.Parameters.Add("@f", SqlDbType.DateTime).Value = fechaCompleta;
                    cmdIns.Parameters.Add("@l", SqlDbType.VarChar, 20).Value = local;
                    cmdIns.Parameters.Add("@a", SqlDbType.VarChar, 20).Value = artCod;
                    cmdIns.Parameters.Add("@p", SqlDbType.Int).Value = packs;
                    cmdIns.Parameters.Add("@c", SqlDbType.NVarChar, 500).Value = comentFinal;
                    await cmdIns.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        var ok = $"Reset aplicado para {artCod} en {local}. Ancla: {fechaAncla:dd/MM/yyyy} {anclaHora} (Packs: {packs:N0}).";
        if (filasAncladas == 0)
            ok += "\n\nNota: no había estado previo en RepoResto para este artículo/local; se registró el reset y la próxima corrida sembrará la fila con esta ancla.";
        return new ResetResultadoDto { Ok = true, Mensaje = ok };
    }

    // True si el artículo está en algún palet activo (no desarmado). Los palets guardan remitos
    // (PaletsDetalle.NroRemito + Origen); el artículo se resuelve matcheando contra el Dragonfish del origen,
    // igual que ListarArticulosAsync de Palets. Defensivo: ante error devuelve false (no rompe el explain).
    private async Task<bool> EnPaletAsync(string artCod, CancellationToken ct)
    {
        // Set-based: arranca del conjunto chico de remitos en palets ACTIVOS y los matchea de una contra
        // el Dragonfish del origen, filtrando por el artículo. Un paso por origen (no un escaneo por remito).
        const string sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM MARKET.dbo.PaletsDetalle DETP WITH (NOLOCK)
                INNER JOIN MARKET.dbo.Palets P WITH (NOLOCK) ON P.id = DETP.idPalet AND P.FechaDesarme IS NULL
                INNER JOIN DRAGONFISH_CENTRAL.ZooLogic.COMPROBANTEV C WITH (NOLOCK)
                    ON DETP.Origen = 'CENTRAL' AND C.FLETRA = 'R' AND C.ANULADO = 0
                   AND C.DESCFW = (CASE WHEN DETP.Auditoria LIKE '%PALET APP%' THEN 'REMITO ' + LTRIM(RTRIM(DETP.NroRemito)) ELSE LTRIM(RTRIM(DETP.NroRemito)) END)
                INNER JOIN DRAGONFISH_CENTRAL.ZooLogic.COMPROBANTEVDET DET WITH (NOLOCK) ON DET.CODIGO = C.CODIGO AND RTRIM(DET.FART) = @cod
                WHERE DETP.Eliminado = 0)
            OR EXISTS (
                SELECT 1
                FROM MARKET.dbo.PaletsDetalle DETP WITH (NOLOCK)
                INNER JOIN MARKET.dbo.Palets P WITH (NOLOCK) ON P.id = DETP.idPalet AND P.FechaDesarme IS NULL
                INNER JOIN DRAGONFISH_LURO.ZooLogic.COMPROBANTEV C WITH (NOLOCK)
                    ON DETP.Origen = 'LURO' AND C.FLETRA = 'R' AND C.ANULADO = 0
                   AND C.DESCFW = (CASE WHEN DETP.Auditoria LIKE '%PALET APP%' THEN 'REMITO ' + LTRIM(RTRIM(DETP.NroRemito)) ELSE LTRIM(RTRIM(DETP.NroRemito)) END)
                INNER JOIN DRAGONFISH_LURO.ZooLogic.COMPROBANTEVDET DET WITH (NOLOCK) ON DET.CODIGO = C.CODIGO AND RTRIM(DET.FART) = @cod
                WHERE DETP.Eliminado = 0)
            OR EXISTS (
                SELECT 1
                FROM MARKET.dbo.PaletsDetalle DETP WITH (NOLOCK)
                INNER JOIN MARKET.dbo.Palets P WITH (NOLOCK) ON P.id = DETP.idPalet AND P.FechaDesarme IS NULL
                INNER JOIN DRAGONFISH_PERALTA.ZooLogic.COMPROBANTEV C WITH (NOLOCK)
                    ON DETP.Origen = 'PERALTA' AND C.FLETRA = 'R' AND C.ANULADO = 0
                   AND C.DESCFW = (CASE WHEN DETP.Auditoria LIKE '%PALET APP%' THEN 'REMITO ' + LTRIM(RTRIM(DETP.NroRemito)) ELSE LTRIM(RTRIM(DETP.NroRemito)) END)
                INNER JOIN DRAGONFISH_PERALTA.ZooLogic.COMPROBANTEVDET DET WITH (NOLOCK) ON DET.CODIGO = C.CODIGO AND RTRIM(DET.FART) = @cod
                WHERE DETP.Eliminado = 0)
            THEN 1 ELSE 0 END";
        try
        {
            await using var cn = _db.Create();
            await cn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.Add("@cod", SqlDbType.VarChar, 20).Value = artCod;
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
        }
        catch
        {
            return false;   // ante timeout/error no bloqueamos el explain
        }
    }
}
