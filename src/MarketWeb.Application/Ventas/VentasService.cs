using System.Data;
using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Ventas;

namespace MarketWeb.Application.Ventas;

/// <summary>
/// Ventas / cobranzas (Administración). Espejo de frmRepoVentas: dos SPs.
/// (sp_ResumenVentasMensual y sp_ConsultaCobranzas leen las RÉPLICAS hoy → ~2 días
///  de atraso; pendiente pasarlas a vivo como sp_ConsultaMargenVentas.)
/// </summary>
public sealed class VentasService : IVentasService
{
    private readonly ISqlConnectionFactory _db;

    public VentasService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<VentaResumenDto>> ListarResumenAsync(
        DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var p = new
        {
            FechaDesde = desde.Date,
            FechaHasta = hasta.Date.AddDays(1).AddSeconds(-1)
        };
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<VentaResumenDto>(new CommandDefinition(
            "sp_ResumenVentasMensual", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CobranzaDto>> ListarCobranzasAsync(
        DateTime desde, DateTime hasta, string local, string agrupamiento,
        string detalle, string categoria, string medio, CancellationToken ct = default)
    {
        var p = new
        {
            FechaDesde = desde.Date,
            FechaHasta = hasta.Date.AddDays(1).AddSeconds(-1),
            Local = string.IsNullOrWhiteSpace(local) ? "TODOS" : local,
            Agrupamiento = string.IsNullOrWhiteSpace(agrupamiento) ? "DÍA" : agrupamiento,
            Detalle = string.IsNullOrWhiteSpace(detalle) ? "CATEGORIA" : detalle,
            Categoria = string.IsNullOrWhiteSpace(categoria) ? "TODOS" : categoria,
            Medio = string.IsNullOrWhiteSpace(medio) ? "TODOS" : medio
        };
        using var cn = _db.Create();
        var rows = await cn.QueryAsync<CobranzaDto>(new CommandDefinition(
            "sp_ConsultaCobranzas", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return rows.ToList();
    }
}
