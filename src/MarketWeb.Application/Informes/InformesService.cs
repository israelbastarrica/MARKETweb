using System.Data;
using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Informes;

namespace MarketWeb.Application.Informes;

/// <summary>
/// Informe de Ventas: por artículo, Comprado (remitos de ingreso REMCOMPRA; si no hay, lo enviado a
/// los locales) vs Vendido en un rango, con costo a la fecha (LISTA0), margen y "venta forzada"
/// (el precio LISTA1 bajó respecto del lanzamiento). Filtra por proveedor (ARTFAB) / temporada / año.
/// </summary>
public sealed class InformesService : IInformesService
{
    private readonly ISqlConnectionFactory _db;
    private const string DF = "DRAGONFISH_CENTRAL.ZooLogic";
    public InformesService(ISqlConnectionFactory db) => _db = db;

    public async Task<InformeVentaCombosDto> CombosAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        var dto = new InformeVentaCombosDto
        {
            Temporadas = (await cn.QueryAsync<string>(new CommandDefinition(
                $"SELECT DISTINCT TEM.TDES FROM {DF}.ART ART LEFT JOIN {DF}.TEMPORADA TEM ON TEM.TCOD=ART.ATEMPORADA WHERE TEM.TDES<>'' ORDER BY TEM.TDES",
                commandTimeout: 60, cancellationToken: ct))).ToList(),
            Anios = (await cn.QueryAsync<int>(new CommandDefinition(
                $"SELECT DISTINCT ANO FROM {DF}.ART WHERE ANO<>0 ORDER BY ANO DESC",
                commandTimeout: 60, cancellationToken: ct))).ToList(),
            Proveedores = (await cn.QueryAsync<ProveedorItemDto>(new CommandDefinition(
                $"SELECT DISTINCT Cod=RTRIM(PRO.CLCOD), Nombre=RTRIM(PRO.CLNOM) FROM {DF}.ART ART LEFT JOIN {DF}.PROV PRO ON PRO.CLCOD=ART.ARTFAB WHERE PRO.CLNOM<>'' ORDER BY Nombre",
                commandTimeout: 60, cancellationToken: ct))).ToList(),
        };
        return dto;
    }

    public async Task<IReadOnlyList<InformeVentaFila>> VentasAsync(InformeVentaFiltro f, CancellationToken ct = default)
    {
        const string sql = @"
DROP TABLE IF EXISTS #Arts, #Ini, #Ven;

SELECT RTRIM(A.ARTCOD) AS ArtCod, RTRIM(A.ARTDES) AS ArtDes,
       RTRIM(ISNULL(T.TDES,'')) AS Temporada, A.ANO AS Anio
INTO #Arts
FROM DRAGONFISH_CENTRAL.ZooLogic.ART A
LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.TEMPORADA T ON T.TCOD = A.ATEMPORADA
WHERE (@Prov = '' OR RTRIM(A.ARTFAB) = @Prov)
  AND (@Temp IS NULL OR RTRIM(ISNULL(T.TDES,'')) = @Temp)
  AND (@Anio = 0 OR A.ANO = @Anio);
CREATE INDEX ix_arts ON #Arts(ArtCod);

-- Precio inicial (LISTA1 lanzamiento) y precio de venta vigente al fin del período
SELECT a.ArtCod,
       PrecioInicial = ISNULL((SELECT TOP 1 P.PDIRECTO FROM DRAGONFISH_CENTRAL.ZooLogic.PRECIOAR P WITH(NOLOCK)
            WHERE P.ARTICULO=a.ArtCod AND P.LISTAPRE='LISTA1' AND P.PDIRECTO>0
            ORDER BY P.FECHAVIG ASC, P.HMODIFW ASC),0),
       PrecioVenta = ISNULL((SELECT TOP 1 P.PDIRECTO FROM DRAGONFISH_CENTRAL.ZooLogic.PRECIOAR P WITH(NOLOCK)
            WHERE P.ARTICULO=a.ArtCod AND P.LISTAPRE='LISTA1' AND P.PDIRECTO>0 AND P.FECHAVIG<=@Hasta
            ORDER BY P.FECHAVIG DESC, P.HMODIFW DESC),0)
INTO #Ini FROM #Arts a;
CREATE INDEX ix_ini ON #Ini(ArtCod);

-- Ventas SOLO de esos artículos (costo a la fecha LISTA0)
SELECT v.ArtCod, Vendido=SUM(v.Cant), Facturado=SUM(v.Facturado), Costo=SUM(v.Costo)
INTO #Ven
FROM (
    SELECT RTRIM(D.FART) AS ArtCod, D.FCANT*C.SIGNOMOV AS Cant, D.MNTPTOT*C.SIGNOMOV AS Facturado,
           CASE WHEN ISNULL(PV.PDIRECTO,0) IN (0,1) THEN ISNULL(PA.PDIRECTO,0) ELSE PV.PDIRECTO END*(D.FCANT*C.SIGNOMOV) AS Costo
    FROM DRAGONFISH_LURO.ZooLogic.COMPROBANTEV C WITH(NOLOCK)
    JOIN DRAGONFISH_LURO.ZooLogic.COMPROBANTEVDET D WITH(NOLOCK) ON C.CODIGO=D.CODIGO
    JOIN #Arts a ON a.ArtCod = RTRIM(D.FART)
    OUTER APPLY (SELECT TOP 1 P.PDIRECTO FROM DRAGONFISH_CENTRAL.ZooLogic.PRECIOAR P WITH(NOLOCK)
                 WHERE P.ARTICULO=RTRIM(D.FART) AND P.FECHAVIG<=C.FFCH AND P.LISTAPRE='LISTA0' ORDER BY P.FECHAVIG DESC, P.HMODIFW DESC) PV
    OUTER APPLY (SELECT TOP 1 P.PDIRECTO FROM DRAGONFISH_CENTRAL.ZooLogic.PRECIOAR P WITH(NOLOCK)
                 WHERE P.ARTICULO=RTRIM(D.FART) AND P.LISTAPRE='LISTA0' AND P.PDIRECTO>100 ORDER BY P.FECHAVIG, P.HMODIFW) PA
    WHERE C.ANULADO=0 AND C.FLETRA<>'R' AND C.FFCH BETWEEN @Desde AND @Hasta AND LEFT(RTRIM(D.FART),1)<>'Z'
    UNION ALL
    SELECT RTRIM(D.FART), D.FCANT*C.SIGNOMOV, D.MNTPTOT*C.SIGNOMOV,
           CASE WHEN ISNULL(PV.PDIRECTO,0) IN (0,1) THEN ISNULL(PA.PDIRECTO,0) ELSE PV.PDIRECTO END*(D.FCANT*C.SIGNOMOV)
    FROM DRAGONFISH_PERALTA.ZooLogic.COMPROBANTEV C WITH(NOLOCK)
    JOIN DRAGONFISH_PERALTA.ZooLogic.COMPROBANTEVDET D WITH(NOLOCK) ON C.CODIGO=D.CODIGO
    JOIN #Arts a ON a.ArtCod = RTRIM(D.FART)
    OUTER APPLY (SELECT TOP 1 P.PDIRECTO FROM DRAGONFISH_CENTRAL.ZooLogic.PRECIOAR P WITH(NOLOCK)
                 WHERE P.ARTICULO=RTRIM(D.FART) AND P.FECHAVIG<=C.FFCH AND P.LISTAPRE='LISTA0' ORDER BY P.FECHAVIG DESC, P.HMODIFW DESC) PV
    OUTER APPLY (SELECT TOP 1 P.PDIRECTO FROM DRAGONFISH_CENTRAL.ZooLogic.PRECIOAR P WITH(NOLOCK)
                 WHERE P.ARTICULO=RTRIM(D.FART) AND P.LISTAPRE='LISTA0' AND P.PDIRECTO>100 ORDER BY P.FECHAVIG, P.HMODIFW) PA
    WHERE C.ANULADO=0 AND C.FLETRA<>'R' AND C.FFCH BETWEEN @Desde AND @Hasta AND LEFT(RTRIM(D.FART),1)<>'Z'
) v
GROUP BY v.ArtCod;

;WITH Comprado AS (
    SELECT RTRIM(D.FART) AS ArtCod, SUM(D.FCANT) AS Comprado
    FROM DRAGONFISH_CENTRAL.ZooLogic.REMCOMPRA C
    JOIN DRAGONFISH_CENTRAL.ZooLogic.REMCOMPRADET D ON C.CODIGO=D.CODIGO
    WHERE ISNULL(C.ANULADO,0)=0 GROUP BY RTRIM(D.FART)
),
Enviado AS (
    SELECT ArtCod, SUM(Cant) AS Enviado FROM (
        SELECT RTRIM(D.FART) AS ArtCod, D.FCANT AS Cant FROM DRAGONFISH_CENTRAL.ZooLogic.COMPROBANTEV C
        JOIN DRAGONFISH_CENTRAL.ZooLogic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO
        WHERE C.FLETRA='R' AND C.ANULADO=0 AND RTRIM(C.FCLIENTE) IN ('LURO','PERALTA')
        UNION ALL
        SELECT RTRIM(D.FART), D.FCANT FROM DRAGONFISH_CCENTRAL.ZooLogic.COMPROBANTEV C
        JOIN DRAGONFISH_CCENTRAL.ZooLogic.COMPROBANTEVDET D ON C.CODIGO=D.CODIGO
        WHERE C.FLETRA='R' AND C.ANULADO=0 AND RTRIM(C.FCLIENTE) IN ('LURO','PERALTA')
    ) z GROUP BY ArtCod
)
SELECT a.ArtCod, a.ArtDes, a.Temporada, CAST(a.Anio AS VARCHAR(10)) AS Anio,
       Comprado = CASE WHEN ISNULL(co.Comprado,0)>0 THEN co.Comprado ELSE ISNULL(en.Enviado,0) END,
       FuenteStock = CASE WHEN ISNULL(co.Comprado,0)>0 THEN 'Compra' ELSE 'Envío' END,
       i.PrecioInicial, i.PrecioVenta,
       Forzada = CAST(CASE WHEN i.PrecioVenta>0 AND i.PrecioInicial>0 AND i.PrecioVenta < i.PrecioInicial THEN 1 ELSE 0 END AS BIT),
       Vendido = ISNULL(vn.Vendido,0),
       Facturado = ISNULL(vn.Facturado,0),
       PrecioVentaProm = CASE WHEN ISNULL(vn.Vendido,0)>0 THEN CAST(vn.Facturado/vn.Vendido AS DECIMAL(18,2)) END,
       Costo = ISNULL(vn.Costo,0),
       MargenPesos = ISNULL(vn.Facturado,0)-ISNULL(vn.Costo,0),
       MargenPct = CASE WHEN ISNULL(vn.Facturado,0)>0 THEN CAST((vn.Facturado-vn.Costo)/vn.Facturado*100 AS DECIMAL(6,2)) END
FROM #Arts a
JOIN #Ini i ON i.ArtCod=a.ArtCod
LEFT JOIN Comprado co ON co.ArtCod=a.ArtCod
LEFT JOIN Enviado  en ON en.ArtCod=a.ArtCod
LEFT JOIN #Ven     vn ON vn.ArtCod=a.ArtCod
ORDER BY a.ArtCod;

DROP TABLE IF EXISTS #Arts, #Ini, #Ven;";

        var p = new
        {
            Prov = string.IsNullOrWhiteSpace(f.ProveedorCod) ? "" : f.ProveedorCod.Trim(),
            Temp = string.IsNullOrWhiteSpace(f.Temporada) || f.Temporada == "TODOS" ? (string?)null : f.Temporada.Trim(),
            Anio = f.Anio,
            Desde = f.Desde.Date,
            Hasta = f.Hasta.Date
        };

        using var cn = _db.Create();
        return (await cn.QueryAsync<InformeVentaFila>(new CommandDefinition(sql, p, commandTimeout: 180, cancellationToken: ct))).ToList();
    }

    public async Task<byte[]> VentasExcelAsync(InformeVentaFiltro f, CancellationToken ct = default)
    {
        var filas = await VentasAsync(f, ct);

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("Ventas");

        string[] headers =
        {
            "Código", "Descripción", "Temporada", "Año", "Comprado", "Origen",
            "P. inicial", "P. venta", "Forzada", "Vendido", "P. vta prom",
            "Facturado", "Costo", "Margen $", "Margen %"
        };
        for (int c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        int r = 2;
        foreach (var x in filas)
        {
            ws.Cell(r, 1).Value = x.ArtCod;
            ws.Cell(r, 2).Value = x.ArtDes;
            ws.Cell(r, 3).Value = x.Temporada;
            ws.Cell(r, 4).Value = x.Anio;
            ws.Cell(r, 5).Value = x.Comprado;
            ws.Cell(r, 6).Value = x.FuenteStock;
            ws.Cell(r, 7).Value = x.PrecioInicial;
            ws.Cell(r, 8).Value = x.PrecioVenta;
            ws.Cell(r, 9).Value = x.Forzada ? "Sí" : "";
            ws.Cell(r, 10).Value = x.Vendido;
            ws.Cell(r, 11).Value = x.PrecioVentaProm ?? 0;
            ws.Cell(r, 12).Value = x.Facturado;
            ws.Cell(r, 13).Value = x.Costo;
            ws.Cell(r, 14).Value = x.MargenPesos;
            ws.Cell(r, 15).Value = x.MargenPct ?? 0;
            r++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, 1, headers.Length).SetAutoFilter();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<IReadOnlyList<InformeSerieFila>> VentasSerieAsync(InformeVentaFiltro f, string dimension, CancellationToken ct = default)
    {
        // Granularidad adaptativa según el span: ≤16 días = día, ≤100 = semana, más = mes.
        var dias = (int)(f.Hasta.Date - f.Desde.Date).TotalDays;
        var gran = dias <= 16 ? "D" : dias <= 100 ? "W" : "M";
        var dim = (dimension ?? "").Trim();
        if (dim is not ("Familia" or "Tipo" or "Categoria" or "Combo")) dim = "Familia";

        const string sql = @"
SET DATEFIRST 1;
DROP TABLE IF EXISTS #Arts;

SELECT RTRIM(A.ARTCOD) AS ArtCod,
       Grupo = RTRIM(CASE @Dim
                 WHEN 'Familia'   THEN ISNULL(FAM.DESCRIP,'')
                 WHEN 'Tipo'      THEN ISNULL(TIPO.DESCRIP,'')
                 WHEN 'Categoria' THEN ISNULL(CATE.DESCRIP,'')
                 ELSE ISNULL(A.CLASIFART,'') END)
INTO #Arts
FROM DRAGONFISH_CENTRAL.ZooLogic.ART A
LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.TEMPORADA T ON T.TCOD=A.ATEMPORADA
LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.FAMILIA  FAM  ON FAM.COD=A.FAMILIA
LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.TIPOART  TIPO ON TIPO.COD=A.TIPOARTI
LEFT JOIN DRAGONFISH_CENTRAL.ZooLogic.CATEGART CATE ON CATE.COD=A.CATEARTI
WHERE (@Prov='' OR RTRIM(A.ARTFAB)=@Prov)
  AND (@Temp IS NULL OR RTRIM(ISNULL(T.TDES,''))=@Temp)
  AND (@Anio=0 OR A.ANO=@Anio);
CREATE INDEX ix_arts ON #Arts(ArtCod);

SELECT Periodo, Grupo = CASE WHEN Grupo='' THEN '(sin dato)' ELSE Grupo END,
       Unidades=SUM(Cant), Facturado=SUM(Facturado), Margen=SUM(Facturado)-SUM(Costo)
FROM (
    SELECT Periodo = CASE @Gran
                       WHEN 'D' THEN CAST(C.FFCH AS DATE)
                       WHEN 'W' THEN DATEADD(DAY, 1-DATEPART(WEEKDAY,C.FFCH), CAST(C.FFCH AS DATE))
                       ELSE DATEFROMPARTS(YEAR(C.FFCH),MONTH(C.FFCH),1) END,
           a.Grupo, D.FCANT*C.SIGNOMOV AS Cant, D.MNTPTOT*C.SIGNOMOV AS Facturado,
           CASE WHEN ISNULL(PV.PDIRECTO,0) IN (0,1) THEN ISNULL(PA.PDIRECTO,0) ELSE PV.PDIRECTO END*(D.FCANT*C.SIGNOMOV) AS Costo
    FROM DRAGONFISH_LURO.ZooLogic.COMPROBANTEV C WITH(NOLOCK)
    JOIN DRAGONFISH_LURO.ZooLogic.COMPROBANTEVDET D WITH(NOLOCK) ON C.CODIGO=D.CODIGO
    JOIN #Arts a ON a.ArtCod = RTRIM(D.FART)
    OUTER APPLY (SELECT TOP 1 P.PDIRECTO FROM DRAGONFISH_CENTRAL.ZooLogic.PRECIOAR P WITH(NOLOCK)
                 WHERE P.ARTICULO=RTRIM(D.FART) AND P.FECHAVIG<=C.FFCH AND P.LISTAPRE='LISTA0' ORDER BY P.FECHAVIG DESC, P.HMODIFW DESC) PV
    OUTER APPLY (SELECT TOP 1 P.PDIRECTO FROM DRAGONFISH_CENTRAL.ZooLogic.PRECIOAR P WITH(NOLOCK)
                 WHERE P.ARTICULO=RTRIM(D.FART) AND P.LISTAPRE='LISTA0' AND P.PDIRECTO>100 ORDER BY P.FECHAVIG, P.HMODIFW) PA
    WHERE C.ANULADO=0 AND C.FLETRA<>'R' AND C.FFCH BETWEEN @Desde AND @Hasta AND LEFT(RTRIM(D.FART),1)<>'Z'
    UNION ALL
    SELECT CASE @Gran
                       WHEN 'D' THEN CAST(C.FFCH AS DATE)
                       WHEN 'W' THEN DATEADD(DAY, 1-DATEPART(WEEKDAY,C.FFCH), CAST(C.FFCH AS DATE))
                       ELSE DATEFROMPARTS(YEAR(C.FFCH),MONTH(C.FFCH),1) END,
           a.Grupo, D.FCANT*C.SIGNOMOV, D.MNTPTOT*C.SIGNOMOV,
           CASE WHEN ISNULL(PV.PDIRECTO,0) IN (0,1) THEN ISNULL(PA.PDIRECTO,0) ELSE PV.PDIRECTO END*(D.FCANT*C.SIGNOMOV)
    FROM DRAGONFISH_PERALTA.ZooLogic.COMPROBANTEV C WITH(NOLOCK)
    JOIN DRAGONFISH_PERALTA.ZooLogic.COMPROBANTEVDET D WITH(NOLOCK) ON C.CODIGO=D.CODIGO
    JOIN #Arts a ON a.ArtCod = RTRIM(D.FART)
    OUTER APPLY (SELECT TOP 1 P.PDIRECTO FROM DRAGONFISH_CENTRAL.ZooLogic.PRECIOAR P WITH(NOLOCK)
                 WHERE P.ARTICULO=RTRIM(D.FART) AND P.FECHAVIG<=C.FFCH AND P.LISTAPRE='LISTA0' ORDER BY P.FECHAVIG DESC, P.HMODIFW DESC) PV
    OUTER APPLY (SELECT TOP 1 P.PDIRECTO FROM DRAGONFISH_CENTRAL.ZooLogic.PRECIOAR P WITH(NOLOCK)
                 WHERE P.ARTICULO=RTRIM(D.FART) AND P.LISTAPRE='LISTA0' AND P.PDIRECTO>100 ORDER BY P.FECHAVIG, P.HMODIFW) PA
    WHERE C.ANULADO=0 AND C.FLETRA<>'R' AND C.FFCH BETWEEN @Desde AND @Hasta AND LEFT(RTRIM(D.FART),1)<>'Z'
) z
GROUP BY Periodo, CASE WHEN Grupo='' THEN '(sin dato)' ELSE Grupo END
ORDER BY Periodo;

DROP TABLE IF EXISTS #Arts;";

        var p = new
        {
            Prov = string.IsNullOrWhiteSpace(f.ProveedorCod) ? "" : f.ProveedorCod.Trim(),
            Temp = string.IsNullOrWhiteSpace(f.Temporada) || f.Temporada == "TODOS" ? (string?)null : f.Temporada.Trim(),
            Anio = f.Anio,
            Desde = f.Desde.Date,
            Hasta = f.Hasta.Date,
            Dim = dim,
            Gran = gran
        };

        using var cn = _db.Create();
        return (await cn.QueryAsync<InformeSerieFila>(new CommandDefinition(sql, p, commandTimeout: 180, cancellationToken: ct))).ToList();
    }
}
