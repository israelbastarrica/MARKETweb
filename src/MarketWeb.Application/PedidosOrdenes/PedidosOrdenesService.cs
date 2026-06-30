using System.Text.RegularExpressions;
using Dapper;
using MarketWeb.Application.Common;
using MarketWeb.Application.Data;
using MarketWeb.Shared.PedidosOrdenes;

namespace MarketWeb.Application.PedidosOrdenes;

/// <summary>
/// Órdenes de Pedido (Diseño). Replica frmABMPedidosOrdenes / frmRepoPedidosOrdenes del MARKET VB.NET.
/// La descripción y el proveedor salen de Dragonfish (ART/PROV) cruzando por ARTCOD; la equivalencia de
/// talles de CatalogosConfigImagenes. Baja lógica (Eliminado=1), consultas parametrizadas.
/// </summary>
public sealed class PedidosOrdenesService : IPedidosOrdenesService
{
    private readonly ISqlConnectionFactory _db;
    public PedidosOrdenesService(ISqlConnectionFactory db) => _db = db;

    // SELECT base del listado (igual que el reporte VB: resuelve descripción/ficha/finalizada y joins a Dragon).
    private const string SelectLista = """
        SELECT  PO.id AS Id, PO.NroOrden, PO.Tipo,
                CodArt = PO.ARTCOD,
                Descripcion = CASE WHEN ART.ARTDES IS NULL
                                   THEN CASE WHEN ISNULL(PO.DescripcionALT,'') = '' THEN 'NO EXISTE EN DRAGON' ELSE PO.DescripcionALT END
                                   ELSE RTRIM(ART.ARTDES) END,
                Ficha = CASE WHEN PO.FichaTecnica IS NULL THEN 'NO' ELSE 'SI' END,
                PO.Estado,
                Finalizada = CASE WHEN PO.Finalizada = 1 THEN 'SI' ELSE 'NO' END,
                EquiTalle = ISNULL(IMG.Descripcion,'')
        FROM    PedidosOrdenes PO
        LEFT JOIN DRAGONFISH_CENTRAL.Zoologic.ART ART ON PO.ARTCOD = ART.ARTCOD
        LEFT JOIN CatalogosConfigImagenes IMG ON IMG.ID = PO.IDEquiTalle
        WHERE   PO.Eliminado = 0
        """;

    public async Task<IReadOnlyList<PedidoOrdenListaDto>> ListarAsync(PedidoOrdenFiltro f, CancellationToken ct = default)
    {
        var sql = SelectLista;
        if (f.NroOrden is > 0) sql += " AND PO.NroOrden = @NroOrden";
        if (!string.IsNullOrWhiteSpace(f.CodArt)) sql += " AND PO.ARTCOD = @CodArt";
        if (!string.IsNullOrWhiteSpace(f.Tipo)) sql += " AND PO.Tipo = @Tipo";
        if (!string.IsNullOrWhiteSpace(f.Estado)) sql += " AND ISNULL(PO.Estado,'') = @Estado";
        if (f.Ficha == "SI") sql += " AND PO.FichaTecnica IS NOT NULL";
        else if (f.Ficha == "NO") sql += " AND PO.FichaTecnica IS NULL";
        if (f.ArtDragon == "SI") sql += " AND ART.ARTDES IS NOT NULL";
        else if (f.ArtDragon == "NO") sql += " AND ART.ARTDES IS NULL";
        if (!string.IsNullOrWhiteSpace(f.CodProveedor)) sql += " AND RTRIM(PO.ARTCOD) LIKE @ProvLike";
        if (f.Finalizada == "SI") sql += " AND PO.Finalizada = 1";
        else if (f.Finalizada == "NO") sql += " AND PO.Finalizada = 0";
        sql += " ORDER BY PO.NroOrden;";

        var p = new
        {
            f.NroOrden,
            CodArt = f.CodArt?.Trim(),
            Tipo = f.Tipo?.Trim(),
            Estado = f.Estado?.Trim(),
            ProvLike = "%" + (f.CodProveedor?.Trim() ?? "") + ".%"
        };
        using var cn = _db.Create();
        return (await cn.QueryAsync<PedidoOrdenListaDto>(new CommandDefinition(sql, p, cancellationToken: ct))).ToList();
    }

