using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Produccion;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MarketWeb.Application.Produccion;

public interface ICatalogosService
{
    Task<IReadOnlyList<CatalogoDto>> ListarAsync(CancellationToken ct = default);
    Task<CatalogoDetalleDto?> DetalleAsync(int id, CancellationToken ct = default);
    Task<CatalogoCombosDto> CombosAsync(CancellationToken ct = default);
    /// <summary>Resuelve descripción + categoría de un ARTCOD desde Dragon (para agregar un renglón).</summary>
    Task<CatalogoRenglonDto?> ResolverArticuloAsync(string codigo, CancellationToken ct = default);
    /// <summary>Órdenes de pedido activas (PedidosOrdenes) para el selector — opción "Orden de Pedido".</summary>
    Task<IReadOnlyList<PedidoOrdenSelDto>> PedidosOrdenesAsync(string? filtro, CancellationToken ct = default);
    /// <summary>Artículos de una proforma de Dragon (PedidosOrdenes por NroOrden) — opción "Proforma Dragon".</summary>
    Task<IReadOnlyList<CatalogoRenglonDto>> ProformaAsync(int nroProforma, CancellationToken ct = default);
    Task<int> GuardarAsync(CatalogoGuardarRequest req, string aud, CancellationToken ct = default);
    Task<bool> EliminarAsync(int id, string aud, CancellationToken ct = default);
    /// <summary>PDF del catálogo: archivo físico en el server (preferido); si no está, el que dejó el .Net en la base.</summary>
    Task<byte[]?> ObtenerPdfAsync(int id, CancellationToken ct = default);
    /// <summary>Genera el PDF (tarjetas) y lo guarda como archivo físico en el server. Devuelve los bytes.</summary>
    Task<byte[]?> GenerarPdfAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Catálogos (Producción) — port fiel de frmABMCatalogo (.Net), SIN Canva.
/// Un catálogo = Nombre/Año/Temporada + lista de ítems ordenados. Tipos de ítem:
/// ARTÍCULO (por código), TEXTO (línea con fondo negro), OP {nroOrden} (renglón de una Orden de
/// Pedido de PedidosOrdenes) y DG {nroProforma} (artículos de una proforma de Dragon).
/// Reusa las tablas existentes Catalogos + CatalogosDetalle (mismas que usa el .Net → interoperan).
/// Descripción/categoría salen de Dragon (ART / CATEGART). Combos Año/Temporada desde Dragon (ART / TEMPORADA).
/// El PDF (las tarjetas) se genera aparte, en una segunda etapa.
/// </summary>
public sealed class CatalogosService : ICatalogosService
{
    private const string DbDragon = "DRAGONFISH_CENTRAL.ZooLogic";

