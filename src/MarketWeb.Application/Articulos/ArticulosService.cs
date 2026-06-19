using System.Text.RegularExpressions;
using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Articulos;
using MarketWeb.Shared.Insumos;

namespace MarketWeb.Application.Articulos;

/// <summary>
/// Consulta de artículos (frmConsultaArticulos). Resuelve descripción/precio por
/// EQUI (código de barras) o ART, stock (COMB), cant. pack, fotos y mapeo.
/// Fuente por ubicación: LURO/CENTRAL = réplica DRAGONFISH_&lt;x&gt;; PERALTA lee
/// ART/EQUI EN VIVO por OPENQUERY (su réplica de ART está vacía).
/// </summary>
public sealed class ArticulosService : IArticulosService
{
    private readonly ISqlConnectionFactory _db;

    public ArticulosService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<UbicacionDto>> ListarUbicacionesAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT ID AS Id, Descripcion FROM Ubicaciones WHERE Eliminado = 0 ORDER BY Descripcion;";
        using var cn = _db.Create();
        return (await cn.QueryAsync<UbicacionDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    private sealed record ArtRow(string ArtCod, string Descripcion, string Precio);

    private static string DbFor(string ubic) => ubic?.ToUpperInvariant() switch
    {
        "LURO" => "DRAGONFISH_LURO",
        "PERALTA" => "DRAGONFISH_PERALTA",
        _ => "DRAGONFISH_CENTRAL"
    };

    // Código de artículo / barra: letras, dígitos, punto, guión. Sin comillas (anti-inyección en OPENQUERY).
    private static string Sanitizar(string codigo)
        => Regex.Replace((codigo ?? "").Split('!')[0].Trim().ToUpperInvariant(), @"[^A-Z0-9.\-]", "");

    public async Task<ConsultaArticuloDto?> ConsultarAsync(string codigo, string ubicacion, CancellationToken ct = default)
    {
        var cod = Sanitizar(codigo);
        if (cod.Length == 0) return null;
        var ubic = string.IsNullOrWhiteSpace(ubicacion) ? "CENTRAL" : ubicacion.Trim();

        using var cn = _db.Create();

        // 1) EQUI (código de barras) → ART. PERALTA en vivo (OPENQUERY), resto réplica/central.
        ArtRow? art;
        if (ubic.Equals("PERALTA", StringComparison.OrdinalIgnoreCase))
        {
            var inner = "SELECT TOP 1 RTRIM(ART.ARTCOD) AS ArtCod, RTRIM(ART.ARTDES) AS Descripcion, CAST(ART.CLASIFART AS VARCHAR(50)) AS Precio "
                      + "FROM DRAGONFISH_PERALTA.ZooLogic.ART ART INNER JOIN DRAGONFISH_PERALTA.ZooLogic.EQUI EQUI ON ART.ARTCOD=EQUI.CARTICUL "
                      + $"WHERE EQUI.CCODIGO='{cod}'";
            var sql = $"SELECT * FROM OPENQUERY([marketperalta.ddns.net], '{inner.Replace("'", "''")}')";
            art = await cn.QuerySingleOrDefaultAsync<ArtRow>(new CommandDefinition(sql, cancellationToken: ct));
        }
        else
        {
            var db = DbFor(ubic);
            var sql = $"SELECT TOP 1 RTRIM(ART.ARTCOD) AS ArtCod, RTRIM(ART.ARTDES) AS Descripcion, CAST(ART.CLASIFART AS VARCHAR(50)) AS Precio "
                    + $"FROM {db}.ZooLogic.ART ART INNER JOIN {db}.ZooLogic.EQUI EQUI ON ART.ARTCOD=EQUI.CARTICUL WHERE EQUI.CCODIGO=@cod";
            art = await cn.QuerySingleOrDefaultAsync<ArtRow>(new CommandDefinition(sql, new { cod }, cancellationToken: ct));
        }

        // 2) Si EQUI no encontró, buscar directo por ARTCOD en CENTRAL (maestro).
        if (art is null)
        {
            const string sql = "SELECT TOP 1 RTRIM(ARTCOD) AS ArtCod, RTRIM(ARTDES) AS Descripcion, CAST(CLASIFART AS VARCHAR(50)) AS Precio "
                             + "FROM DRAGONFISH_CENTRAL.ZooLogic.ART WHERE ARTCOD=@cod";
            art = await cn.QuerySingleOrDefaultAsync<ArtRow>(new CommandDefinition(sql, new { cod }, cancellationToken: ct));
        }

        if (art is null) return null;

        var dto = new ConsultaArticuloDto { ArtCod = art.ArtCod, Descripcion = art.Descripcion, Precio = art.Precio };

        // 3) Stock local + tránsito (COMB de la ubicación) — EN VIVO para LURO/PERALTA con fallback a réplica.
        var stock = await StockLocalAsync(cn, ubic, Sanitizar(dto.ArtCod), ct);
        dto.StockLocal = stock.StockLocal;
        dto.EnTransito = stock.EnTransito;

        // 4) Stock depósito central (COCANT + ENTRANSITO)
        const string sqlDep = "WITH S AS (SELECT *, ROW_NUMBER() OVER (PARTITION BY COART,COCOL,TALLE ORDER BY FALTAFW DESC, HALTAFW DESC) AS Fila "
                            + "FROM DRAGONFISH_CENTRAL.Zoologic.COMB WITH(NOLOCK) WHERE COART=@art) "
                            + "SELECT ISNULL(SUM(COCANT+ENTRANSITO),0) FROM S WHERE Fila=1";
        dto.StockDeposito = await cn.ExecuteScalarAsync<decimal>(new CommandDefinition(sqlDep, new { art = dto.ArtCod }, cancellationToken: ct));

        // 5) Cant. pack (default 60)
        const string sqlPack = "SELECT TOP 1 ISNULL(CantPack,0) FROM ArticulosDatosAdiciones WHERE ARTCOD=@art AND Eliminado=0 ORDER BY ID DESC";
        var pack = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(sqlPack, new { art = dto.ArtCod }, cancellationToken: ct));
        dto.CantPack = pack ?? 60;

