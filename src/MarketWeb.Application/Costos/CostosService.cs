using System.Data;
using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Costos;
using MarketWeb.Shared.Insumos;

namespace MarketWeb.Application.Costos;

/// <summary>
/// Costos / margen (Administración). Espejo de frmRepoCostos: el cálculo vive en
/// el SP sp_ConsultaMargenVentas; acá solo lo invocamos con los filtros.
/// </summary>
public sealed class CostosService : ICostosService
{
    private readonly ISqlConnectionFactory _db;

    public CostosService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<UbicacionDto>> ListarUbicacionesAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT ID AS Id, Descripcion FROM Ubicaciones WHERE Eliminado = 0 ORDER BY Descripcion;";
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<UbicacionDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CostoMargenDto>> ListarMargenAsync(
        DateTime desde, DateTime hasta, string local, string agrupamiento, CancellationToken ct = default)
    {
        var parametros = new
        {
            FechaDesde = desde.Date,
            FechaHasta = hasta.Date.AddDays(1).AddSeconds(-1), // hasta fin del día, igual que el desktop
            Local = string.IsNullOrWhiteSpace(local) ? "TODOS" : local,
            Agrupamiento = string.IsNullOrWhiteSpace(agrupamiento) ? "DÍA" : agrupamiento
        };

        using var cn = _db.Create();
        var rows = await cn.QueryAsync<CostoMargenDto>(new CommandDefinition(
            "sp_ConsultaMargenVentas", parametros,
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return rows.ToList();
    }
}
