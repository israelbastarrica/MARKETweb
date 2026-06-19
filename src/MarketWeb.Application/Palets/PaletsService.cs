using System.Data;
using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Palets;

namespace MarketWeb.Application.Palets;

public sealed class PaletsService : IPaletsService
{
    private readonly ISqlConnectionFactory _db;
    public PaletsService(ISqlConnectionFactory db) => _db = db;

    private sealed class Row
    {
        public int id { get; set; }
        public int NroPalet { get; set; }
        public DateTime? FechaDesarme { get; set; }
        public DateTime? FechaImpresion { get; set; }
        public string? RemitosDesc { get; set; }
        public string? UbicacionDesc { get; set; }
        public int TieneImpresion { get; set; }
        public string? Articulos { get; set; }
        public string? Tipo { get; set; }
        public string? Categoria { get; set; }
    }

    public async Task<IReadOnlyList<PaletDto>> ListarAsync(
        string? nroPalet, string? codArticulo, string? tipo, string? categoria,
        bool verDesarmados, DateTime desde, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<Row>(new CommandDefinition(
            "sp_ConsultaPalets",
            new
            {
                NroPalet = nroPalet ?? "",
                VerDesarmados = verDesarmados,
                CodArticulo = codArticulo ?? "",
                Tipo = (tipo ?? "") == "TODOS" ? "" : (tipo ?? ""),
                Categoria = (categoria ?? "") == "TODOS" ? "" : (categoria ?? ""),
                FechaDesde = desde.Date
            },
            commandType: CommandType.StoredProcedure, commandTimeout: 180, cancellationToken: ct));

        return rows.Select(r => new PaletDto
        {
            Id = r.id,
            NroPalet = r.NroPalet,
            Remitos = LimpiarRemitos(r.RemitosDesc),
            Ubicacion = r.UbicacionDesc ?? "",
            Articulos = r.Articulos ?? "",
            Tipo = r.Tipo,
            Categoria = r.Categoria,
            Impreso = r.TieneImpresion == 1,
            Desarmado = r.FechaDesarme is not null
        }).ToList();
    }

    // El SP devuelve "REMITOS: 10" o "DESARMADO". La columna es chica → mostramos solo el número
    // (dejamos "DESARMADO" tal cual).
    private static string LimpiarRemitos(string? desc)
    {
        var s = (desc ?? "").Trim();
        const string pref = "REMITOS:";
        return s.StartsWith(pref, StringComparison.OrdinalIgnoreCase) ? s[pref.Length..].Trim() : s;
    }

    public async Task<IReadOnlyList<string>> ListarTiposAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT DISTINCT TIPO.DESCRIP FROM DRAGONFISH_CENTRAL.ZooLogic.ART ART LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.TIPOART TIPO ON TIPO.COD = ART.TIPOARTI WHERE TIPO.DESCRIP <> '' ORDER BY TIPO.DESCRIP;";
        using var cn = _db.Create();
        return (await cn.QueryAsync<string>(new CommandDefinition(sql, commandTimeout: 60, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<string>> ListarCategoriasAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT DISTINCT CATE.DESCRIP FROM DRAGONFISH_CENTRAL.ZooLogic.ART ART LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.CATEGART CATE ON CATE.COD = ART.CATEARTI WHERE CATE.DESCRIP <> '' ORDER BY CATE.DESCRIP;";
        using var cn = _db.Create();
        return (await cn.QueryAsync<string>(new CommandDefinition(sql, commandTimeout: 60, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<PaletArticuloDto>> ListarArticulosAsync(int idPalet, CancellationToken ct = default)
    {
        // Por cada origen, matchea PaletsDetalle.NroRemito con COMPROBANTEV.DESCFW del Dragonfish del origen.
        // La descripción del artículo se toma de DRAGONFISH_CENTRAL.ART (catálogo global; PERALTA.ART está vacío).
        string Rama(string origen, string baseDb) => $@"
            SELECT Origen = '{origen}', Codigo = RTRIM(DET.FART), Descripcion = MAX(RTRIM(A.ARTDES)),
                   Combo = MAX(RTRIM(A.CLASIFART)), Remito = DETP.NroRemito, Cantidad = SUM(DET.FCANT)
            FROM MARKET.dbo.PaletsDetalle DETP WITH (NOLOCK)
            INNER JOIN {baseDb}.ZooLogic.COMPROBANTEV C WITH (NOLOCK)
                ON C.FLETRA = 'R' AND C.ANULADO = 0
               AND C.DESCFW = (CASE WHEN DETP.Auditoria LIKE '%PALET APP%'
                                    THEN 'REMITO ' + LTRIM(RTRIM(DETP.NroRemito))
                                    ELSE LTRIM(RTRIM(DETP.NroRemito)) END)
            INNER JOIN {baseDb}.ZooLogic.COMPROBANTEVDET DET WITH (NOLOCK) ON C.CODIGO = DET.CODIGO
            LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.ART A WITH (NOLOCK) ON DET.FART = A.ARTCOD
            WHERE DETP.idPalet = @idPalet AND DETP.Eliminado = 0 AND DETP.Origen = '{origen}'
            GROUP BY DET.FART, DETP.NroRemito";

        var sql = $@"
            {Rama("CENTRAL", "DRAGONFISH_CENTRAL")}
            UNION ALL
            {Rama("LURO", "DRAGONFISH_LURO")}
            UNION ALL
            {Rama("PERALTA", "DRAGONFISH_PERALTA")}
            ORDER BY Origen, Codigo;";

        using var cn = _db.Create();
        return (await cn.QueryAsync<PaletArticuloDto>(new CommandDefinition(sql, new { idPalet }, commandTimeout: 120, cancellationToken: ct))).ToList();
    }

    public async Task DesarmarAsync(int idPalet, CancellationToken ct = default)
    {
        var auditoria = $"Desarmado | WEB | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
        const string sql = """
            UPDATE Palets SET FechaDesarme = GETDATE(), Auditoria = @auditoria WHERE id = @idPalet;
            UPDATE PaletsDetalle SET Eliminado = 1, Auditoria = @auditoria WHERE IDPalet = @idPalet AND Eliminado = 0;
            """;
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { idPalet, auditoria }, cancellationToken: ct));
    }
}
