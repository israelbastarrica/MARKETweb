using System.Data;
using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Mapa;

namespace MarketWeb.Application.Mapa;

/// <summary>
/// Porteo de deposito-3d/backend/index.js a Dapper. Mismas queries sobre MARKET.dbo.Mapeo /
/// MapeoRegistro / Palets + DRAGONFISH_CENTRAL.ZooLogic.ART + SP_ReporteMapeo_Generar.
/// Siempre IDUbicacion=1 (el depósito CENTRAL, que es lo que modela el .glb).
/// </summary>
public sealed class MapaService : IMapaService
{
    private readonly ISqlConnectionFactory _db;
    public MapaService(ISqlConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<MapaModuloDto>> ModulosAsync(CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var rows = await cn.QueryAsync<MapaModuloDto>(new CommandDefinition(@"
            SELECT RTRIM(MAP.Modulo) AS Modulo, COUNT(REG.ID) AS CantidadArticulos
            FROM MARKET.dbo.Mapeo MAP
            LEFT JOIN MARKET.dbo.MapeoRegistro REG ON MAP.ID = REG.IDMapeo AND REG.Eliminado = 0
            WHERE MAP.Eliminado = 0 AND MAP.IDUbicacion = 1
            GROUP BY MAP.Modulo", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<MapaModuloDetalleDto> ModuloAsync(string modulo, CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var productos = (await cn.QueryAsync<MapaArticuloDto>(new CommandDefinition(@"
            SELECT REG.ARTCOD,
                   RTRIM(ART.ARTDES)   AS ARTDES,
                   RTRIM(ART.MARCA)    AS MARCA,
                   RTRIM(PAL.NroPalet) AS NroPalet
            FROM MARKET.dbo.Mapeo MAP
            INNER JOIN MARKET.dbo.MapeoRegistro REG ON MAP.ID = REG.IDMapeo
            LEFT JOIN MARKET.dbo.Palets PAL ON REG.IDPalet = PAL.id
            INNER JOIN DRAGONFISH_CENTRAL.ZooLogic.ART ON REG.ARTCOD = ART.ARTCOD
            WHERE MAP.Eliminado = 0 AND REG.Eliminado = 0 AND MAP.IDUbicacion = 1 AND MAP.Modulo = @modulo",
            new { modulo }, cancellationToken: ct))).ToList();

        return new MapaModuloDetalleDto
        {
            Modulo = modulo,
            Vacio = productos.Count == 0,
            Productos = productos
        };
    }

    public async Task<IReadOnlyList<string>> VaciosAsync(CancellationToken ct = default)
    {
        await using var cn = _db.Create();
        await cn.OpenAsync(ct);
        var rows = await cn.QueryAsync<string>(new CommandDefinition(@"
            SELECT RTRIM(M.Modulo)
            FROM MARKET.dbo.Mapeo M
            LEFT JOIN MARKET.dbo.MapeoRegistro R ON M.ID = R.IDMapeo AND R.Eliminado = 0
            WHERE M.Eliminado = 0 AND M.IDUbicacion = 1 AND R.ID IS NULL
            ORDER BY M.Pasillo, M.Fila, M.Posicion", cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<string>> BuscarAsync(string? q, CancellationToken ct = default)
    {
        q = q?.Trim();
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Array.Empty<string>();

        await using var cn = _db.Create();
        await cn.OpenAsync(ct);

        // Dos búsquedas: por descripción (contiene) y por código (prefijo), igual que el backend Node.
        var porDesc = await EjecutarBusquedaAsync(cn, descripcion: q, codArt: null, ct);
        var porCod = await EjecutarBusquedaAsync(cn, descripcion: null, codArt: q, ct);

        var modulos = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in porDesc.Concat(porCod))
        {
            var d = (IDictionary<string, object>)row;
            if (d.TryGetValue("REGID", out var rid) && rid is not null &&
                d.TryGetValue("Modulo", out var mod) && mod is not null)
                modulos.Add(mod.ToString()!.Trim());
        }
        return modulos.ToList();
    }

    private static async Task<IEnumerable<dynamic>> EjecutarBusquedaAsync(
        System.Data.Common.DbConnection cn, string? descripcion, string? codArt, CancellationToken ct)
    {
        // Los 21 parámetros de SP_ReporteMapeo_Generar con los defaults del backend Node.
        var p = new DynamicParameters();
        p.Add("FiltroUbicacion", "ESPECIFICO");
        p.Add("IDUbicacion", 1);
        p.Add("Sector", null);
        p.Add("Mobiliario", null);
        p.Add("Fila", null);
        p.Add("Posicion", null);
        p.Add("CodArt", codArt);
        p.Add("SoloVacios", 0);
        p.Add("Combo", null);
        p.Add("CalculaStock", 0);
        p.Add("Tipo", null);
        p.Add("Familia", null);
        p.Add("Material", null);
        p.Add("Temporada", null);
        p.Add("Año", 0);
        p.Add("Categoria", null);
        p.Add("CodProveedor", null);
        p.Add("Descripcion", descripcion);
        p.Add("FiltraFechaAlta", 0);
        p.Add("FiltraFechaDesde", null);
        p.Add("FiltraFechaHasta", null);
        return await cn.QueryAsync(new CommandDefinition(
            "SP_ReporteMapeo_Generar", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }
}
