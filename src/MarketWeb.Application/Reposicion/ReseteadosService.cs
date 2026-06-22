using System.Data;
using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Reposicion;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Reposicion;

/// <summary>
/// Porteo de frmRepoReseteados / frmABMRepoReseteado: ABM de MARKET.dbo.RepoReposicionArticulosReseteados
/// (artículos cuyo contador se reseteó, con los packs detectados al momento del reseteo). Borrado lógico.
/// </summary>
public sealed class ReseteadosService : IReseteadosService
{
    private readonly ISqlConnectionFactory _db;
    public ReseteadosService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<string>> ListarMobiliariosAsync(CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var rows = await cn.QueryAsync<string>(new CommandDefinition(
            "SELECT DISTINCT RTRIM(Mobiliario) FROM MARKET.dbo.Mapeo " +
            "WHERE Eliminado = 0 AND Mobiliario IS NOT NULL AND RTRIM(Mobiliario) <> '' ORDER BY 1",
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ReseteadoDto>> ListarAsync(string local, string mobiliario, string artCod, CancellationToken ct = default)
    {
        var filtraLocal = !string.IsNullOrWhiteSpace(local) && local != "TODOS";
        var filtraMob = !string.IsNullOrWhiteSpace(mobiliario) && mobiliario != "TODOS";
        var filtraArt = !string.IsNullOrWhiteSpace(artCod);

        var sql =
            "SELECT R.ID AS Id, R.Fecha, RTRIM(R.Local) AS Local, RTRIM(R.Mobiliario) AS Mobiliario, " +
            "       RTRIM(R.ARTCOD) AS ArtCod, ISNULL(R.PacksDetectados, 0) AS PacksDetectados, " +
            "       ISNULL(RTRIM(ART.ARTDES), '(sin descripción)') AS ArtDes " +
            "FROM MARKET.dbo.RepoReposicionArticulosReseteados R " +
            "LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD) = RTRIM(R.ARTCOD) " +
            "WHERE R.Eliminado = 0 ";
        if (filtraLocal) sql += "  AND UPPER(RTRIM(R.Local)) = UPPER(@Local) ";
        if (filtraMob) sql += "  AND UPPER(RTRIM(R.Mobiliario)) = UPPER(@Mobiliario) ";
        if (filtraArt) sql += "  AND RTRIM(R.ARTCOD) LIKE @ArtCod ";
        sql += "ORDER BY R.Fecha DESC, R.Local, R.Mobiliario, R.ARTCOD";

        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var p = new DynamicParameters();
        if (filtraLocal) p.Add("@Local", local, DbType.AnsiString, size: 20);
        if (filtraMob) p.Add("@Mobiliario", mobiliario, DbType.String, size: 60);
        if (filtraArt) p.Add("@ArtCod", "%" + artCod.Trim() + "%", DbType.AnsiString, size: 22);
        var rows = await cn.QueryAsync<ReseteadoDto>(new CommandDefinition(sql, p, commandTimeout: 60, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<ReseteadoEditorDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var ed = await cn.QuerySingleOrDefaultAsync<ReseteadoEditorDto>(new CommandDefinition(
            "SELECT R.ID AS Id, RTRIM(R.Local) AS Local, RTRIM(R.Mobiliario) AS Mobiliario, " +
            "       RTRIM(R.ARTCOD) AS ArtCod, ISNULL(R.PacksDetectados, 0) AS PacksDetectados, " +
            "       ISNULL(RTRIM(ART.ARTDES), '') AS ArtDes " +
            "FROM MARKET.dbo.RepoReposicionArticulosReseteados R " +
            "LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON RTRIM(ART.ARTCOD) = RTRIM(R.ARTCOD) " +
            "WHERE R.ID = @id AND R.Eliminado = 0", new { id }, cancellationToken: ct));
        return ed;
    }

    public async Task<int> GuardarAsync(ReseteadoSaveRequest req, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var p = new DynamicParameters();
        p.Add("@l", req.Local, DbType.AnsiString, size: 20);
        p.Add("@m", req.Mobiliario, DbType.String, size: 60);
        p.Add("@a", req.ArtCod.Trim(), DbType.AnsiString, size: 20);
        p.Add("@p", req.PacksDetectados, DbType.Int32);

        if (req.Id > 0)
        {
            p.Add("@id", req.Id, DbType.Int32);
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE MARKET.dbo.RepoReposicionArticulosReseteados " +
                "SET Local = @l, Mobiliario = @m, ARTCOD = @a, PacksDetectados = @p WHERE ID = @id",
                p, cancellationToken: ct));
            return req.Id;
        }

        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "INSERT INTO MARKET.dbo.RepoReposicionArticulosReseteados (Local, Mobiliario, ARTCOD, PacksDetectados) " +
            "VALUES (@l, @m, @a, @p); SELECT CAST(SCOPE_IDENTITY() AS INT);", p, cancellationToken: ct));
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE MARKET.dbo.RepoReposicionArticulosReseteados SET Eliminado = 1 WHERE ID = @id AND Eliminado = 0",
            new { id }, cancellationToken: ct));
    }
}
