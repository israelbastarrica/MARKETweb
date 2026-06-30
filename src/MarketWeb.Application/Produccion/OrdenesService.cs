using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Produccion;
using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Produccion;

public interface IOrdenesService
{
    Task<IReadOnlyList<OrdenDto>> ListarAsync(CancellationToken ct = default);
    Task<OrdenDetalleDto?> DetalleAsync(int id, CancellationToken ct = default);
    Task<ImportarOrdenesResultadoDto> ImportarMuestraAsync(string aud, CancellationToken ct = default);
    Task<ImportarOrdenesResultadoDto> ImportarOrdenAsync(int nroOrden, string aud, CancellationToken ct = default);
    Task<IReadOnlyList<ComboRangoDto>> ListarCombosAsync(CancellationToken ct = default);
    Task<OrdenCabeceraCombosDto> CombosCabeceraAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TelaColorDto>> ListarColoresTelaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OrdenColorDto>> ColoresAsync(int idRenglon, CancellationToken ct = default);
    Task GuardarColoresAsync(int idRenglon, IReadOnlyList<OrdenColorDto> colores, string aud, CancellationToken ct = default);
    Task<IReadOnlyList<OrdenProduccionCeldaDto>> ProduccionAsync(int idRenglon, CancellationToken ct = default);
    Task GuardarProduccionAsync(int idRenglon, IReadOnlyList<OrdenProduccionCeldaDto> celdas, string aud, CancellationToken ct = default);
    Task<int> GuardarCabeceraAsync(OrdenSaveRequest req, string aud, CancellationToken ct = default);
    Task<bool> GuardarRenglonAsync(OrdenRenglonSaveRequest req, string aud, CancellationToken ct = default);
    Task<bool> EliminarRenglonAsync(int id, string aud, CancellationToken ct = default);
    Task<bool> EliminarOrdenAsync(int id, string aud, CancellationToken ct = default);
}

/// <summary>
/// Módulo Diseño / Órdenes de Pedido — Fase 1 (esqueleto, sin Dragon).
/// Lee/escribe ProdOrdenes + ProdOrdenesDetalle (auto-heal idempotente).
/// Una orden = filas de PedidosOrdenes (Asana) con el mismo NroOrden; la descripción
/// sale de Dragon ART o de la planilla ProdCodigosMarket. Importa una orden de cada tipo
/// (Nacional / Importado) para pulir el ABM con datos reales antes de migrar todo.
/// </summary>
public sealed class OrdenesService : IOrdenesService
{
    private readonly ISqlConnectionFactory _db;
    public OrdenesService(ISqlConnectionFactory db) => _db = db;

