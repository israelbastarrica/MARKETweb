using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Dragonfish;

namespace MarketWeb.Application.Remitos;

public sealed class RemitosLookupService : IRemitosLookupService
{
    private readonly ISqlConnectionFactory _db;
    public RemitosLookupService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<UltimaRepoItemDto>> UltimaRepoAsync(string local, CancellationToken ct = default)
    {
        // La ultima corrida activa QUE TENGA filas del local pedido (no la ultima global:
        // una corrida posterior sin filas de ese local dejaria la lista vacia).
        const string sql =
            "SELECT DISTINCT RTRIM(d.ARTCOD) AS ArtCod, RTRIM(d.ARTDES) AS ArtDes " +
            "FROM MARKET.dbo.ReposicionDetalle d " +
            "WHERE d.IDReposicion = ( " +
            "    SELECT TOP 1 r.ID FROM MARKET.dbo.Reposicion r " +
            "    WHERE ISNULL(r.Eliminado, 0) = 0 " +
            "      AND EXISTS ( SELECT 1 FROM MARKET.dbo.ReposicionDetalle dd " +
            "                   WHERE dd.IDReposicion = r.ID AND RTRIM(dd.LocalDestino) = @Local ) " +
            "    ORDER BY r.FechaHoraCorrida DESC " +
            ") AND RTRIM(d.LocalDestino) = @Local " +
            "ORDER BY ArtCod;";   // alias, no d.ARTCOD: con SELECT DISTINCT el ORDER BY debe estar en el select

        using var cn = _db.Create();
        return (await cn.QueryAsync<UltimaRepoItemDto>(new CommandDefinition(sql, new { Local = local.ToUpperInvariant() }, cancellationToken: ct))).ToList();
    }