    private readonly ISqlConnectionFactory _db;
    private readonly IConfiguration _cfg;
    public CatalogosService(ISqlConnectionFactory db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    /// <summary>
    /// Carpeta física donde viven los PDF de catálogos (config "Catalogos:PdfDir").
    /// Los archivos NO se guardan en la base para que no crezca; sólo el PDF en disco.
    /// Default: subcarpeta "Catalogos" al lado del ejecutable del server.
    /// </summary>
    private string PdfDir
    {
        get
        {
            var dir = _cfg["Catalogos:PdfDir"];
            if (string.IsNullOrWhiteSpace(dir))
                dir = Path.Combine(AppContext.BaseDirectory, "Datos", "Catalogos");
            return dir;
        }
    }

    private string PdfPath(int id) => Path.Combine(PdfDir, $"Catalogo_{id}.pdf");

    // Las tablas ya existen (las usa el .Net). El DDL es un salvavidas idempotente por si la base está limpia.
    private const string SchemaDdl = @"
IF OBJECT_ID('dbo.Catalogos','U') IS NULL
CREATE TABLE dbo.Catalogos(
    ID INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(200) NULL, [Año] INT NULL, Temporada NVARCHAR(80) NULL,
    ArchivoPDF VARBINARY(MAX) NULL,
    Eliminado BIT NOT NULL CONSTRAINT DF_Catalogos_Elim DEFAULT(0),
    Auditoria NVARCHAR(300) NULL);
IF OBJECT_ID('dbo.CatalogosDetalle','U') IS NULL
CREATE TABLE dbo.CatalogosDetalle(
    ID INT IDENTITY(1,1) PRIMARY KEY,
    IDCatalogo INT NOT NULL,
    Tipo NVARCHAR(40) NULL, Valor NVARCHAR(MAX) NULL, Categoria NVARCHAR(100) NULL,
    Orden INT NULL, PC NVARCHAR(100) NULL,
    Eliminado BIT NOT NULL CONSTRAINT DF_CatDet_Elim DEFAULT(0),
    Auditoria NVARCHAR(300) NULL);";

    private async Task EnsureSchemaAsync(SqlConnection cn, CancellationToken ct)
    {
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(SchemaDdl, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CatalogoDto>> ListarAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        const string sql = @"
SELECT c.ID AS Id, ISNULL(c.Nombre,'') AS Nombre, c.[Año] AS Anio, ISNULL(c.Temporada,'') AS Temporada,
       (SELECT COUNT(*) FROM dbo.CatalogosDetalle d WHERE d.IDCatalogo=c.ID AND d.Eliminado=0) AS CantItems,
       CASE WHEN c.ArchivoPDF IS NULL THEN 0 ELSE 1 END AS TienePdf
FROM dbo.Catalogos c
WHERE c.Eliminado=0
ORDER BY c.ID DESC;";
        var rows = (await cn.QueryAsync<CatalogoDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
        // El PDF puede estar como archivo físico en el server (nuevo) o en la base (viejo, del .Net).
        foreach (var r in rows)
            if (!r.TienePdf && File.Exists(PdfPath(r.Id))) r.TienePdf = true;
        return rows;
    }

    public async Task<byte[]?> ObtenerPdfAsync(int id, CancellationToken ct = default)
    {
        // 1) Archivo físico en el server (forma preferida, la base no crece).
        var path = PdfPath(id);
        if (File.Exists(path)) return await File.ReadAllBytesAsync(path, ct);

        // 2) Fallback: el PDF que el .Net dejó guardado en la base.
        using var cn = _db.Create();
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        var bytes = await cn.ExecuteScalarAsync<byte[]?>(new CommandDefinition(
            "SELECT ArchivoPDF FROM dbo.Catalogos WHERE ID=@id AND Eliminado=0;", new { id }, cancellationToken: ct));
        return bytes is { Length: > 0 } ? bytes : null;
    }

    // Datos de una tarjeta de artículo (misma consulta que ObtenerDatosDesdeSQL del .Net).
    private sealed class CartaRow
    {
        public string Codigo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string Talles { get; set; } = "";
        public string Materiales { get; set; } = "";
        public string Subfamilia { get; set; } = "";
        public string Familia { get; set; } = "";
        public string Linea { get; set; } = "";
        public string Combo { get; set; } = "";
        public decimal? Peso { get; set; }
        public decimal? Stock { get; set; }
        public decimal? Precio { get; set; }
        public string Colores { get; set; } = "";
        public byte[]? Foto { get; set; }
    }

    private sealed class FichaRow { public int Id { get; set; } public byte[]? Ficha { get; set; } }

    public async Task<byte[]?> GenerarPdfAsync(int id, CancellationToken ct = default)
    {
        var det = await DetalleAsync(id, ct);
        if (det is null) return null;

        // Datos de artículos (una sola consulta a Dragon para todos los códigos del catálogo).
        var codigos = det.Items
            .Where(i => i.Tipo != "TEXTO" && !string.IsNullOrWhiteSpace(i.Codigo))
            .Select(i => i.Codigo.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Fichas técnicas de las Órdenes de Pedido (la página ES esta imagen). El id de PedidosOrdenes
        // viaja en RefValor de los renglones OP; si no, se cae al Valor.
        var idsOP = det.Items
            .Where(i => i.Tipo.StartsWith("OP ", StringComparison.Ordinal))
            .Select(i => int.TryParse(i.RefValor, out var n) ? n : 0)
            .Where(n => n > 0).Distinct().ToList();
        var fichas = new Dictionary<int, byte[]>();
        if (idsOP.Count > 0)
        {
            using var cnf = _db.Create();
            if (cnf.State != System.Data.ConnectionState.Open) await cnf.OpenAsync(ct);
            var rows = await cnf.QueryAsync<FichaRow>(new CommandDefinition(
                "SELECT ID AS Id, FichaTecnica AS Ficha FROM dbo.PedidosOrdenes WHERE ID IN @ids;",
                new { ids = idsOP }, cancellationToken: ct));
            foreach (var r in rows) if (r.Ficha is { Length: > 0 }) fichas[r.Id] = r.Ficha;
        }

        var datos = new Dictionary<string, CartaRow>(StringComparer.OrdinalIgnoreCase);
        if (codigos.Count > 0)
        {
            using var cn = _db.Create();
            if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
            var sql = $@"
SELECT RTRIM(ART.ARTCOD) AS Codigo, LTRIM(RTRIM(ISNULL(ART.ARTDES,''))) AS Descripcion,
       ISNULL(CATE.DESCRIP,'') AS Categoria, ISNULL(CTA.DESCRIP,'') AS Talles,
       ISNULL(MAT.MATDES,'') AS Materiales, ISNULL(GRU.DESCRIP,'') AS Subfamilia,
       ISNULL(FAM.DESCRIP,'') AS Familia, ISNULL(TIPO.DESCRIP,'') AS Linea, ISNULL(ART.CLASIFART,'') AS Combo,
       ART.ARTPESO AS Peso,
       (SELECT TOP 1 COCANT FROM {DbDragon}.COMB C WHERE C.COART=ART.ARTCOD) AS Stock,
       (SELECT TOP 1 PDIRECTO FROM {DbDragon}.PRECIOAR P WHERE P.ARTICULO=ART.ARTCOD AND LISTAPRE='LISTA0' ORDER BY FALTAFW DESC, HMODIFW DESC) AS Precio,
       ISNULL(STUFF((SELECT DISTINCT ', ' + RTRIM(RC.FCOLO) FROM {DbDragon}.REMCOMPRADET RC
                     WHERE RC.FART=ART.ARTCOD AND RC.FCOTXT IS NOT NULL AND RC.FCOLO<>'' FOR XML PATH('')),1,2,''),'') AS Colores,
       CASE WHEN (SELECT TOP 1 F.FOTOIA FROM dbo.GoogleDriveFotosArticulos F WHERE F.Codigo=RTRIM(ART.ARTCOD) AND FotoIA IS NOT NULL) IS NOT NULL
            THEN (SELECT TOP 1 F.FOTOIA FROM dbo.GoogleDriveFotosArticulos F WHERE F.Codigo=RTRIM(ART.ARTCOD) AND FotoIA IS NOT NULL)
            ELSE (SELECT TOP 1 F.FotoDrive FROM dbo.GoogleDriveFotosArticulos F WHERE F.Codigo=RTRIM(ART.ARTCOD) AND FotoDrive IS NOT NULL) END AS Foto
FROM {DbDragon}.ART ART WITH(NOLOCK)
LEFT JOIN {DbDragon}.TIPOART TIPO ON TIPO.COD=ART.TIPOARTI
LEFT JOIN {DbDragon}.FAMILIA FAM ON FAM.COD=ART.FAMILIA
LEFT JOIN {DbDragon}.CATEGART CATE ON CATE.COD=ART.CATEARTI
LEFT JOIN {DbDragon}.CTALLE CTA ON CTA.CODIGO=ART.CURTALL
LEFT JOIN {DbDragon}.MAT MAT ON MAT.MATCOD=ART.MAT
LEFT JOIN {DbDragon}.GRUPO GRU ON GRU.COD=ART.GRUPO
WHERE RTRIM(ART.ARTCOD) IN @codes;";
            var rows = await cn.QueryAsync<CartaRow>(new CommandDefinition(sql, new { codes = codigos }, cancellationToken: ct));
            foreach (var r in rows) datos[r.Codigo.Trim()] = r;
        }

        static string N0(decimal? v) => v is null ? "" : v.Value.ToString("N0");

        var cartas = new List<CatalogosPdf.Carta>();
        foreach (var it in det.Items)
        {
            if (it.Tipo == "TEXTO")
            {
                cartas.Add(new CatalogosPdf.Carta { EsTexto = true, Texto = it.Descripcion });
                continue;
            }
            // Orden de Pedido: la hoja ES la ficha técnica guardada en la base.
            if (it.Tipo.StartsWith("OP ", StringComparison.Ordinal)
                && int.TryParse(it.RefValor, out var idOp) && fichas.TryGetValue(idOp, out var ficha))
            {
                cartas.Add(new CatalogosPdf.Carta { ImagenPagina = ficha });
                continue;
            }
            datos.TryGetValue((it.Codigo ?? "").Trim(), out var d);
            cartas.Add(new CatalogosPdf.Carta
            {
                Codigo = it.Codigo,
                Descripcion = d?.Descripcion is { Length: > 0 } ? d!.Descripcion : it.Descripcion,
                Categoria = d?.Categoria ?? it.Categoria,
                Talles = d?.Talles ?? "",
                Materiales = d?.Materiales ?? "",
                Subfamilia = d?.Subfamilia ?? "",
                Familia = d?.Familia ?? "",
                Linea = d?.Linea ?? "",
                Combo = d?.Combo ?? "",
                Peso = N0(d?.Peso),
                Stock = N0(d?.Stock),
                Precio = N0(d?.Precio),
                Colores = d?.Colores ?? "",
                Foto = d?.Foto
            });
        }

        var pdf = new CatalogosPdf().Construir(det.Nombre, det.Temporada, det.Anio, cartas);

        // Guardar como archivo físico en el server (no en la base).
        var path = PdfPath(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, pdf, ct);
        return pdf;
    }

    public async Task<CatalogoDetalleDto?> DetalleAsync(int id, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        const string cab = @"SELECT ID AS Id, ISNULL(Nombre,'') AS Nombre, [Año] AS Anio, ISNULL(Temporada,'') AS Temporada
                             FROM dbo.Catalogos WHERE ID=@id AND Eliminado=0;";
        var dto = await cn.QueryFirstOrDefaultAsync<CatalogoDetalleDto>(new CommandDefinition(cab, new { id }, cancellationToken: ct));
        if (dto is null) return null;

        // Reconstruye la grilla igual que CargarDetalle del .Net:
        //  TEXTO       → Valor es el texto; Código vacío.
        //  ARTÍCULO/DG → Valor es el ARTCOD; descripción desde ART.
        //  OP          → Valor es el ID de PedidosOrdenes; el ARTCOD sale de ahí, descripción desde ART.
        var det = $@"
SELECT d.Tipo AS Tipo,
       CASE WHEN d.Tipo='TEXTO' THEN ''
            WHEN d.Tipo LIKE 'OP %' THEN RTRIM(ISNULL(PO.ARTCOD,''))
            ELSE ISNULL(d.Valor,'') END AS Codigo,
       CASE WHEN d.Tipo='TEXTO' THEN ISNULL(d.Valor,'')
            ELSE LTRIM(RTRIM(ISNULL(ART.ARTDES,''))) END AS Descripcion,
       ISNULL(d.Categoria,'') AS Categoria,
       ISNULL(d.Orden,0) AS Orden,
       ISNULL(d.Valor,'') AS RefValor,
       CASE WHEN d.Tipo='TEXTO' THEN CAST(1 AS BIT)
            WHEN ART.ARTCOD IS NULL THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END AS ExisteEnDragon
FROM dbo.CatalogosDetalle d
OUTER APPLY (SELECT TOP 1 P.ARTCOD FROM dbo.PedidosOrdenes P
             WHERE d.Tipo LIKE 'OP %' AND P.ID = TRY_CONVERT(INT, d.Valor)) PO
OUTER APPLY (SELECT TOP 1 A.ARTCOD, A.ARTDES FROM {DbDragon}.ART A WITH(NOLOCK)
             WHERE RTRIM(A.ARTCOD) = CASE WHEN d.Tipo LIKE 'OP %' THEN RTRIM(ISNULL(PO.ARTCOD,''))
                                          ELSE RTRIM(ISNULL(d.Valor,'')) END) ART
WHERE d.IDCatalogo=@id AND d.Eliminado=0
ORDER BY ISNULL(d.Orden,0), d.ID;";
        var items = await cn.QueryAsync<CatalogoRenglonDto>(new CommandDefinition(det, new { id }, cancellationToken: ct));
        dto.Items = items.ToList();
        return dto;
    }

    public async Task<CatalogoCombosDto> CombosAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        var dto = new CatalogoCombosDto();
        // Igual que CargarCombos del .Net: años y temporadas realmente usados por artículos de Dragon.
        try
        {
            var anios = await cn.QueryAsync<int>(new CommandDefinition(
                $@"SELECT DISTINCT ANO FROM {DbDragon}.ART WITH(NOLOCK) WHERE ANO<>0 ORDER BY ANO DESC;",
                cancellationToken: ct));
            dto.Anios = anios.ToList();
        }
        catch { }
        try
        {
            var temps = await cn.QueryAsync<string>(new CommandDefinition(
                $@"SELECT DISTINCT LTRIM(RTRIM(TEM.TDES)) FROM {DbDragon}.ART ART WITH(NOLOCK)
                   LEFT JOIN {DbDragon}.TEMPORADA TEM WITH(NOLOCK) ON TEM.TCOD=ART.ATEMPORADA
                   WHERE ISNULL(TEM.TDES,'')<>'' ORDER BY 1;",
                cancellationToken: ct));
            dto.Temporadas = temps.ToList();
        }
        catch { }
        return dto;
    }

    public async Task<CatalogoRenglonDto?> ResolverArticuloAsync(string codigo, CancellationToken ct = default)
    {
        codigo = (codigo ?? "").Trim();
        if (codigo.Length == 0) return null;
        using var cn = _db.Create();
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        var sql = $@"
SELECT TOP 1 LTRIM(RTRIM(A.ARTCOD)) AS Codigo,
       LTRIM(RTRIM(ISNULL(A.ARTDES,''))) AS Descripcion,
       UPPER(LTRIM(RTRIM(ISNULL(CATE.DESCRIP,'')))) AS Categoria
FROM {DbDragon}.ART A WITH(NOLOCK)
LEFT JOIN {DbDragon}.CATEGART CATE WITH(NOLOCK) ON CATE.COD=A.CATEARTI
WHERE RTRIM(A.ARTCOD)=@codigo;";
        var item = await cn.QueryFirstOrDefaultAsync<CatalogoRenglonDto>(new CommandDefinition(sql, new { codigo }, cancellationToken: ct));
        if (item is null)
            return new CatalogoRenglonDto { Tipo = "ARTÍCULO", Codigo = codigo, Descripcion = "NO EXISTE EN DRAGON", Categoria = "", ExisteEnDragon = false };
        item.Tipo = "ARTÍCULO";
        item.ExisteEnDragon = true;
        return item;
    }

    public async Task<IReadOnlyList<PedidoOrdenSelDto>> PedidosOrdenesAsync(string? filtro, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        filtro = (filtro ?? "").Trim();
        var sql = $@"
SELECT TOP 500 PO.ID AS Id, PO.NroOrden AS NroOrden, RTRIM(ISNULL(PO.ARTCOD,'')) AS ARTCOD,
       LTRIM(RTRIM(COALESCE(NULLIF(RTRIM(ART.ARTDES),''), NULLIF(LTRIM(RTRIM(PO.DescripcionALT)),''), ''))) AS Descripcion,
       ISNULL(PO.Tipo,'') AS Tipo
FROM dbo.PedidosOrdenes PO
OUTER APPLY (SELECT TOP 1 A.ARTDES FROM {DbDragon}.ART A WITH(NOLOCK) WHERE RTRIM(A.ARTCOD)=RTRIM(PO.ARTCOD)) ART
WHERE ISNULL(PO.Eliminado,0)=0
  AND (@f='' OR PO.ARTCOD LIKE '%'+@f+'%' OR CONVERT(NVARCHAR(20),PO.NroOrden) LIKE '%'+@f+'%')
ORDER BY PO.NroOrden DESC, PO.ID;";
        var rows = await cn.QueryAsync<PedidoOrdenSelDto>(new CommandDefinition(sql, new { f = filtro }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CatalogoRenglonDto>> ProformaAsync(int nroProforma, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        // Igual que CrearCatalogoProformaDragon del .Net: artículos activos de esa orden → filas "DG {nro}".
        var sql = $@"
SELECT ('DG ' + CONVERT(NVARCHAR(20), PO.NroOrden)) AS Tipo,
       RTRIM(ISNULL(PO.ARTCOD,'')) AS Codigo,
       LTRIM(RTRIM(ISNULL(ART.ARTDES,''))) AS Descripcion,
       '' AS Categoria,
       CASE WHEN ART.ARTCOD IS NULL THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END AS ExisteEnDragon
FROM dbo.PedidosOrdenes PO
OUTER APPLY (SELECT TOP 1 A.ARTCOD, A.ARTDES FROM {DbDragon}.ART A WITH(NOLOCK) WHERE RTRIM(A.ARTCOD)=RTRIM(PO.ARTCOD)) ART
WHERE ISNULL(PO.Eliminado,0)=0 AND PO.NroOrden=@nro
ORDER BY PO.ID;";
        var rows = await cn.QueryAsync<CatalogoRenglonDto>(new CommandDefinition(sql, new { nro = nroProforma }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> GuardarAsync(CatalogoGuardarRequest req, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        try
        {
            int id = req.Id;
            if (id <= 0)
            {
                const string ins = @"INSERT INTO dbo.Catalogos (Nombre, [Año], Temporada, Eliminado, Auditoria)
                                     VALUES (@Nombre, @Anio, @Temporada, 0, @aud);
                                     SELECT CAST(SCOPE_IDENTITY() AS INT);";
                id = await cn.ExecuteScalarAsync<int>(new CommandDefinition(ins,
                    new { req.Nombre, req.Anio, req.Temporada, aud }, tx, cancellationToken: ct));
            }
            else
            {
                const string upd = @"UPDATE dbo.Catalogos SET Nombre=@Nombre, [Año]=@Anio, Temporada=@Temporada, Auditoria=@aud
                                     WHERE ID=@id;";
                await cn.ExecuteAsync(new CommandDefinition(upd,
                    new { req.Nombre, req.Anio, req.Temporada, aud, id }, tx, cancellationToken: ct));
                // Borrado lógico de los renglones previos (nunca DELETE) y re-inserción en orden.
                await cn.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.CatalogosDetalle SET Eliminado=1, Auditoria=@aud WHERE IDCatalogo=@id AND Eliminado=0;",
                    new { aud, id }, tx, cancellationToken: ct));
            }

            const string insDet = @"INSERT INTO dbo.CatalogosDetalle (IDCatalogo, Tipo, Valor, Categoria, Orden, PC, Eliminado, Auditoria)
                                    VALUES (@id, @Tipo, @Valor, @Categoria, @Orden, @pc, 0, @aud);";
            int orden = 0;
            var pc = Environment.MachineName;
            foreach (var it in req.Items)
            {
                orden++;
                var tipo = string.IsNullOrWhiteSpace(it.Tipo) ? "ARTÍCULO" : it.Tipo.Trim();
                // Qué se persiste en Valor, igual que el .Net:
                //  TEXTO → el texto (Descripcion) · OP → el ID de PedidosOrdenes (RefValor) · resto (ARTÍCULO/DG) → el código.
                string valor = tipo == "TEXTO" ? (it.Descripcion ?? "")
                             : tipo.StartsWith("OP ", StringComparison.Ordinal) ? (it.RefValor ?? it.Codigo ?? "")
                             : (it.Codigo ?? "");
                await cn.ExecuteAsync(new CommandDefinition(insDet,
                    new { id, Tipo = tipo, Valor = valor, Categoria = it.Categoria ?? "", Orden = orden, pc, aud },
                    tx, cancellationToken: ct));
            }

            await tx.CommitAsync(ct);
            return id;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> EliminarAsync(int id, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        // Borrado lógico (nunca DELETE): cabecera + renglones.
        var n = await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.Catalogos SET Eliminado=1, Auditoria=@aud WHERE ID=@id AND Eliminado=0;
              UPDATE dbo.CatalogosDetalle SET Eliminado=1, Auditoria=@aud WHERE IDCatalogo=@id AND Eliminado=0;",
            new { id, aud }, cancellationToken: ct));
        return n > 0;
    }
}