    private const string SchemaDdl = @"
IF OBJECT_ID('dbo.ProdOrdenes','U') IS NULL
CREATE TABLE dbo.ProdOrdenes(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NroOrden INT NOT NULL,
    Tipo NVARCHAR(20) NOT NULL,
    Estado NVARCHAR(40) NOT NULL CONSTRAINT DF_ProdOrdenes_Estado DEFAULT('Borrador'),
    ProveedorCod NVARCHAR(50) NULL, ProveedorNombre NVARCHAR(150) NULL,
    IdViaje INT NULL, Moneda NVARCHAR(20) NULL, FechaLlegada DATE NULL, Etiquetador NVARCHAR(150) NULL,
    Temporada NVARCHAR(40) NULL, Anio INT NULL, Material NVARCHAR(50) NULL, Familia NVARCHAR(50) NULL, Subfamilia NVARCHAR(80) NULL,
    Finalizada BIT NOT NULL CONSTRAINT DF_ProdOrdenes_Final DEFAULT(0),
    Eliminado BIT NOT NULL CONSTRAINT DF_ProdOrdenes_Elim DEFAULT(0),
    Auditoria NVARCHAR(200) NULL);
IF OBJECT_ID('dbo.ProdOrdenesDetalle','U') IS NULL
CREATE TABLE dbo.ProdOrdenesDetalle(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdOrden INT NOT NULL,
    ARTCOD NVARCHAR(50) NULL, CodigoProveedor NVARCHAR(100) NULL, Descripcion NVARCHAR(300) NULL,
    ExisteEnDragon BIT NOT NULL CONSTRAINT DF_ProdOrdDet_Existe DEFAULT(0),
    TieneFicha BIT NOT NULL CONSTRAINT DF_ProdOrdDet_Ficha DEFAULT(0),
    EquiTalle NVARCHAR(150) NULL, MobiliarioDestino NVARCHAR(50) NULL,
    Cantidad INT NULL, Packs INT NULL, CostoUnit DECIMAL(18,2) NULL, PrecioVenta DECIMAL(18,2) NULL,
    Origen NVARCHAR(20) NULL, IdPedidoOrden INT NULL, IdViajeArticulo INT NULL,
    Estado NVARCHAR(40) NULL,
    Finalizada BIT NOT NULL CONSTRAINT DF_ProdOrdDet_Final DEFAULT(0),
    NroItem INT NULL,
    Eliminado BIT NOT NULL CONSTRAINT DF_ProdOrdDet_Elim DEFAULT(0),
    Auditoria NVARCHAR(200) NULL,
    CONSTRAINT FK_ProdOrdDet_Orden FOREIGN KEY (IdOrden) REFERENCES dbo.ProdOrdenes(Id));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ProdOrdDet_Orden')
    CREATE INDEX IX_ProdOrdDet_Orden ON dbo.ProdOrdenesDetalle(IdOrden) WHERE Eliminado=0;";

    // Columnas del "machete" agregadas al renglón (auto-heal idempotente).
    private const string DetalleColsDdl = @"
IF COL_LENGTH('dbo.ProdOrdenesDetalle','Corte') IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD Corte NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','Prioridad') IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD Prioridad NVARCHAR(30) NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','Talles') IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD Talles NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','Curva') IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD Curva NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','FechaEntregaTexto') IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD FechaEntregaTexto NVARCHAR(120) NULL;";

    // Colores/rollos por renglón (B) + producción color×talle estimado/real (C). Auto-heal.
    private const string ColoresProduccionDdl = @"
IF OBJECT_ID('dbo.ProdOrdenesColores','U') IS NULL
CREATE TABLE dbo.ProdOrdenesColores(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdRenglon INT NOT NULL,
    ColorCod NVARCHAR(20) NULL, ColorNombre NVARCHAR(100) NULL, Rollos INT NULL,
    Eliminado BIT NOT NULL CONSTRAINT DF_ProdOrdCol_Elim DEFAULT(0),
    Auditoria NVARCHAR(200) NULL,
    CONSTRAINT FK_ProdOrdCol_Det FOREIGN KEY (IdRenglon) REFERENCES dbo.ProdOrdenesDetalle(Id));