    public async Task<ArticuloLookupDto?> BuscarArticuloAsync(string cod, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cod)) return null;
        cod = cod.Trim().ToUpperInvariant();

        const string sql =
            "SELECT TOP 1 RTRIM(ARTCOD) AS Cod, RTRIM(ARTDES) AS Des " +
            "FROM DRAGONFISH_CENTRAL.ZooLogic.ART " +
            "WHERE RTRIM(ARTCOD) = @Cod; " +
            "SELECT DISTINCT RTRIM(COCOL) AS Color, RTRIM(TALLE) AS Talle " +
            "FROM DRAGONFISH_CENTRAL.ZooLogic.COMB " +
            "WHERE RTRIM(COART) = @Cod " +
            "ORDER BY Color, Talle;";

        using var cn = _db.Create();
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { Cod = cod }, cancellationToken: ct));

        var art = await multi.ReadFirstOrDefaultAsync<ArtRow>();
        if (art is null) return null;

        var combos = (await multi.ReadAsync<CombRow>()).ToList();
        return new ArticuloLookupDto
        {
            Cod = art.Cod,
            Des = art.Des,
            Variantes = combos
                .Where(c => !string.IsNullOrWhiteSpace(c.Talle) || !string.IsNullOrWhiteSpace(c.Color))
                .Select(c => new ComboVarianteDto { Color = c.Color, Talle = c.Talle })
                .ToList(),
        };
    }

    public async Task<BolsaDto?> BuscarBolsaAsync(string nroBolsa, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nroBolsa)) return null;
        var nro = nroBolsa.Trim();

        using var cn = _db.Create();

        // 1) Bolsa por código de barras (NroBolsa). Primero activa (Eliminado=0); si no aparece,
        //    caemos al fallback SIN filtro: en el depósito hay bolsas con baja lógica que igual se remiten.
        var id = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 ID FROM MARKET.dbo.PacksBolsas WHERE RTRIM(NroBolsa) = @Nro AND ISNULL(Eliminado, 0) = 0 ORDER BY ID DESC;",
            new { Nro = nro }, cancellationToken: ct));
        var eliminada = false;
        if (id is null)
        {
            id = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT TOP 1 ID FROM MARKET.dbo.PacksBolsas WHERE RTRIM(NroBolsa) = @Nro ORDER BY ID DESC;",
                new { Nro = nro }, cancellationToken: ct));
            eliminada = id is not null;
        }
        if (id is null) return null;

        // 2) Detalle (artículo + color + talle + cantidad) + descripción del ART. Si la bolsa estaba
        //    activa, excluimos renglones dados de baja; si la bolsa venía eliminada, su detalle también
        //    está en Eliminado=1 → traemos todos (si no, la encontraríamos vacía).
        var sqlDet =
            "SELECT RTRIM(BD.ARTCOD) AS ArtCod, RTRIM(ISNULL(ART.ARTDES, '')) AS ArtDes, " +
            "       RTRIM(ISNULL(BD.Color, '')) AS Color, RTRIM(ISNULL(BD.Talle, '')) AS Talle, BD.Cantidad " +
            "FROM MARKET.dbo.PacksBolsasDetalle BD " +
            "LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.ART ART ON RTRIM(ART.ARTCOD) = RTRIM(BD.ARTCOD) " +
            "WHERE BD.IDPackBolsa = @Id " +
            (eliminada ? "" : "AND ISNULL(BD.Eliminado, 0) = 0 ") +
            "ORDER BY BD.ARTCOD, BD.Color, BD.Talle;";

        var renglones = (await cn.QueryAsync<BolsaRenglonDto>(new CommandDefinition(sqlDet, new { Id = id.Value }, cancellationToken: ct))).ToList();

        return new BolsaDto { NroBolsa = nro, IdPackBolsa = id.Value, Eliminada = eliminada, Renglones = renglones };
    }

    // Levanta el detalle de un remito YA EXISTENTE de un local (LURO/PERALTA), identificado por
    // su número visible Punto-Número. Lee EN VIVO por OPENQUERY (el remito puede ser reciente y la
    // réplica viene atrasada) con fallback a la réplica. Devuelve artículo + color(código) + talle + cantidad.
    public async Task<IReadOnlyList<BolsaRenglonDto>> BuscarRemitoLocalAsync(string local, int punto, int numero, CancellationToken ct = default)
    {
        var u = (local ?? "").Trim().ToUpperInvariant();
        if (u != "LURO" && u != "PERALTA") return new List<BolsaRenglonDto>();
        if (punto <= 0 || numero <= 0) return new List<BolsaRenglonDto>();

        var db = "DRAGONFISH_" + u;
        var linked = u == "LURO" ? "marketluro.ddns.net" : "marketperalta.ddns.net";

        // {0} = base Dragonfish. FLETRA='R' (remito), no anulado. Color = CCOLOR (código), no FCOLTXT (texto).
        // CAST de la cantidad a INT para que Dapper no falle materializando el SUM (decimal) en int.
        const string core =
            "SELECT RTRIM(DET.FART) AS ArtCod, RTRIM(ISNULL(ART.ARTDES,'''')) AS ArtDes, " +
            "RTRIM(ISNULL(DET.CCOLOR,'''')) AS Color, RTRIM(ISNULL(DET.TALLE,'''')) AS Talle, " +
            "CAST(SUM(DET.FCANT) AS INT) AS Cantidad " +
            "FROM {0}.ZooLogic.COMPROBANTEV COMP WITH(NOLOCK) " +
            "INNER JOIN {0}.ZooLogic.COMPROBANTEVDET DET WITH(NOLOCK) ON COMP.CODIGO=DET.CODIGO " +
            "LEFT JOIN {0}.ZooLogic.ART ART WITH(NOLOCK) ON DET.FART=ART.ARTCOD " +
            "WHERE COMP.FLETRA=''R'' AND COMP.ANULADO=0 AND COMP.FPTOVEN={1} AND COMP.FNUMCOMP={2} " +
            "GROUP BY DET.FART, ART.ARTDES, DET.CCOLOR, DET.TALLE HAVING SUM(DET.FCANT) <> 0 " +
            "ORDER BY DET.FART, DET.CCOLOR, DET.TALLE";

        using var cn = _db.Create();

        // Busca por un punto dado: EN VIVO (OPENQUERY, inner con comillas dobladas) con fallback a réplica.
        async Task<List<BolsaRenglonDto>> BuscarPorPunto(int p)
        {
            var inner = string.Format(core, db, p, numero);
            var sqlLive = $"SELECT * FROM OPENQUERY([{linked}], '{inner}')";
            try
            {
                var live = (await cn.QueryAsync<BolsaRenglonDto>(new CommandDefinition(sqlLive, cancellationToken: ct))).ToList();
                if (live.Count > 0) return live;
            }
            catch { /* el local no respondió → réplica */ }

            var sqlRep = string.Format(core, db, p, numero).Replace("''", "'");
            return (await cn.QueryAsync<BolsaRenglonDto>(new CommandDefinition(sqlRep, cancellationToken: ct))).ToList();
        }

        var res = await BuscarPorPunto(punto);

        // Si pusieron el punto 1 y no apareció, reintentamos con el punto propio del local
        // (LURO=2, PERALTA=3). Solo aplica cuando tipearon 1; si pusieron otro punto, se respeta.
        if (res.Count == 0 && punto == 1)
        {
            var alt = u == "PERALTA" ? 3 : 2;
            res = await BuscarPorPunto(alt);
        }

        return res;
    }

    // Motivos de remito (ZooLogic.MOTIVO). Se excluye el 13 (Insumos), que va por su propio circuito.
    public async Task<IReadOnlyList<MotivoDto>> MotivosAsync(CancellationToken ct = default)
    {
        const string sql =
            "SELECT RTRIM(MOTCOD) AS Cod, RTRIM(MOTDES) AS Des " +
            "FROM DRAGONFISH_CENTRAL.ZooLogic.MOTIVO " +
            "WHERE RTRIM(MOTCOD) <> '13' " +
            "ORDER BY MOTCOD;";
        using var cn = _db.Create();
        return (await cn.QueryAsync<MotivoDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task RegistrarRemitoTabletAsync(long? numero, string? codigo, string local, string? licencia,
        string? dispositivoPc, string? motivo, string? usuario, CancellationToken ct = default)
    {
        // Crea la tabla si falta (idempotente) y guarda el mapeo. El N° de remito que devuelve Dragon
        // es la clave para que el agente de impresión rutee la impresora (todos caen con SALTAFW=808601).
        const string sql = @"
IF OBJECT_ID('MARKET.dbo.RemitosTabletImpresion','U') IS NULL
BEGIN
    CREATE TABLE MARKET.dbo.RemitosTabletImpresion(
        Id            INT IDENTITY(1,1) CONSTRAINT PK_RemitosTabletImpresion PRIMARY KEY,
        Numero        BIGINT        NULL,    -- N° de remito que devolvió Dragon (clave de ruteo)
        Codigo        NVARCHAR(60)  NULL,    -- código del comprobante (Dragon)
        Letra         NVARCHAR(2)   NULL,    -- 'R'
        Local         NVARCHAR(20)  NULL,
        Licencia      NVARCHAR(20)  NULL,    -- impresora destino (809131 / 809129)
        DispositivoPc NVARCHAR(60)  NULL,    -- ""Esta PC"" (header X-Pc): TabletLog1 / TabletLog2
        Motivo        NVARCHAR(10)  NULL,
        Usuario       NVARCHAR(120) NULL,    -- mail del que dio el alta
        FechaHora     DATETIME      NOT NULL CONSTRAINT DF_RemitosTabletImpresion_Fecha DEFAULT GETDATE(),
        Impreso       BIT           NOT NULL CONSTRAINT DF_RemitosTabletImpresion_Impreso DEFAULT 0,
        FechaImpreso  DATETIME      NULL
    );
    CREATE INDEX IX_RemitosTabletImpresion_Numero ON MARKET.dbo.RemitosTabletImpresion(Numero);
END;

INSERT INTO MARKET.dbo.RemitosTabletImpresion (Numero, Codigo, Letra, Local, Licencia, DispositivoPc, Motivo, Usuario)
VALUES (@Numero, @Codigo, 'R', @Local, @Licencia, @DispositivoPc, @Motivo, @Usuario);";

        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Numero = numero,
            Codigo = Recortar(codigo, 60),
            Local = Recortar(local?.Trim().ToUpperInvariant(), 20),
            Licencia = Recortar(licencia?.Trim(), 20),
            DispositivoPc = Recortar(dispositivoPc?.Trim(), 60),
            Motivo = Recortar(motivo?.Trim(), 10),
            Usuario = Recortar(usuario?.Trim(), 120)
        }, cancellationToken: ct));
    }

    private static string? Recortar(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));

    private sealed record ArtRow(string Cod, string Des);
    private sealed record CombRow(string Color, string Talle);
}
