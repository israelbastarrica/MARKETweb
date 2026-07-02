using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Reposicion;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Reposicion;

public interface IGruposUnificadosService
{
    Task<IReadOnlyList<GrupoUnificadoDto>> ListarAsync(CancellationToken ct = default);
    Task<GrupoUnificadoDetalleDto?> DetalleAsync(int id, CancellationToken ct = default);
    Task<GrupoArticuloDto?> ResolverArticuloAsync(string codigo, CancellationToken ct = default);
    Task<int> GuardarAsync(GrupoUnificadoGuardarRequest req, string aud, CancellationToken ct = default);
    Task<bool> EliminarAsync(int id, string aud, CancellationToken ct = default);
}

/// <summary>
/// ABM de grupos de artículos unificados para reposición (varios ARTCOD del mismo cajón que cuentan
/// su venta como uno). Solo define/edita los grupos en RepoGruposUnificados/…Det; la reposición los
/// consume aparte. Descripción desde Dragon (ART.ARTDES), CantPack desde ArticulosDatosAdiciones.
/// </summary>
public sealed class GruposUnificadosService : IGruposUnificadosService
{
    private const string DbDragon = "DRAGONFISH_CENTRAL.ZooLogic";
    private readonly ISqlConnectionFactory _db;
    public GruposUnificadosService(ISqlConnectionFactory db) => _db = db;

    private const string SchemaDdl = @"
IF OBJECT_ID('dbo.RepoGruposUnificados','U') IS NULL
CREATE TABLE dbo.RepoGruposUnificados(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(150) NULL,
    Eliminado BIT NOT NULL CONSTRAINT DF_RepoGrupUnif_Elim DEFAULT(0),
    Auditoria NVARCHAR(300) NULL);
IF OBJECT_ID('dbo.RepoGruposUnificadosDet','U') IS NULL
CREATE TABLE dbo.RepoGruposUnificadosDet(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdGrupo INT NOT NULL,
    ARTCOD NVARCHAR(50) NOT NULL,
    Eliminado BIT NOT NULL CONSTRAINT DF_RepoGrupUnifDet_Elim DEFAULT(0),
    Auditoria NVARCHAR(300) NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RepoGrupUnifDet_Grupo')
    CREATE INDEX IX_RepoGrupUnifDet_Grupo ON dbo.RepoGruposUnificadosDet(IdGrupo) WHERE Eliminado=0;";

    private async Task EnsureAsync(SqlConnection cn, CancellationToken ct)
    {
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(SchemaDdl, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<GrupoUnificadoDto>> ListarAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureAsync(cn, ct);
        const string sql = @"
SELECT g.Id, ISNULL(g.Nombre,'') AS Nombre,
       (SELECT COUNT(*) FROM dbo.RepoGruposUnificadosDet d WHERE d.IdGrupo=g.Id AND d.Eliminado=0) AS CantArticulos
FROM dbo.RepoGruposUnificados g
WHERE g.Eliminado=0
ORDER BY g.Nombre, g.Id;";
        return (await cn.QueryAsync<GrupoUnificadoDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<GrupoUnificadoDetalleDto?> DetalleAsync(int id, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureAsync(cn, ct);
        var cab = await cn.QueryFirstOrDefaultAsync<GrupoUnificadoDetalleDto>(new CommandDefinition(
            "SELECT Id, ISNULL(Nombre,'') AS Nombre FROM dbo.RepoGruposUnificados WHERE Id=@id AND Eliminado=0;",
            new { id }, cancellationToken: ct));
        if (cab is null) return null;

        var sql = $@"
SELECT RTRIM(d.ARTCOD) AS ArtCod,
       LTRIM(RTRIM(ISNULL(ART.ARTDES,''))) AS Descripcion,
       ad.CantPack AS CantPack,
       CASE WHEN ART.ARTCOD IS NULL THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END AS ExisteEnDragon
FROM dbo.RepoGruposUnificadosDet d
OUTER APPLY (SELECT TOP 1 A.ARTCOD, A.ARTDES FROM {DbDragon}.ART A WITH(NOLOCK) WHERE RTRIM(A.ARTCOD)=RTRIM(d.ARTCOD)) ART
OUTER APPLY (SELECT TOP 1 x.CantPack FROM dbo.ArticulosDatosAdiciones x WHERE RTRIM(x.ARTCOD)=RTRIM(d.ARTCOD) AND x.Eliminado=0 ORDER BY x.ID DESC) ad
WHERE d.IdGrupo=@id AND d.Eliminado=0
ORDER BY d.ARTCOD;";
        cab.Articulos = (await cn.QueryAsync<GrupoArticuloDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct))).ToList();
        return cab;
    }

    public async Task<GrupoArticuloDto?> ResolverArticuloAsync(string codigo, CancellationToken ct = default)
    {
        codigo = (codigo ?? "").Trim();
        if (codigo.Length == 0) return null;
        using var cn = _db.Create();
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync(ct);
        var sql = $@"
SELECT TOP 1 LTRIM(RTRIM(A.ARTCOD)) AS ArtCod, LTRIM(RTRIM(ISNULL(A.ARTDES,''))) AS Descripcion,
       (SELECT TOP 1 x.CantPack FROM dbo.ArticulosDatosAdiciones x WHERE RTRIM(x.ARTCOD)=RTRIM(A.ARTCOD) AND x.Eliminado=0 ORDER BY x.ID DESC) AS CantPack
FROM {DbDragon}.ART A WITH(NOLOCK) WHERE RTRIM(A.ARTCOD)=@codigo;";
        var item = await cn.QueryFirstOrDefaultAsync<GrupoArticuloDto>(new CommandDefinition(sql, new { codigo }, cancellationToken: ct));
        if (item is null) return new GrupoArticuloDto { ArtCod = codigo, Descripcion = "NO EXISTE EN DRAGON", ExisteEnDragon = false };
        item.ExisteEnDragon = true;
        return item;
    }

    public async Task<int> GuardarAsync(GrupoUnificadoGuardarRequest req, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureAsync(cn, ct);
        using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        try
        {
            int id = req.Id;
            if (id <= 0)
                id = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                    "INSERT INTO dbo.RepoGruposUnificados (Nombre, Eliminado, Auditoria) VALUES (@Nombre,0,@aud); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { req.Nombre, aud }, tx, cancellationToken: ct));
            else
            {
                await cn.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.RepoGruposUnificados SET Nombre=@Nombre, Auditoria=@aud WHERE Id=@id;",
                    new { req.Nombre, aud, id }, tx, cancellationToken: ct));
                await cn.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.RepoGruposUnificadosDet SET Eliminado=1, Auditoria=@aud WHERE IdGrupo=@id AND Eliminado=0;",
                    new { aud, id }, tx, cancellationToken: ct));
            }

            foreach (var art in req.ArtCods.Select(a => (a ?? "").Trim()).Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
                await cn.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO dbo.RepoGruposUnificadosDet (IdGrupo, ARTCOD, Eliminado, Auditoria) VALUES (@id,@art,0,@aud);",
                    new { id, art, aud }, tx, cancellationToken: ct));

            await tx.CommitAsync(ct);
            return id;
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<bool> EliminarAsync(int id, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureAsync(cn, ct);
        var n = await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.RepoGruposUnificados SET Eliminado=1, Auditoria=@aud WHERE Id=@id AND Eliminado=0;
              UPDATE dbo.RepoGruposUnificadosDet SET Eliminado=1, Auditoria=@aud WHERE IdGrupo=@id AND Eliminado=0;",
            new { id, aud }, cancellationToken: ct));
        return n > 0;
    }
}