IF OBJECT_ID('dbo.ProdOrdenesProduccion','U') IS NULL
CREATE TABLE dbo.ProdOrdenesProduccion(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdRenglon INT NOT NULL,
    ColorCod NVARCHAR(20) NULL, Talle NVARCHAR(20) NULL, CantEstimada INT NULL, CantReal INT NULL,
    Eliminado BIT NOT NULL CONSTRAINT DF_ProdOrdProd_Elim DEFAULT(0),
    Auditoria NVARCHAR(200) NULL,
    CONSTRAINT FK_ProdOrdProd_Det FOREIGN KEY (IdRenglon) REFERENCES dbo.ProdOrdenesDetalle(Id));";

    private async Task EnsureSchemaAsync(Microsoft.Data.SqlClient.SqlConnection cn, CancellationToken ct)
    {
        await cn.ExecuteAsync(new CommandDefinition(SchemaDdl, cancellationToken: ct));
        await cn.ExecuteAsync(new CommandDefinition(DetalleColsDdl, cancellationToken: ct));
        await cn.ExecuteAsync(new CommandDefinition(ColoresProduccionDdl, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<OrdenDto>> ListarAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        const string sql = @"
SELECT O.Id, O.NroOrden, O.Tipo, O.Estado, ISNULL(O.ProveedorNombre,'') AS ProveedorNombre,
       ISNULL(O.Temporada,'') AS Temporada, O.Anio, O.FechaLlegada, O.Finalizada,
       CantRenglones = (SELECT COUNT(*) FROM dbo.ProdOrdenesDetalle D WHERE D.IdOrden=O.Id AND D.Eliminado=0)
FROM dbo.ProdOrdenes O
WHERE O.Eliminado=0
ORDER BY O.NroOrden DESC, O.Tipo;";
        return (await cn.QueryAsync<OrdenDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<OrdenDetalleDto?> DetalleAsync(int id, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        const string sqlCab = @"
SELECT Id, NroOrden, Tipo, Estado, ISNULL(ProveedorCod,'') AS ProveedorCod, ISNULL(ProveedorNombre,'') AS ProveedorNombre,
       IdViaje, ISNULL(Moneda,'') AS Moneda, FechaLlegada, ISNULL(Etiquetador,'') AS Etiquetador,
       ISNULL(Temporada,'') AS Temporada, Anio, ISNULL(Material,'') AS Material, ISNULL(Familia,'') AS Familia, ISNULL(Subfamilia,'') AS Subfamilia, Finalizada
FROM dbo.ProdOrdenes WHERE Id=@id AND Eliminado=0;";
        var cab = await cn.QuerySingleOrDefaultAsync<OrdenDetalleDto>(new CommandDefinition(sqlCab, new { id }, cancellationToken: ct));
        if (cab is null) return null;

        const string sqlDet = @"
SELECT Id, ISNULL(ARTCOD,'') AS ARTCOD, ISNULL(CodigoProveedor,'') AS CodigoProveedor, ISNULL(Descripcion,'') AS Descripcion,
       ExisteEnDragon, TieneFicha, ISNULL(EquiTalle,'') AS EquiTalle, ISNULL(MobiliarioDestino,'') AS MobiliarioDestino,
       Cantidad, Packs, CostoUnit, PrecioVenta, ISNULL(Origen,'') AS Origen, ISNULL(Estado,'') AS Estado, Finalizada, NroItem,
       ISNULL(Corte,'') AS Corte, ISNULL(Prioridad,'') AS Prioridad, ISNULL(Talles,'') AS Talles, ISNULL(Curva,'') AS Curva, ISNULL(FechaEntregaTexto,'') AS FechaEntregaTexto
FROM dbo.ProdOrdenesDetalle WHERE IdOrden=@id AND Eliminado=0 ORDER BY NroItem, Id;";
        cab.Renglones = (await cn.QueryAsync<OrdenRenglonDto>(new CommandDefinition(sqlDet, new { id }, cancellationToken: ct))).ToList();
        return cab;
    }

    // Insert de renglones desde lo viejo (PedidosOrdenes) + Dragon ART + planilla ProdCodigosMarket.
    // OUTER APPLY TOP 1: cada artículo trae UNA fila aunque la planilla mapee varios códigos de proveedor al mismo CodigoMarket.
    private const string DetInsertSql = @"
INSERT INTO dbo.ProdOrdenesDetalle
    (IdOrden, ARTCOD, CodigoProveedor, Descripcion, ExisteEnDragon, TieneFicha, EquiTalle,
     Origen, IdPedidoOrden, Estado, Finalizada, NroItem, Auditoria)
SELECT @idOrden,
       RTRIM(PO.ARTCOD),
       RTRIM(ISNULL(CM.CodigoProveedor,'')),
       LEFT(COALESCE(NULLIF(RTRIM(ART.ARTDES),''), NULLIF(RTRIM(CM.DescripcionMarket),''), NULLIF(LTRIM(RTRIM(PO.DescripcionALT)),''), 'NO EXISTE EN DRAGON'), 300),
       CASE WHEN ART.ARTCOD IS NULL THEN 0 ELSE 1 END,
       CASE WHEN PO.FichaTecnica IS NULL THEN 0 ELSE 1 END,
       ISNULL(IMG.Descripcion,''),
       'Asana', PO.ID, ISNULL(PO.Estado,''), ISNULL(PO.Finalizada,0),
       ROW_NUMBER() OVER (ORDER BY PO.ID), @aud
FROM dbo.PedidosOrdenes PO
OUTER APPLY (SELECT TOP 1 A.ARTCOD, A.ARTDES FROM DRAGONFISH_CENTRAL.Zoologic.ART A WITH(NOLOCK)
             WHERE RTRIM(A.ARTCOD)=RTRIM(PO.ARTCOD)) ART
OUTER APPLY (SELECT TOP 1 C.CodigoProveedor, C.DescripcionMarket FROM dbo.ProdCodigosMarket C
             WHERE C.Eliminado=0 AND RTRIM(C.CodigoMarket)=RTRIM(PO.ARTCOD) ORDER BY C.Id) CM
OUTER APPLY (SELECT TOP 1 I.Descripcion FROM dbo.CatalogosConfigImagenes I WHERE I.ID=PO.IDEquiTalle) IMG
WHERE PO.Eliminado=0 AND PO.NroOrden=@nro AND UPPER(LTRIM(RTRIM(PO.Tipo)))=@tipo;";

    public async Task<ImportarOrdenesResultadoDto> ImportarMuestraAsync(string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        await cn.OpenAsync(ct);
        using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        int ordenes = 0, renglones = 0;
        try
        {
            // Muestra: 1 importado + 1 nacional de juguetes (JU%) + 1 nacional de indumentaria (I% = telas). Pick = la más rica.
            var criterios = new[]
            {
                new { Tipo = "IMPORTADO", Like = (string?)null },
                new { Tipo = "NACIONAL",  Like = (string?)"JU%" },
                new { Tipo = "NACIONAL",  Like = (string?)"I%"  },
            };
            foreach (var crit in criterios)
            {
                var nro = await cn.ExecuteScalarAsync<int?>(new CommandDefinition(
                    @"SELECT TOP 1 NroOrden FROM dbo.PedidosOrdenes
                      WHERE Eliminado=0 AND UPPER(LTRIM(RTRIM(Tipo)))=@tipo AND NroOrden IS NOT NULL
                        AND (@like IS NULL OR RTRIM(ARTCOD) LIKE @like)
                      GROUP BY NroOrden ORDER BY COUNT(*) DESC, NroOrden DESC;",
                    new { tipo = crit.Tipo, like = crit.Like }, tx, cancellationToken: ct));
                if (nro is null) continue;
                renglones += await ImportarUnaAsync(cn, tx, nro.Value, crit.Tipo, aud, ct);
                ordenes++;
            }
            await tx.CommitAsync(ct);
            return new ImportarOrdenesResultadoDto { Ok = true, Ordenes = ordenes, Renglones = renglones, Mensaje = $"Importadas {ordenes} órdenes ({renglones} renglones)." };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new ImportarOrdenesResultadoDto { Ok = false, Mensaje = "Error importando: " + ex.Message };
        }
    }

    public async Task<ImportarOrdenesResultadoDto> ImportarOrdenAsync(int nroOrden, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        await cn.OpenAsync(ct);
        using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        int ordenes = 0, renglones = 0;
        try
        {
            // Tipos presentes para ese NroOrden (normalmente uno).
            var tipos = (await cn.QueryAsync<string>(new CommandDefinition(
                @"SELECT DISTINCT UPPER(LTRIM(RTRIM(Tipo))) FROM dbo.PedidosOrdenes
                  WHERE Eliminado=0 AND NroOrden=@nro AND Tipo IS NOT NULL;",
                new { nro = nroOrden }, tx, cancellationToken: ct))).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            if (tipos.Count == 0)
            {
                await tx.RollbackAsync(ct);
                return new ImportarOrdenesResultadoDto { Ok = false, Mensaje = $"No hay artículos para la orden {nroOrden} en PedidosOrdenes." };
            }
            foreach (var tipo in tipos)
            {
                renglones += await ImportarUnaAsync(cn, tx, nroOrden, tipo, aud, ct);
                ordenes++;
            }
            await tx.CommitAsync(ct);
            return new ImportarOrdenesResultadoDto { Ok = true, Ordenes = ordenes, Renglones = renglones, Mensaje = $"Orden {nroOrden} importada ({renglones} renglones)." };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new ImportarOrdenesResultadoDto { Ok = false, Mensaje = "Error importando: " + ex.Message };
        }
    }

    // Importa una (NroOrden, Tipo): baja lógica de lo previo + cabecera + renglones. Devuelve renglones insertados.
    private static async Task<int> ImportarUnaAsync(SqlConnection cn, SqlTransaction tx, int nro, string tipo, string aud, CancellationToken ct)
    {
        await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE D SET D.Eliminado=1, D.Auditoria=@aud
              FROM dbo.ProdOrdenesDetalle D JOIN dbo.ProdOrdenes O ON O.Id=D.IdOrden
              WHERE O.NroOrden=@nro AND O.Tipo=@tipo AND O.Eliminado=0;
              UPDATE dbo.ProdOrdenes SET Eliminado=1, Auditoria=@aud WHERE NroOrden=@nro AND Tipo=@tipo AND Eliminado=0;",
            new { nro, tipo, aud }, tx, cancellationToken: ct));

        var estado = await cn.ExecuteScalarAsync<string>(new CommandDefinition(
            @"SELECT TOP 1 ISNULL(NULLIF(LTRIM(RTRIM(Estado)),''),'Borrador') FROM dbo.PedidosOrdenes
              WHERE Eliminado=0 AND NroOrden=@nro AND UPPER(LTRIM(RTRIM(Tipo)))=@tipo ORDER BY Estado;",
            new { nro, tipo }, tx, cancellationToken: ct)) ?? "Borrador";

        var idOrden = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO dbo.ProdOrdenes (NroOrden, Tipo, Estado, Auditoria)
              VALUES (@nro, @tipo, @estado, @aud);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new { nro, tipo, estado, aud }, tx, cancellationToken: ct));

        return await cn.ExecuteAsync(new CommandDefinition(DetInsertSql, new { idOrden, nro, tipo, aud }, tx, cancellationToken: ct));
    }

    // Precio de venta por combo: tabla PreciosLista (PrecioDesde/PrecioHasta/Combo). El costo cae en un rango → Combo.
    public async Task<IReadOnlyList<ComboRangoDto>> ListarCombosAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        const string sql = @"