        // 6) Mapeo: ubicaciones del local elegido (todas) + DEPÓSITO (solo TOP 1).
        const string sqlLocal = "SELECT U.Descripcion AS Ubicacion, Pasillo AS Sector, Fila AS Fila, Posicion AS Posicion "
                              + "FROM MapeoRegistro REG "
                              + "INNER JOIN Mapeo MAP ON REG.IDMapeo=MAP.ID "
                              + "INNER JOIN Ubicaciones U ON MAP.IDUbicacion=U.ID "
                              + "INNER JOIN UbicacionesTipo UT ON U.IDTipo=UT.ID "
                              + "WHERE MAP.Eliminado=0 AND REG.Eliminado=0 AND REG.ARTCOD=@art "
                              + "AND U.Descripcion=@ubic AND UT.Descripcion<>'DEPOSITO' "
                              + "ORDER BY REG.ID DESC";
        var locales = (await cn.QueryAsync<UbicacionArtDto>(new CommandDefinition(sqlLocal, new { art = dto.ArtCod, ubic }, cancellationToken: ct))).ToList();

        const string sqlDepMap = "SELECT TOP 1 U.Descripcion AS Ubicacion, Pasillo AS Sector, Fila AS Fila, Posicion AS Posicion "
                            + "FROM MapeoRegistro REG "
                            + "INNER JOIN Mapeo MAP ON REG.IDMapeo=MAP.ID "
                            + "INNER JOIN Ubicaciones U ON MAP.IDUbicacion=U.ID "
                            + "INNER JOIN UbicacionesTipo UT ON U.IDTipo=UT.ID "
                            + "WHERE MAP.Eliminado=0 AND REG.Eliminado=0 AND REG.ARTCOD=@art "
                            + "AND UT.Descripcion='DEPOSITO' "
                            + "ORDER BY REG.ID DESC";
        var deposito = await cn.QuerySingleOrDefaultAsync<UbicacionArtDto>(new CommandDefinition(sqlDepMap, new { art = dto.ArtCod }, cancellationToken: ct));