    public async Task<PedidoOrdenDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT  id AS Id, NroOrden, ARTCOD AS ArtCod, AsanaTaskID, DescripcionALT,
                    IDEquiTalle AS IdEquiTalle, Tipo,
                    TieneFicha = CASE WHEN FichaTecnica IS NULL THEN 0 ELSE 1 END
            FROM    PedidosOrdenes
            WHERE   id = @id AND Eliminado = 0;
            """;
        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<PedidoOrdenDto>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<int> CrearAsync(PedidoOrdenSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var p = Params(req);
        p.Add("aud", Auditoria("Registro agregado", usuario));
        const string sql = """
            INSERT INTO PedidosOrdenes (NroOrden, ARTCOD, FichaTecnica, Auditoria, Eliminado, Tipo, DescripcionALT, IDEquiTalle)
            OUTPUT INSERTED.id
            VALUES (@NroOrden, @ArtCod, NULL, @aud, 0, @Tipo, @DescripcionALT, @IdEquiTalle);
            """;
        using var cn = _db.Create();
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task ModificarAsync(int id, PedidoOrdenSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var p = Params(req);
        p.Add("id", id);
        p.Add("aud", Auditoria("Registro Modificado", usuario));
        const string sql = """
            UPDATE PedidosOrdenes
            SET NroOrden = @NroOrden, ARTCOD = @ArtCod, Tipo = @Tipo,
                DescripcionALT = @DescripcionALT, IDEquiTalle = @IdEquiTalle, Auditoria = @aud
            WHERE id = @id AND Eliminado = 0;
            """;
        using var cn = _db.Create();
        var n = await cn.ExecuteAsync(new CommandDefinition(sql, p, cancellationToken: ct));
        if (n == 0) throw new BusinessException("No se encontró la orden a modificar.");
    }

    public async Task EliminarAsync(int id, string usuario, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        // No se puede eliminar si la orden ya está vinculada a un catálogo (CatalogosDetalle, tipo "OP ").
        const string sqlVinc = """
            SELECT COUNT(*) FROM CatalogosDetalle
            WHERE Eliminado = 0 AND LEFT(Tipo,3) = 'OP ' AND ISNULL(Valor,'') = @poId;
            """;
        var vinc = await cn.ExecuteScalarAsync<int>(new CommandDefinition(sqlVinc, new { poId = id.ToString() }, cancellationToken: ct));
        if (vinc > 0) throw new BusinessException("No se puede eliminar: la orden está vinculada a un catálogo.");

        const string sql = "UPDATE PedidosOrdenes SET Eliminado = 1, Auditoria = @aud WHERE id = @id;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { id, aud = Auditoria("Registro Eliminado", usuario) }, cancellationToken: ct));
    }

    public async Task<ArticuloDragonDto> ResolverArticuloAsync(string artCod, CancellationToken ct = default)
    {
        var cod = (artCod ?? "").Trim();
        var res = new ArticuloDragonDto { ArtCod = cod };
        if (cod.Length == 0) return res;

        using var cn = _db.Create();
        var desc = await cn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT TOP 1 RTRIM(ARTDES) FROM DRAGONFISH_CENTRAL.Zoologic.ART WHERE ARTCOD = @cod",
            new { cod }, cancellationToken: ct));
        res.Descripcion = string.IsNullOrWhiteSpace(desc) ? null : desc;
        res.ExisteEnDragon = res.Descripcion is not null;

        // Código de proveedor = 3 dígitos antes del punto (formato NNN.XXXXX).
        var m = Regex.Match(cod, @"(\d{3})\.");
        if (m.Success)
        {
            res.CodProveedor = m.Groups[1].Value;
            res.Proveedor = await cn.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT TOP 1 RTRIM(CLNOM) FROM DRAGONFISH_CENTRAL.ZooLogic.PROV WHERE CLCOD = @cp",
                new { cp = res.CodProveedor }, cancellationToken: ct));
        }
        return res;
    }

    public async Task<IReadOnlyList<string>> EstadosAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT DISTINCT Estado FROM PedidosOrdenes WHERE Eliminado = 0 AND ISNULL(Estado,'') <> '' ORDER BY Estado;";
        using var cn = _db.Create();
        return (await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<EquivalenciaTalleDto>> EquivalenciasTallesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT ID AS Id, Descripcion FROM CatalogosConfigImagenes
            WHERE Eliminado = 0 AND Tipo = 'EQUIVALENCIAS TALLES'
            ORDER BY Descripcion;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<EquivalenciaTalleDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<ProveedorOrdenDto>> ProveedoresAsync(CancellationToken ct = default)
    {
        // Proveedores realmente usados: el código son los 3 dígitos ANTES del punto del ARTCOD
        // (formato tipo "INA019.105" -> proveedor "019"), cruzado con PROV de Dragon.
        const string sql = """
            SELECT DISTINCT
                   Codigo = SUBSTRING(PO.ARTCOD, CHARINDEX('.', PO.ARTCOD) - 3, 3),
                   Nombre = RTRIM(P.CLNOM)
            FROM PedidosOrdenes PO
            INNER JOIN DRAGONFISH_CENTRAL.ZooLogic.PROV P
                   ON P.CLCOD = SUBSTRING(PO.ARTCOD, CHARINDEX('.', PO.ARTCOD) - 3, 3)
            WHERE PO.Eliminado = 0
              AND CHARINDEX('.', PO.ARTCOD) >= 4
              AND SUBSTRING(PO.ARTCOD, CHARINDEX('.', PO.ARTCOD) - 3, 3) LIKE '[0-9][0-9][0-9]'
            ORDER BY Nombre;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<ProveedorOrdenDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    private static DynamicParameters Params(PedidoOrdenSaveRequest req)
    {
        if (req.NroOrden <= 0) throw new BusinessException("Ingresá el N° de orden.");
        if (string.IsNullOrWhiteSpace(req.ArtCod)) throw new BusinessException("Ingresá el código de artículo.");
        var p = new DynamicParameters();
        p.Add("NroOrden", req.NroOrden);
        p.Add("ArtCod", req.ArtCod.Trim());
        p.Add("Tipo", string.IsNullOrWhiteSpace(req.Tipo) ? TipoOrden.Nacional : req.Tipo.Trim());
        p.Add("DescripcionALT", string.IsNullOrWhiteSpace(req.DescripcionALT) ? null : req.DescripcionALT.Trim());
        p.Add("IdEquiTalle", req.IdEquiTalle is > 0 ? req.IdEquiTalle : null);
        return p;
    }

    private static string Auditoria(string accion, string usuario) =>
        $"{accion} | {DateTime.Now:dd/MM/yyyy HH:mm:ss} por {usuario}";
}