SELECT PrecioDesde AS Desde, PrecioHasta AS Hasta, RTRIM(Combo) AS Combo
FROM dbo.PreciosLista
WHERE Vigencia = 1 AND ISNULL(Eliminado,0) = 0
ORDER BY PrecioDesde;";
        return (await cn.QueryAsync<ComboRangoDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    // Combos de Dragon para la cabecera. Temporada=TEMPORADA.TDES, Material=MAT.MATDES, Familia=FAMILIA.DESCRIP, Subfamilia=GRUPO.DESCRIP.
    public async Task<OrdenCabeceraCombosDto> CombosCabeceraAsync(CancellationToken ct = default)
    {
        const string db = "DRAGONFISH_CENTRAL.ZooLogic";
        using var cn = _db.Create();
        var c = new OrdenCabeceraCombosDto();
        c.Temporadas = (await cn.QueryAsync<string>(new CommandDefinition(
            $"SELECT RTRIM(TDES) FROM {db}.TEMPORADA WHERE ISNULL(TDES,'')<>'' ORDER BY 1", cancellationToken: ct))).ToList();
        c.Materiales = (await cn.QueryAsync<string>(new CommandDefinition(
            $"SELECT RTRIM(MATDES) FROM {db}.MAT WHERE ISNULL(MATDES,'')<>'' ORDER BY 1", cancellationToken: ct))).ToList();
        c.Familias = (await cn.QueryAsync<string>(new CommandDefinition(
            $"SELECT RTRIM(DESCRIP) FROM {db}.FAMILIA WHERE ISNULL(DESCRIP,'')<>'' ORDER BY 1", cancellationToken: ct))).ToList();
        c.Subfamilias = (await cn.QueryAsync<string>(new CommandDefinition(
            $"SELECT RTRIM(DESCRIP) FROM {db}.GRUPO WHERE ISNULL(DESCRIP,'')<>'' ORDER BY 1", cancellationToken: ct))).ToList();
        return c;
    }

    // ---- Colores (B) ----
    public async Task<IReadOnlyList<TelaColorDto>> ListarColoresTelaAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        const string sql = @"SELECT RTRIM(Codigo) AS Codigo, RTRIM(Descripcion) AS Descripcion
                             FROM dbo.TelasColores WHERE ISNULL(Eliminado,0)=0 ORDER BY Codigo;";
        return (await cn.QueryAsync<TelaColorDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<OrdenColorDto>> ColoresAsync(int idRenglon, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        const string sql = @"SELECT Id, ISNULL(ColorCod,'') AS ColorCod, ISNULL(ColorNombre,'') AS ColorNombre, Rollos
                             FROM dbo.ProdOrdenesColores WHERE IdRenglon=@id AND Eliminado=0 ORDER BY Id;";
        return (await cn.QueryAsync<OrdenColorDto>(new CommandDefinition(sql, new { id = idRenglon }, cancellationToken: ct))).ToList();
    }

    public async Task GuardarColoresAsync(int idRenglon, IReadOnlyList<OrdenColorDto> colores, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        await cn.OpenAsync(ct);
        using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        try
        {
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.ProdOrdenesColores SET Eliminado=1, Auditoria=@aud WHERE IdRenglon=@id AND Eliminado=0;",
                new { id = idRenglon, aud }, tx, cancellationToken: ct));
            foreach (var c in colores.Where(x => !string.IsNullOrWhiteSpace(x.ColorCod)))
                await cn.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO dbo.ProdOrdenesColores (IdRenglon, ColorCod, ColorNombre, Rollos, Auditoria)
                      VALUES (@id, @Cod, @Nom, @Rollos, @aud);",
                    new { id = idRenglon, Cod = c.ColorCod.Trim(), Nom = NullIfBlank(c.ColorNombre), c.Rollos, aud }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    // ---- Producción color×talle (C) ----
    public async Task<IReadOnlyList<OrdenProduccionCeldaDto>> ProduccionAsync(int idRenglon, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        const string sql = @"SELECT ISNULL(ColorCod,'') AS ColorCod, ISNULL(Talle,'') AS Talle, CantEstimada AS Estimada, CantReal AS [Real]
                             FROM dbo.ProdOrdenesProduccion WHERE IdRenglon=@id AND Eliminado=0;";
        return (await cn.QueryAsync<OrdenProduccionCeldaDto>(new CommandDefinition(sql, new { id = idRenglon }, cancellationToken: ct))).ToList();
    }

    public async Task GuardarProduccionAsync(int idRenglon, IReadOnlyList<OrdenProduccionCeldaDto> celdas, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        await cn.OpenAsync(ct);
        using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        try
        {
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.ProdOrdenesProduccion SET Eliminado=1, Auditoria=@aud WHERE IdRenglon=@id AND Eliminado=0;",
                new { id = idRenglon, aud }, tx, cancellationToken: ct));
            foreach (var c in celdas.Where(x => x.Estimada.HasValue || x.Real.HasValue))
                await cn.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO dbo.ProdOrdenesProduccion (IdRenglon, ColorCod, Talle, CantEstimada, CantReal, Auditoria)
                      VALUES (@id, @Cod, @Talle, @Est, @Real, @aud);",
                    new { id = idRenglon, Cod = NullIfBlank(c.ColorCod), Talle = NullIfBlank(c.Talle), Est = c.Estimada, Real = c.Real, aud }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task<int> GuardarCabeceraAsync(OrdenSaveRequest req, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        if (req.Id == 0)
        {
            const string ins = @"
INSERT INTO dbo.ProdOrdenes (NroOrden, Tipo, Estado, ProveedorCod, ProveedorNombre, IdViaje, Moneda, FechaLlegada, Etiquetador, Temporada, Anio, Material, Familia, Subfamilia, Auditoria)
VALUES (@NroOrden, @Tipo, @Estado, @ProveedorCod, @ProveedorNombre, @IdViaje, @Moneda, @FechaLlegada, @Etiquetador, @Temporada, @Anio, @Material, @Familia, @Subfamilia, @aud);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return await cn.ExecuteScalarAsync<int>(new CommandDefinition(ins, Params(req, aud), cancellationToken: ct));
        }
        const string upd = @"
UPDATE dbo.ProdOrdenes SET NroOrden=@NroOrden, Tipo=@Tipo, Estado=@Estado, ProveedorCod=@ProveedorCod, ProveedorNombre=@ProveedorNombre,
    IdViaje=@IdViaje, Moneda=@Moneda, FechaLlegada=@FechaLlegada, Etiquetador=@Etiquetador, Temporada=@Temporada, Anio=@Anio,
    Material=@Material, Familia=@Familia, Subfamilia=@Subfamilia, Auditoria=@aud
WHERE Id=@Id AND Eliminado=0;";
        await cn.ExecuteAsync(new CommandDefinition(upd, Params(req, aud), cancellationToken: ct));
        return req.Id;
    }

    private static object Params(OrdenSaveRequest r, string aud) => new
    {
        r.Id, r.NroOrden, Tipo = (r.Tipo ?? "").Trim().ToUpperInvariant(), Estado = (r.Estado ?? "Borrador").Trim(),
        ProveedorCod = NullIfBlank(r.ProveedorCod), ProveedorNombre = NullIfBlank(r.ProveedorNombre), r.IdViaje,
        Moneda = NullIfBlank(r.Moneda), r.FechaLlegada, Etiquetador = NullIfBlank(r.Etiquetador),
        Temporada = NullIfBlank(r.Temporada), r.Anio, Material = NullIfBlank(r.Material),
        Familia = NullIfBlank(r.Familia), Subfamilia = NullIfBlank(r.Subfamilia), aud
    };

    public async Task<bool> GuardarRenglonAsync(OrdenRenglonSaveRequest req, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        const string upd = @"
UPDATE dbo.ProdOrdenesDetalle SET MobiliarioDestino=@Mob, Cantidad=@Cantidad, CostoUnit=@CostoUnit, PrecioVenta=@PrecioVenta,
    Corte=@Corte, Prioridad=@Prioridad, Talles=@Talles, Curva=@Curva, FechaEntregaTexto=@FechaEntrega, Auditoria=@aud
WHERE Id=@Id AND Eliminado=0;";
        var n = await cn.ExecuteAsync(new CommandDefinition(upd,
            new
            {
                req.Id, Mob = NullIfBlank(req.MobiliarioDestino), req.Cantidad, req.CostoUnit, req.PrecioVenta,
                Corte = NullIfBlank(req.Corte), Prioridad = NullIfBlank(req.Prioridad), Talles = NullIfBlank(req.Talles),
                Curva = NullIfBlank(req.Curva), FechaEntrega = NullIfBlank(req.FechaEntregaTexto), aud
            },
            cancellationToken: ct));
        return n > 0;
    }

    public async Task<bool> EliminarRenglonAsync(int id, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        var n = await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.ProdOrdenesDetalle SET Eliminado=1, Auditoria=@aud WHERE Id=@id AND Eliminado=0;",
            new { id, aud }, cancellationToken: ct));
        return n > 0;
    }

    public async Task<bool> EliminarOrdenAsync(int id, string aud, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await EnsureSchemaAsync(cn, ct);
        var n = await cn.ExecuteAsync(new CommandDefinition(
            @"UPDATE dbo.ProdOrdenesDetalle SET Eliminado=1, Auditoria=@aud WHERE IdOrden=@id AND Eliminado=0;
              UPDATE dbo.ProdOrdenes SET Eliminado=1, Auditoria=@aud WHERE Id=@id AND Eliminado=0;",
            new { id, aud }, cancellationToken: ct));
        return n > 0;
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