        if (deposito is not null)
        {
            deposito.EsDeposito = true;
            locales.Add(deposito);
        }
        dto.Ubicaciones = locales;
        dto.EnDeposito = deposito is not null;

        // La búsqueda dentro de palets va por endpoint aparte (BuscarEnPaletsAsync): es lenta
        // (cruza 3 bases Dragon por DESCFW) y no debe demorar la consulta por escáner.
        return dto;
    }

    // Palets activos (no desarmados) que contienen el artículo, con su ubicación en el depósito.
    // INVERTIDA: arranca de los remitos de los palets (pocos) y verifica por EXISTS si contienen
    // el artículo en el Dragonfish del origen (evita escanear COMPROBANTEVDET entero). La ubicación
    // del palet sale del MapeoRegistro que lo apunta (IDPalet). Aun así es lenta (~varios segundos)
    // por el lookup por DESCFW → se llama desde un endpoint aparte, no en la consulta principal.
    private const string SqlPaletsConArticulo = """
        SELECT DISTINCT
               Palet = P.NroPalet,
               Sector = CAST(MAP.Pasillo AS VARCHAR(50)),
               Fila = CAST(MAP.Fila AS VARCHAR(20)),
               Posicion = CAST(MAP.Posicion AS VARCHAR(20))
        FROM MARKET.dbo.PaletsDetalle PD WITH (NOLOCK)
        INNER JOIN MARKET.dbo.Palets P WITH (NOLOCK) ON P.ID = PD.IDPalet AND P.Eliminado = 0 AND P.FechaDesarme IS NULL
        LEFT JOIN MARKET.dbo.MapeoRegistro REGP WITH (NOLOCK) ON REGP.IDPalet = P.ID AND REGP.Eliminado = 0
        LEFT JOIN MARKET.dbo.Mapeo MAP WITH (NOLOCK) ON REGP.IDMapeo = MAP.ID AND MAP.Eliminado = 0
        WHERE PD.Eliminado = 0 AND (
            (PD.Origen = 'CENTRAL' AND EXISTS (SELECT 1 FROM DRAGONFISH_CENTRAL.ZooLogic.COMPROBANTEV C WITH (NOLOCK) INNER JOIN DRAGONFISH_CENTRAL.ZooLogic.COMPROBANTEVDET DET WITH (NOLOCK) ON C.CODIGO = DET.CODIGO WHERE C.FLETRA = 'R' AND C.ANULADO = 0 AND C.DESCFW = (CASE WHEN PD.Auditoria LIKE '%PALET APP%' THEN 'REMITO ' + LTRIM(RTRIM(PD.NroRemito)) ELSE LTRIM(RTRIM(PD.NroRemito)) END) AND RTRIM(DET.FART) = @art))
         OR (PD.Origen = 'LURO' AND EXISTS (SELECT 1 FROM DRAGONFISH_LURO.ZooLogic.COMPROBANTEV C WITH (NOLOCK) INNER JOIN DRAGONFISH_LURO.ZooLogic.COMPROBANTEVDET DET WITH (NOLOCK) ON C.CODIGO = DET.CODIGO WHERE C.FLETRA = 'R' AND C.ANULADO = 0 AND C.DESCFW = (CASE WHEN PD.Auditoria LIKE '%PALET APP%' THEN 'REMITO ' + LTRIM(RTRIM(PD.NroRemito)) ELSE LTRIM(RTRIM(PD.NroRemito)) END) AND RTRIM(DET.FART) = @art))
         OR (PD.Origen = 'PERALTA' AND EXISTS (SELECT 1 FROM DRAGONFISH_PERALTA.ZooLogic.COMPROBANTEV C WITH (NOLOCK) INNER JOIN DRAGONFISH_PERALTA.ZooLogic.COMPROBANTEVDET DET WITH (NOLOCK) ON C.CODIGO = DET.CODIGO WHERE C.FLETRA = 'R' AND C.ANULADO = 0 AND C.DESCFW = (CASE WHEN PD.Auditoria LIKE '%PALET APP%' THEN 'REMITO ' + LTRIM(RTRIM(PD.NroRemito)) ELSE LTRIM(RTRIM(PD.NroRemito)) END) AND RTRIM(DET.FART) = @art))
        )
        ORDER BY P.NroPalet;
        """;

    public async Task<IReadOnlyList<UbicacionArtDto>> BuscarEnPaletsAsync(string codigo, CancellationToken ct = default)
    {
        var cod = Sanitizar(codigo);
        if (cod.Length == 0) return new List<UbicacionArtDto>();
        using var cn = _db.Create();
        var palets = (await cn.QueryAsync<UbicacionArtDto>(new CommandDefinition(
            SqlPaletsConArticulo, new { art = cod }, commandTimeout: 120, cancellationToken: ct))).ToList();
        foreach (var p in palets) { p.EsDeposito = true; p.Ubicacion = "DEPÓSITO"; }
        return palets;
    }

    private sealed record StockRow(decimal StockLocal, decimal EnTransito);

    // Stock COMB del local: LURO/PERALTA EN VIVO (OPENQUERY) con fallback a réplica; CENTRAL directo.
    private static async Task<StockRow> StockLocalAsync(
        Microsoft.Data.SqlClient.SqlConnection cn, string ubic, string artCod, CancellationToken ct)
    {
        var db = DbFor(ubic);
        var u = (ubic ?? "").ToUpperInvariant();
        var linked = u == "LURO" ? "marketluro.ddns.net" : (u == "PERALTA" ? "marketperalta.ddns.net" : null);

        const string core =
            "WITH S AS (SELECT *, ROW_NUMBER() OVER (PARTITION BY COART,COCOL,TALLE ORDER BY FALTAFW DESC, HALTAFW DESC) AS Fila "
          + "FROM {0}.Zoologic.COMB WITH(NOLOCK) WHERE COART={1}) "
          + "SELECT StockLocal=ISNULL(SUM(COCANT),0), EnTransito=ISNULL(SUM(ENTRANSITO),0) FROM S WHERE Fila=1";

        if (linked is not null)
        {
            var inner = string.Format(core, db, $"'{artCod}'");
            var sqlLive = $"SELECT * FROM OPENQUERY([{linked}], '{inner.Replace("'", "''")}')";
            try
            {
                return await cn.QuerySingleOrDefaultAsync<StockRow>(new CommandDefinition(sqlLive, cancellationToken: ct))
                       ?? new StockRow(0, 0);
            }
            catch { /* el local no respondió → fallback a réplica */ }
        }

        var sqlRep = string.Format(core, db, "@art");
        return await cn.QuerySingleOrDefaultAsync<StockRow>(new CommandDefinition(sqlRep, new { art = artCod }, cancellationToken: ct))
               ?? new StockRow(0, 0);
    }

    public async Task<byte[]?> ObtenerFotoAsync(string codigo, bool ia, CancellationToken ct = default)
    {
        var cod = Sanitizar(codigo);
        if (cod.Length == 0) return null;
        var col = ia ? "FotoIA" : "FotoDrive";
        var sql = $"SELECT TOP 1 {col} FROM GoogleDriveFotosArticulos WHERE Codigo=@cod AND {col} IS NOT NULL ORDER BY ID DESC";
        using var cn = _db.Create();
        return await cn.ExecuteScalarAsync<byte[]?>(new CommandDefinition(sql, new { cod }, cancellationToken: ct));
    }
}
