using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Produccion;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Produccion;

public interface ICatalogosService
{
    Task<IReadOnlyList<CatalogoDto>> ListarAsync(CancellationToken ct = default);
    Task<CatalogoDetalleDto?> DetalleAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> TemporadasAsync(CancellationToken ct = default);
    /// <summary>Resuelve descripción + categoría de un ARTCOD desde Dragon (para agregar un renglón).</summary>
    Task<CatalogoRenglonDto?> ResolverArticuloAsync(string codigo, CancellationToken ct = default);
    Task<int> GuardarAsync(CatalogoGuardarRequest req, string aud, CancellationToken ct = default);
    Task<bool> EliminarAsync(int id, string aud, CancellationToken ct = default);
}

/// <summary>
/// Catálogos (Producción) — port fiel de frmABMCatalogo (.Net), SIN Canva.
/// Un catálogo = Nombre/Año/Temporada + lista de ítems ordenados (ARTÍCULO por código / TEXTO libre).
/// Reusa las tablas existentes Catalogos + CatalogosDetalle (mismas que usa el .Net → interoperan).
/// La descripción/categoría del artículo salen de Dragon (ART / CATEGART), igual que el .Net.
/// El PDF (las tarjetas) se genera aparte, en una segunda etapa.
/// </summary>
public sealed class CatalogosService : ICatalogosService
{
    private readonly ISqlConnectionFactory _db;
    public CatalogosService(ISqlConnectionFactory db) => _db = db;

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
        var rows = await cn.QueryAsync<CatalogoDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<CatalogoDetalleDto?> DetalleAsync(int id, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        const string cab = @"SELECT ID AS Id, ISNULL(Nombre,'') AS Nombre, [Año] AS Anio, ISNULL(Temporada,'') AS Temporada
                             FROM dbo.Catalogos WHERE ID=@id AND Eliminado=0;";
        var dto = await cn.QueryFirstOrDefaultAsync<CatalogoDetalleDto>(new CommandDefinition(cab, new { id }, cancellationToken: ct));
        if (dto is null) return null;

        // ARTÍCULO/OP/DG → descripción desde Dragon (ART.ARTDES); TEXTO → el propio Valor.
        const string det = @"
SELECT d.Tipo AS Tipo,
       ISNULL(d.Valor,'') AS Valor,
       CASE WHEN d.Tipo='TEXTO' THEN ISNULL(d.Valor,'')
            ELSE LTRIM(RTRIM(ISNULL(ART.ARTDES,''))) END AS Descripcion,
       ISNULL(d.Categoria,'') AS Categoria,
       ISNULL(d.Orden,0) AS Orden,
       CASE WHEN d.Tipo='TEXTO' THEN CAST(1 AS BIT)
            WHEN ART.ARTCOD IS NULL THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END AS ExisteEnDragon
FROM dbo.CatalogosDetalle d
OUTER APPLY (SELECT TOP 1 A.ARTCOD, A.ARTDES FROM DRAGONFISH_CENTRAL.Zoologic.ART A WITH(NOLOCK)
             WHERE RTRIM(A.ARTCOD)=RTRIM(d.Valor)) ART
WHERE d.IDCatalogo=@id AND d.Eliminado=0
ORDER BY ISNULL(d.Orden,0), d.ID;";
        var items = await cn.QueryAsync<CatalogoRenglonDto>(new CommandDefinition(det, new { id }, cancellationToken: ct));
        dto.Items = items.ToList();
        return dto;
    }

    public async Task<IReadOnlyList<string>> TemporadasAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        // Temporadas de Dragon (ATEMPORADA→TEMPORADA); si falla, lista vacía y el combo queda libre.
        try
        {
            const string sql = @"SELECT LTRIM(RTRIM(DESCRIP)) FROM DRAGONFISH_CENTRAL.Zoologic.TEMPORADA WITH(NOLOCK)
                                 WHERE ISNULL(DESCRIP,'')<>'' ORDER BY DESCRIP;";
            var t = await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct));
            return t.ToList();
        }
        catch { return new List<string>(); }
    }

    public async Task<CatalogoRenglonDto?> ResolverArticuloAsync(string codigo, CancellationToken ct = default)
    {
        codigo = (codigo ?? "").Trim();
        if (codigo.Length == 0) return null;
        using var cn = _db.Create();
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        const string sql = @"
SELECT TOP 1 LTRIM(RTRIM(A.ARTCOD)) AS Valor,
       LTRIM(RTRIM(ISNULL(A.ARTDES,''))) AS Descripcion,
       UPPER(LTRIM(RTRIM(ISNULL(CATE.DESCRIP,'')))) AS Categoria
FROM DRAGONFISH_CENTRAL.Zoologic.ART A WITH(NOLOCK)
LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.CATEGART CATE WITH(NOLOCK) ON CATE.COD=A.CATEARTI
WHERE RTRIM(A.ARTCOD)=@codigo;";
        var item = await cn.QueryFirstOrDefaultAsync<CatalogoRenglonDto>(new CommandDefinition(sql, new { codigo }, cancellationToken: ct));
        if (item is null)
            return new CatalogoRenglonDto { Tipo = "ARTÍCULO", Valor = codigo, Descripcion = "NO EXISTE EN DRAGON", Categoria = "", ExisteEnDragon = false };
        item.Tipo = "ARTÍCULO";
        item.ExisteEnDragon = true;
        return item;
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
                // TEXTO guarda el texto en Valor; ARTÍCULO guarda el código en Valor.
                var valor = tipo == "TEXTO" ? (it.Descripcion ?? "") : (it.Valor ?? "");
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
