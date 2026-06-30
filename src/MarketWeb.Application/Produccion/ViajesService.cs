using System.Globalization;
using Dapper;
using MarketWeb.Application.Data;
using MarketWeb.Shared.Produccion;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MarketWeb.Application.Produccion;

/// <summary>
/// Módulo Producción / Viajes. Lee/escribe las tablas Prod* de MARKET y, por ImportarAsync,
/// espeja la base SQLite de la app offline ViajePedidos (preservando ids con IDENTITY_INSERT,
/// upsert por IdSqlite, y copiando las fotos a la carpeta del server).
/// Rutas configurables en appsettings (sección Viajes); defaults apuntan a la carpeta de dev.
/// </summary>
public sealed class ViajesService : IViajesService
{
    private readonly ISqlConnectionFactory _db;
    private readonly IConfiguration _cfg;
    public ViajesService(ISqlConnectionFactory db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    // Default = ruta del servidor (carpeta uploads donde están el .db y las fotos). Configurable por appsettings/env.
    private const string DefaultDb = @"C:\Documentos\MARKETWeb\uploads\logistica_china.db";
    private const string DefaultOrigen = @"C:\Documentos\MARKETWeb\uploads";

    private string DbPath => _cfg["Viajes:DbPath"] ?? DefaultDb;
    private string FotosOrigen => _cfg["Viajes:FotosOrigen"] ?? DefaultOrigen;
    private string FotosDestino
    {
        get { var d = _cfg["Viajes:FotosDestino"]; return string.IsNullOrWhiteSpace(d) ? FotosOrigen : d; }
    }

    // Tabla de equivalencias código proveedor → código MARKET (Dragon). Auto-creada, idempotente.
    private const string CodigosMarketDdl = @"
IF OBJECT_ID('dbo.ProdCodigosMarket','U') IS NULL
CREATE TABLE dbo.ProdCodigosMarket(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CodigoProveedor   NVARCHAR(100) NOT NULL,   -- normalizado: sin sufijo '#', trim, upper
    CodigoMarket      NVARCHAR(50)  NULL,
    DescripcionMarket NVARCHAR(300) NULL,
    Eliminado BIT NOT NULL CONSTRAINT DF_ProdCodigosMarket_Elim DEFAULT(0),
    Auditoria NVARCHAR(200) NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdCodigosMarket_Prov')
    CREATE UNIQUE INDEX UX_ProdCodigosMarket_Prov ON dbo.ProdCodigosMarket(CodigoProveedor) WHERE Eliminado=0;";

    // Columnas de viaje agregadas desde la web (auto-creadas): maneja contenedores/CBM + moneda del viaje.
    private const string ViajeColsDdl = @"
IF COL_LENGTH('dbo.ProdViajes','ManejaContenedores') IS NULL
    ALTER TABLE dbo.ProdViajes ADD ManejaContenedores BIT NOT NULL CONSTRAINT DF_ProdViajes_ManejaCont DEFAULT(1);
IF COL_LENGTH('dbo.ProdViajes','Moneda') IS NULL
    ALTER TABLE dbo.ProdViajes ADD Moneda NVARCHAR(20) NOT NULL CONSTRAINT DF_ProdViajes_Moneda DEFAULT('RMB');
IF COL_LENGTH('dbo.ProdViajes','UnidadTransporte') IS NULL
    ALTER TABLE dbo.ProdViajes ADD UnidadTransporte NVARCHAR(20) NOT NULL CONSTRAINT DF_ProdViajes_Unidad DEFAULT('Contenedor');";

    // Proveedor scoped al viaje (los creados desde la web pertenecen a un viaje). Auto-creada.
    private const string ProveedorColDdl = @"
IF COL_LENGTH('dbo.ProdProveedores','IdViaje') IS NULL
    ALTER TABLE dbo.ProdProveedores ADD IdViaje INT NULL;";

    // Override manual del código Dragon por artículo (se carga a mano; gana sobre la planilla). Auto-creada.
    private const string ArticuloDragonColDdl = @"
IF COL_LENGTH('dbo.ProdViajeArticulos','CodigoMarketManual') IS NULL
    ALTER TABLE dbo.ProdViajeArticulos ADD CodigoMarketManual NVARCHAR(50) NULL;";

    // Normaliza A.CodigoProveedor en SQL igual que la app (corta en '#', trim, upper) para el cruce.
    private const string NormProvSql =
        "UPPER(LTRIM(RTRIM(CASE WHEN CHARINDEX('#', ISNULL(A.CodigoProveedor,'')) > 0 " +
        "THEN LEFT(A.CodigoProveedor, CHARINDEX('#', A.CodigoProveedor)-1) ELSE ISNULL(A.CodigoProveedor,'') END)))";

    // -------------------- Lecturas --------------------

    public async Task<IReadOnlyList<ViajeDto>> ListarViajesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT V.Id, V.Nombre, V.Fecha, V.Pais, V.Estado, V.FechaImportacion,
                   V.ManejaContenedores, RTRIM(ISNULL(V.Moneda,'')) AS Moneda, RTRIM(ISNULL(V.UnidadTransporte,'Contenedor')) AS UnidadTransporte,
                   (SELECT COUNT(*) FROM ProdViajeArticulos A WHERE A.IdViaje=V.Id AND A.Eliminado=0) AS CantArticulos,
                   (SELECT COUNT(DISTINCT A.IdProveedor) FROM ProdViajeArticulos A WHERE A.IdViaje=V.Id AND A.Eliminado=0 AND A.IdProveedor IS NOT NULL) AS CantProveedores,
                   (SELECT COUNT(*) FROM ProdContenedores C WHERE C.IdViaje=V.Id AND C.Eliminado=0) AS CantContenedores
            FROM ProdViajes V WHERE V.Eliminado=0
            ORDER BY V.Fecha DESC, V.Id DESC;
            """;
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(ViajeColsDdl, cancellationToken: ct));
        return (await cn.QueryAsync<ViajeDto>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<ViajeDto?> ViajeAsync(int idViaje, CancellationToken ct = default)
    {
        const string sql = """
            SELECT V.Id, V.Nombre, V.Fecha, V.Pais, V.Estado, V.FechaImportacion,
                   V.ManejaContenedores, RTRIM(ISNULL(V.Moneda,'')) AS Moneda, RTRIM(ISNULL(V.UnidadTransporte,'Contenedor')) AS UnidadTransporte,
                   (SELECT COUNT(*) FROM ProdViajeArticulos A WHERE A.IdViaje=V.Id AND A.Eliminado=0) AS CantArticulos,
                   (SELECT COUNT(DISTINCT A.IdProveedor) FROM ProdViajeArticulos A WHERE A.IdViaje=V.Id AND A.Eliminado=0 AND A.IdProveedor IS NOT NULL) AS CantProveedores,
                   (SELECT COUNT(*) FROM ProdContenedores C WHERE C.IdViaje=V.Id AND C.Eliminado=0) AS CantContenedores
            FROM ProdViajes V WHERE V.Eliminado=0 AND V.Id=@id;
            """;
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(ViajeColsDdl, cancellationToken: ct));
        return await cn.QuerySingleOrDefaultAsync<ViajeDto>(new CommandDefinition(sql, new { id = idViaje }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ViajeArticuloDto>> ArticulosAsync(int idViaje, CancellationToken ct = default)
    {
        // Costo unitario: USD FOB (sin nacionalizar) y AR$ (nacionalizado + servicios del proveedor por prenda).
        // Mismas fórmulas/defaults que la app (tasa_rmb=7.24, tasa_ars=1200).
        var sql = ("""
            SELECT A.Id, A.NumeroGeneral,
                   RTRIM(ISNULL(A.CodigoInterno,'')) AS CodigoInterno, RTRIM(ISNULL(A.CodigoProveedor,'')) AS CodigoProveedor,
                   RTRIM(ISNULL(A.Descripcion,'')) AS Descripcion, RTRIM(ISNULL(A.Tipo,'')) AS Tipo, RTRIM(ISNULL(A.Genero,'')) AS Genero,
                   RTRIM(ISNULL(P.Nombre,'')) AS Proveedor, RTRIM(ISNULL(C.NombreContenedor,'')) AS Contenedor,
                   RTRIM(ISNULL(A.Talles,'')) AS Talles, RTRIM(ISNULL(A.Colores,'')) AS Colores,
                   A.CajasPedidas, A.CantidadTotalPrendas, A.CbmCaja,
                   CAST( COALESCE(A.PrecioYuanes,0) / COALESCE(NULLIF(A.TasaRmb,0),7.24) * (1 - COALESCE(A.PDesc,0)/100.0) AS DECIMAL(18,4)) AS CostoUsdUnit,
                   CAST( COALESCE(A.PrecioYuanes,0) / COALESCE(NULLIF(A.TasaRmb,0),7.24) * (1 - COALESCE(A.PDesc,0)/100.0)
                              * (1 + COALESCE(A.PNac,0)/100.0) * COALESCE(NULLIF(A.TasaArs,0),1200)
                         + COALESCE((SELECT SUM(SP.ImporteYuan) FROM ProdCostoServicioProveedor SP WHERE SP.IdProveedor=A.IdProveedor AND SP.Eliminado=0),0)
                              / COALESCE(NULLIF(A.TasaRmb,0),7.24) * (1 + COALESCE(A.PNac,0)/100.0) * COALESCE(NULLIF(A.TasaArs,0),1200)
                         AS DECIMAL(18,4)) AS CostoArsUnit,
                   RTRIM(COALESCE(NULLIF(A.CodigoMarketManual,''), CM.CodigoMarket, '')) AS CodigoMarket,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM DRAGONFISH_CENTRAL.Zoologic.ART D WITH(NOLOCK)
                        WHERE RTRIM(D.ARTCOD) = RTRIM(COALESCE(NULLIF(A.CodigoMarketManual,''), CM.CodigoMarket, ''))
                          AND RTRIM(COALESCE(NULLIF(A.CodigoMarketManual,''), CM.CodigoMarket, '')) <> '') THEN 1 ELSE 0 END AS BIT) AS ExisteEnDragon,
                   ISNULL((SELECT TOP 1 F.Archivo FROM ProdViajeArticuloFotos F WHERE F.IdArticulo=A.Id AND F.Eliminado=0
                           ORDER BY F.EsPrincipal DESC, F.Id), A.Foto) AS FotoPrincipal
            FROM ProdViajeArticulos A
            LEFT JOIN ProdProveedores  P ON P.Id = A.IdProveedor
            LEFT JOIN ProdContenedores C ON C.Id = A.IdContenedor
            LEFT JOIN ProdCodigosMarket CM ON CM.Eliminado=0 AND CM.CodigoProveedor = /**NORM**/
            WHERE A.IdViaje=@id AND A.Eliminado=0
            ORDER BY P.Nombre, A.NumeroGeneral, A.CodigoInterno;
            """).Replace("/**NORM**/", NormProvSql);
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(CodigosMarketDdl, cancellationToken: ct));
        await cn.ExecuteAsync(new CommandDefinition(ArticuloDragonColDdl, cancellationToken: ct));
        return (await cn.QueryAsync<ViajeArticuloDto>(new CommandDefinition(sql, new { id = idViaje }, commandTimeout: 90, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<ViajeProveedorDto>> ProveedoresAsync(int idViaje, CancellationToken ct = default)
    {
        const string sql = """
            SELECT P.Id, RTRIM(ISNULL(P.Codigo,'')) AS Codigo, RTRIM(ISNULL(P.Nombre,'')) AS Nombre,
                   RTRIM(ISNULL(P.Ciudad,'')) AS Ciudad, RTRIM(ISNULL(P.Pais,'')) AS Pais,
                   RTRIM(ISNULL(P.Celular,'')) AS Celular, RTRIM(ISNULL(P.Email,'')) AS Email, P.DiasEntrega,
                   ISNULL((SELECT TOP 1 F.Archivo FROM ProdProveedorFotos F WHERE F.IdProveedor=P.Id AND F.Eliminado=0
                           ORDER BY F.EsPrincipal DESC, F.Id), P.Foto) AS FotoPrincipal,
                   (SELECT COUNT(*) FROM ProdViajeArticulos A WHERE A.IdProveedor=P.Id AND A.IdViaje=@id AND A.Eliminado=0) AS CantArticulos
            FROM ProdProveedores P
            WHERE P.Eliminado=0
              AND (P.IdViaje = @id
                   OR EXISTS (SELECT 1 FROM ProdViajeArticulos A WHERE A.IdProveedor=P.Id AND A.IdViaje=@id AND A.Eliminado=0))
            ORDER BY P.Nombre;
            """;
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(ProveedorColDdl, cancellationToken: ct));
        return (await cn.QueryAsync<ViajeProveedorDto>(new CommandDefinition(sql, new { id = idViaje }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<ViajeContenedorDto>> ContenedoresAsync(int idViaje, CancellationToken ct = default)
    {
        const string sql = """
            SELECT C.Id, RTRIM(ISNULL(C.NombreContenedor,'')) AS NombreContenedor, RTRIM(ISNULL(C.Tipo,'')) AS Tipo, C.CapacidadMaxCbm,
                   (SELECT COUNT(*) FROM ProdViajeArticulos A WHERE A.IdContenedor=C.Id AND A.Eliminado=0) AS CantArticulos,
                   ISNULL((SELECT SUM(ISNULL(A.CbmCaja,0)*ISNULL(A.CajasPedidas,0)) FROM ProdViajeArticulos A WHERE A.IdContenedor=C.Id AND A.Eliminado=0),0) AS CbmUsado
            FROM ProdContenedores C
            WHERE C.IdViaje=@id AND C.Eliminado=0
            ORDER BY C.NombreContenedor;
            """;
        using var cn = _db.Create();
        return (await cn.QueryAsync<ViajeContenedorDto>(new CommandDefinition(sql, new { id = idViaje }, cancellationToken: ct))).ToList();
    }

    public async Task<ViajeArticuloFichaDto?> FichaAsync(int idArticulo, CancellationToken ct = default)
    {
        var sql = ("""
            SELECT A.Id, A.IdViaje, A.IdProveedor, A.NumeroGeneral,
                   RTRIM(ISNULL(A.CodigoInterno,'')) AS CodigoInterno, RTRIM(ISNULL(A.CodigoProveedor,'')) AS CodigoProveedor,
                   RTRIM(ISNULL(A.Descripcion,'')) AS Descripcion, RTRIM(ISNULL(P.Nombre,'')) AS Proveedor,
                   RTRIM(ISNULL(A.Genero,'')) AS Genero, RTRIM(ISNULL(A.Tipo,'')) AS Tipo, RTRIM(ISNULL(A.Material,'')) AS Material,
                   RTRIM(ISNULL(C.NombreContenedor,'')) AS Contenedor,
                   RTRIM(ISNULL(A.Talles,'')) AS Talles, RTRIM(ISNULL(A.CurvaTalles,'')) AS CurvaTalles, ISNULL(A.TablaTalles,'') AS TablaTalles,
                   ISNULL(A.Colores,'') AS Colores, ISNULL(A.ColoresProveedor,'') AS ColoresProveedor,
                   A.PrecioYuanes, A.PDesc, A.PNac, A.TasaRmb, A.TasaArs, RTRIM(ISNULL(A.TipoDolar,'')) AS TipoDolar,
                   RTRIM(ISNULL(A.ComboGuardado,'')) AS ComboGuardado,
                   A.CbmUnitario, A.CbmCaja, A.CajasPedidas, A.PacksPorCaja, A.CantidadTotalPrendas, RTRIM(ISNULL(A.TipoBulto,'')) AS TipoBulto,
                   A.DiasEntrega, ISNULL(A.Observaciones,'') AS Observaciones, ISNULL(A.PacksArmados,'') AS PacksArmados,
                   RTRIM(COALESCE(NULLIF(A.CodigoMarketManual,''), CM.CodigoMarket, '')) AS CodigoMarket,
                   RTRIM(ISNULL(A.CodigoMarketManual,'')) AS CodigoMarketManual,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM DRAGONFISH_CENTRAL.Zoologic.ART D WITH(NOLOCK)
                        WHERE RTRIM(D.ARTCOD) = RTRIM(COALESCE(NULLIF(A.CodigoMarketManual,''), CM.CodigoMarket, ''))
                          AND RTRIM(COALESCE(NULLIF(A.CodigoMarketManual,''), CM.CodigoMarket, '')) <> '') THEN 1 ELSE 0 END AS BIT) AS ExisteEnDragon,
                   RTRIM(ISNULL(CM.DescripcionMarket,'')) AS DescripcionMarket
            FROM ProdViajeArticulos A
            LEFT JOIN ProdProveedores  P ON P.Id = A.IdProveedor
            LEFT JOIN ProdContenedores C ON C.Id = A.IdContenedor
            LEFT JOIN ProdCodigosMarket CM ON CM.Eliminado=0 AND CM.CodigoProveedor = /**NORM**/
            WHERE A.Id=@id AND A.Eliminado=0;
            """).Replace("/**NORM**/", NormProvSql);
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(CodigosMarketDdl, cancellationToken: ct));
        await cn.ExecuteAsync(new CommandDefinition(ArticuloDragonColDdl, cancellationToken: ct));
        var ficha = await cn.QuerySingleOrDefaultAsync<ViajeArticuloFichaDto>(new CommandDefinition(sql, new { id = idArticulo }, cancellationToken: ct));
        if (ficha is null) return null;

        const string sqlFotos = """
            SELECT RTRIM(ISNULL(Archivo,'')) AS Archivo, EsPrincipal
            FROM ProdViajeArticuloFotos WHERE IdArticulo=@id AND Eliminado=0
            ORDER BY EsPrincipal DESC, Id;
            """;
        ficha.Fotos = (await cn.QueryAsync<ViajeFotoDto>(new CommandDefinition(sqlFotos, new { id = idArticulo }, cancellationToken: ct))).ToList();
        // Si no hay fotos en la tabla hija pero el artículo tiene Foto suelta, la usamos como principal.
        if (ficha.Fotos.Count == 0)
        {
            using var cn2 = _db.Create();
            var foto = await cn2.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT RTRIM(ISNULL(Foto,'')) FROM ProdViajeArticulos WHERE Id=@id", new { id = idArticulo }, cancellationToken: ct));
            if (!string.IsNullOrWhiteSpace(foto)) ficha.Fotos.Add(new ViajeFotoDto { Archivo = foto!, EsPrincipal = true });
        }
        return ficha;
    }

    public async Task<int> ContarCodigosMarketAsync(CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(CodigosMarketDdl, cancellationToken: ct));
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.ProdCodigosMarket WHERE Eliminado=0", cancellationToken: ct));
    }

    public async Task<ImportarCodigosResultadoDto> ImportarCodigosMarketAsync(string texto, string usuario, CancellationToken ct = default)
    {
        var aud = $"Equiv {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}";
        var filas = new List<(string Prov, string Market, string Desc)>();
        foreach (var raw in (texto ?? "").Replace("\r", "").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var parts = raw.Split('\t');
            if (parts.Length < 2) parts = raw.Split(';');   // fallback si pegan con ';'
            var prov = NormProv(parts.ElementAtOrDefault(0) ?? "");
            var market = (parts.ElementAtOrDefault(1) ?? "").Trim();
            var desc = (parts.ElementAtOrDefault(2) ?? "").Trim();
            if (prov.Length == 0 || market.Length == 0) continue;
            filas.Add((prov, market, desc));
        }

        using var cn = _db.Create();
        await cn.OpenAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(CodigosMarketDdl, cancellationToken: ct));

        const string upsert = @"
UPDATE dbo.ProdCodigosMarket SET CodigoMarket=@m, DescripcionMarket=NULLIF(@d,''), Eliminado=0, Auditoria=@a WHERE CodigoProveedor=@p;
IF @@ROWCOUNT=0 INSERT INTO dbo.ProdCodigosMarket(CodigoProveedor,CodigoMarket,DescripcionMarket,Eliminado,Auditoria) VALUES(@p,@m,NULLIF(@d,''),0,@a);";
        int n = 0;
        foreach (var f in filas)
        {
            await cn.ExecuteAsync(new CommandDefinition(upsert, new { p = f.Prov, m = f.Market, d = f.Desc, a = aud }, cancellationToken: ct));
            n++;
        }
        var total = await cn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM dbo.ProdCodigosMarket WHERE Eliminado=0", cancellationToken: ct));
        return new ImportarCodigosResultadoDto { Ok = true, Procesados = n, Total = total, Mensaje = $"{n} equivalencias cargadas. Total en MARKET: {total}." };
    }

    private static string NormProv(string s) => (s ?? "").Split('#')[0].Trim().ToUpperInvariant();

    public async Task GuardarCodigoDragonAsync(int idArticulo, string codigo, string usuario, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(ArticuloDragonColDdl, cancellationToken: ct));
        var aud = $"Cód.Dragon {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}";
        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.ProdViajeArticulos SET CodigoMarketManual = NULLIF(@c,''), Auditoria=@a WHERE Id=@id",
            new { c = (codigo ?? "").Trim(), a = aud, id = idArticulo }, cancellationToken: ct));
    }

    // ==================== ABM (alta/baja/modificación desde la web) ====================

    public async Task<int> GuardarViajeAsync(ViajeSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var aud = $"ABM viaje {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}";
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(ViajeColsDdl, cancellationToken: ct));
        var datos = new
        {
            req.Id, Nombre = (req.Nombre ?? "").Trim(), req.Fecha, Pais = (req.Pais ?? "").Trim(),
            Estado = string.IsNullOrWhiteSpace(req.Estado) ? "ABIERTO" : req.Estado.Trim(),
            ManejaContenedores = true,   // el CBM/unidad de carga siempre se maneja; cambia solo el nombre (Contenedor/Camión)
            Moneda = string.IsNullOrWhiteSpace(req.Moneda) ? "USD" : req.Moneda.Trim(),
            UnidadTransporte = string.IsNullOrWhiteSpace(req.UnidadTransporte) ? "Contenedor" : req.UnidadTransporte.Trim(), Aud = aud
        };
        if (req.Id == 0)
        {
            const string ins = @"
                INSERT INTO ProdViajes (Nombre, Fecha, Pais, Estado, ManejaContenedores, Moneda, UnidadTransporte, Eliminado, Auditoria)
                VALUES (@Nombre, @Fecha, @Pais, @Estado, @ManejaContenedores, @Moneda, @UnidadTransporte, 0, @Aud);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return await cn.ExecuteScalarAsync<int>(new CommandDefinition(ins, datos, cancellationToken: ct));
        }
        const string upd = @"
            UPDATE ProdViajes SET Nombre=@Nombre, Fecha=@Fecha, Pais=@Pais, Estado=@Estado,
                   ManejaContenedores=@ManejaContenedores, Moneda=@Moneda, UnidadTransporte=@UnidadTransporte, Auditoria=@Aud
            WHERE Id=@Id;";
        await cn.ExecuteAsync(new CommandDefinition(upd, datos, cancellationToken: ct));
        return req.Id;
    }

    public async Task EliminarViajeAsync(int id, string usuario, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE ProdViajes SET Eliminado=1, Auditoria=@a WHERE Id=@id",
            new { id, a = $"Baja {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}" }, cancellationToken: ct));
    }

    public async Task<ContenedorEditorDto?> ObtenerContenedorAsync(int id, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<ContenedorEditorDto>(new CommandDefinition(
            "SELECT Id, IdViaje, RTRIM(ISNULL(NombreContenedor,'')) AS NombreContenedor, RTRIM(ISNULL(Tipo,'')) AS Tipo, CapacidadMaxCbm " +
            "FROM ProdContenedores WHERE Id=@id AND Eliminado=0", new { id }, cancellationToken: ct));
    }

    public async Task GuardarContenedorAsync(ContenedorSaveRequest req, string usuario, CancellationToken ct = default)
    {
        var aud = $"ABM contenedor {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}";
        var p = new { req.Id, req.IdViaje, Nombre = (req.NombreContenedor ?? "").Trim(), Tipo = (req.Tipo ?? "").Trim(), req.CapacidadMaxCbm, Aud = aud };
        using var cn = _db.Create();
        if (req.Id == 0)
            await cn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO ProdContenedores (IdViaje, NombreContenedor, Tipo, CapacidadMaxCbm, Eliminado, Auditoria) VALUES (@IdViaje,@Nombre,@Tipo,@CapacidadMaxCbm,0,@Aud)", p, cancellationToken: ct));
        else
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE ProdContenedores SET NombreContenedor=@Nombre, Tipo=@Tipo, CapacidadMaxCbm=@CapacidadMaxCbm, Auditoria=@Aud WHERE Id=@Id", p, cancellationToken: ct));
    }

    public async Task EliminarContenedorAsync(int id, string usuario, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE ProdContenedores SET Eliminado=1, Auditoria=@a WHERE Id=@id",
            new { id, a = $"Baja {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}" }, cancellationToken: ct));
    }

    public async Task<ProveedorEditorDto?> ObtenerProveedorAsync(int id, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(ProveedorColDdl, cancellationToken: ct));
        return await cn.QuerySingleOrDefaultAsync<ProveedorEditorDto>(new CommandDefinition(
            "SELECT Id, ISNULL(IdViaje,0) AS IdViaje, RTRIM(ISNULL(Nombre,'')) AS Nombre, RTRIM(ISNULL(Codigo,'')) AS Codigo, RTRIM(ISNULL(Ciudad,'')) AS Ciudad, " +
            "RTRIM(ISNULL(Pais,'')) AS Pais, RTRIM(ISNULL(Celular,'')) AS Celular, RTRIM(ISNULL(Email,'')) AS Email, " +
            "RTRIM(ISNULL(Broker,'')) AS Broker, DiasEntrega, ISNULL(Observaciones,'') AS Observaciones " +
            "FROM ProdProveedores WHERE Id=@id AND Eliminado=0", new { id }, cancellationToken: ct));
    }

    public async Task GuardarProveedorAsync(ProveedorEditorDto req, string usuario, CancellationToken ct = default)
    {
        var aud = $"ABM proveedor {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}";
        var p = new
        {
            req.Id, IdViaje = req.IdViaje == 0 ? (int?)null : req.IdViaje,
            Nombre = (req.Nombre ?? "").Trim(), Codigo = (req.Codigo ?? "").Trim(), Ciudad = (req.Ciudad ?? "").Trim(),
            Pais = (req.Pais ?? "").Trim(), Celular = (req.Celular ?? "").Trim(), Email = (req.Email ?? "").Trim(),
            Broker = (req.Broker ?? "").Trim(), req.DiasEntrega, Observaciones = (req.Observaciones ?? "").Trim(), Aud = aud
        };
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(ProveedorColDdl, cancellationToken: ct));
        if (req.Id == 0)
            await cn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO ProdProveedores (IdViaje, Nombre, Codigo, Ciudad, Pais, Celular, Email, Broker, DiasEntrega, Observaciones, Eliminado, Auditoria) " +
                "VALUES (@IdViaje,@Nombre,@Codigo,@Ciudad,@Pais,@Celular,@Email,@Broker,@DiasEntrega,@Observaciones,0,@Aud)", p, cancellationToken: ct));
        else
            await cn.ExecuteAsync(new CommandDefinition(
                "UPDATE ProdProveedores SET Nombre=@Nombre, Codigo=@Codigo, Ciudad=@Ciudad, Pais=@Pais, Celular=@Celular, Email=@Email, " +
                "Broker=@Broker, DiasEntrega=@DiasEntrega, Observaciones=@Observaciones, Auditoria=@Aud WHERE Id=@Id", p, cancellationToken: ct));
    }

    public async Task EliminarProveedorAsync(int id, string usuario, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE ProdProveedores SET Eliminado=1, Auditoria=@a WHERE Id=@id",
            new { id, a = $"Baja {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}" }, cancellationToken: ct));
    }

    public async Task<ArticuloEditorDto?> ObtenerArticuloEditorAsync(int id, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        return await cn.QuerySingleOrDefaultAsync<ArticuloEditorDto>(new CommandDefinition(@"
            SELECT Id, IdViaje, IdProveedor, IdContenedor,
                   RTRIM(ISNULL(CodigoInterno,'')) AS CodigoInterno, RTRIM(ISNULL(CodigoProveedor,'')) AS CodigoProveedor, NumeroGeneral,
                   RTRIM(ISNULL(Descripcion,'')) AS Descripcion, RTRIM(ISNULL(Genero,'')) AS Genero, RTRIM(ISNULL(Tipo,'')) AS Tipo, RTRIM(ISNULL(Material,'')) AS Material,
                   RTRIM(ISNULL(Talles,'')) AS Talles, ISNULL(Colores,'') AS Colores,
                   PrendasPorPack, PacksPorCaja, CajasPedidas, CantidadTotalPrendas, CbmCaja,
                   PrecioYuanes, PDesc, PNac, TasaRmb, TasaArs, RTRIM(ISNULL(TipoDolar,'')) AS TipoDolar,
                   MoqUnidades, MoqColores, DiasEntrega, ISNULL(Observaciones,'') AS Observaciones
            FROM ProdViajeArticulos WHERE Id=@id AND Eliminado=0", new { id }, cancellationToken: ct));
    }

    public async Task<int> GuardarArticuloAsync(ArticuloEditorDto req, string usuario, CancellationToken ct = default)
    {
        var aud = $"ABM artículo {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}";
        var p = new
        {
            req.Id, req.IdViaje,
            IdProveedor = req.IdProveedor == 0 ? null : req.IdProveedor,
            IdContenedor = req.IdContenedor == 0 ? null : req.IdContenedor,
            CodigoInterno = (req.CodigoInterno ?? "").Trim(), CodigoProveedor = (req.CodigoProveedor ?? "").Trim(), req.NumeroGeneral,
            Descripcion = (req.Descripcion ?? "").Trim(), Genero = (req.Genero ?? "").Trim(), Tipo = (req.Tipo ?? "").Trim(), Material = (req.Material ?? "").Trim(),
            Talles = (req.Talles ?? "").Trim(), Colores = (req.Colores ?? "").Trim(),
            req.PrendasPorPack, req.PacksPorCaja, req.CajasPedidas, req.CantidadTotalPrendas, req.CbmCaja,
            req.PrecioYuanes, req.PDesc, req.PNac, req.TasaRmb, req.TasaArs, TipoDolar = (req.TipoDolar ?? "").Trim(),
            req.MoqUnidades, req.MoqColores, req.DiasEntrega, Observaciones = (req.Observaciones ?? "").Trim(), Aud = aud
        };
        using var cn = _db.Create();
        if (req.Id == 0)
        {
            const string ins = @"
                INSERT INTO ProdViajeArticulos (IdViaje, IdProveedor, IdContenedor, CodigoInterno, CodigoProveedor, NumeroGeneral,
                    Descripcion, Genero, Tipo, Material, Talles, Colores, PrendasPorPack, PacksPorCaja, CajasPedidas, CantidadTotalPrendas,
                    CbmCaja, PrecioYuanes, PDesc, PNac, TasaRmb, TasaArs, TipoDolar, MoqUnidades, MoqColores, DiasEntrega, Observaciones, Eliminado, Auditoria)
                VALUES (@IdViaje,@IdProveedor,@IdContenedor,@CodigoInterno,@CodigoProveedor,@NumeroGeneral,
                    @Descripcion,@Genero,@Tipo,@Material,@Talles,@Colores,@PrendasPorPack,@PacksPorCaja,@CajasPedidas,@CantidadTotalPrendas,
                    @CbmCaja,@PrecioYuanes,@PDesc,@PNac,@TasaRmb,@TasaArs,@TipoDolar,@MoqUnidades,@MoqColores,@DiasEntrega,@Observaciones,0,@Aud);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return await cn.ExecuteScalarAsync<int>(new CommandDefinition(ins, p, cancellationToken: ct));
        }
        const string upd = @"
            UPDATE ProdViajeArticulos SET IdProveedor=@IdProveedor, IdContenedor=@IdContenedor, CodigoInterno=@CodigoInterno,
                CodigoProveedor=@CodigoProveedor, NumeroGeneral=@NumeroGeneral, Descripcion=@Descripcion, Genero=@Genero, Tipo=@Tipo, Material=@Material,
                Talles=@Talles, Colores=@Colores, PrendasPorPack=@PrendasPorPack, PacksPorCaja=@PacksPorCaja, CajasPedidas=@CajasPedidas,
                CantidadTotalPrendas=@CantidadTotalPrendas, CbmCaja=@CbmCaja, PrecioYuanes=@PrecioYuanes, PDesc=@PDesc, PNac=@PNac,
                TasaRmb=@TasaRmb, TasaArs=@TasaArs, TipoDolar=@TipoDolar, MoqUnidades=@MoqUnidades, MoqColores=@MoqColores,
                DiasEntrega=@DiasEntrega, Observaciones=@Observaciones, Auditoria=@Aud
            WHERE Id=@Id;";
        await cn.ExecuteAsync(new CommandDefinition(upd, p, cancellationToken: ct));
        return req.Id;
    }

    public async Task EliminarArticuloAsync(int id, string usuario, CancellationToken ct = default)
    {
        using var cn = _db.Create();
        await cn.ExecuteAsync(new CommandDefinition(
            "UPDATE ProdViajeArticulos SET Eliminado=1, Auditoria=@a WHERE Id=@id",
            new { id, a = $"Baja {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm}" }, cancellationToken: ct));
    }

    public string? FotoFullPath(string archivo)
    {
        var safe = Path.GetFileName(archivo ?? "");
        if (string.IsNullOrWhiteSpace(safe)) return null;
        // Busca el archivo en la carpeta configurada y en las subcarpetas típicas de la app
        // (puede haber quedado en uploads/ o static/uploads/ según cómo se copió al server).
        foreach (var baseDir in new[] { FotosDestino, FotosOrigen })
        {
            if (string.IsNullOrWhiteSpace(baseDir)) continue;
            foreach (var sub in new[] { "", "uploads", Path.Combine("static", "uploads"), "static" })
            {
                var full = Path.Combine(baseDir, sub, safe);
                if (File.Exists(full)) return full;
            }
        }
        return null;
    }

    // -------------------- Import (espejo del .db) --------------------

    // (M)arket col, (S)qlite col, (T)ipo: s=string, i=int, d=decimal, b=bit, t=datetime
    private static readonly (string M, string S, char T)[] ColsProveedor =
    {
        ("Nombre","nombre",'s'),("Celular","celular",'s'),("Email","email",'s'),("Ciudad","ciudad",'s'),
        ("Pais","pais",'s'),("Codigo","codigo",'s'),("Broker","broker",'s'),("Observaciones","observaciones",'s'),
        ("DiasEntrega","dias_entrega",'i'),("Foto","foto",'s'),("ImagenTablaTalles","imagen_tabla_talles",'s'),
        ("ImagenTablaColores","imagen_tabla_colores",'s')
    };
    private static readonly (string M, string S, char T)[] ColsContenedor =
    {
        ("IdViaje","id_viaje",'i'),("NombreContenedor","nombre_contenedor",'s'),("Tipo","tipo",'s'),("CapacidadMaxCbm","capacidad_max_cbm",'d')
    };
    private static readonly (string M, string S, char T)[] ColsArticulo =
    {
        ("IdViaje","id_viaje",'i'),("IdProveedor","id_proveedor",'i'),("IdContenedor","id_contenedor",'i'),
        ("CodigoInterno","codigo_interno",'s'),("CodigoProveedor","codigo_proveedor",'s'),("NumeroGeneral","numero_general",'i'),
        ("Descripcion","descripcion",'s'),("Tipo","tipo",'s'),("Genero","genero",'s'),("Material","material",'s'),
        ("Talles","talles",'s'),("CurvaTalles","curva_talles",'s'),("TablaTalles","tabla_talles",'s'),
        ("Colores","colores",'s'),("CurvaColores","curva_colores",'s'),("ColoresProveedor","colores_proveedor",'s'),
        ("PrendasPorPack","prendas_por_pack",'i'),("PrendasPackBase","prendas_pack_base",'i'),("PacksPorCaja","packs_por_caja",'i'),
        ("CajasPedidas","cajas_pedidas",'i'),("CantidadTotalPrendas","cantidad_total_prendas",'i'),("TipoBulto","tipo_bulto",'s'),
        ("PacksArmados","packs_armados",'s'),("ComboGuardado","combo_guardado",'s'),
        ("CbmCaja","cbm_caja",'d'),("CbmUnitario","cbm_unitario",'d'),
        ("PrecioYuanes","precio_yuanes",'d'),("PrecioDolares","precio_dolares",'d'),("PrecioNacionalizado","precio_nacionalizado",'d'),
        ("PrecioVenta","precio_venta",'d'),("ValorTotalArticulo","valor_total_articulo",'d'),
        ("PNac","p_nac",'d'),("PDesc","p_desc",'d'),("TasaRmb","tasa_rmb",'d'),("TasaArs","tasa_ars",'d'),("TipoDolar","tipo_dolar",'s'),
        ("MoqUnidades","moq_unidades",'i'),("MoqColores","moq_colores",'i'),("DiasEntrega","dias_entrega",'i'),
        ("Foto","foto",'s'),("ImagenTablaTalles","imagen_tabla_talles",'s'),("ImagenTablaColores","imagen_tabla_colores",'s'),
        ("Observaciones","observaciones",'s')
    };
    private static readonly (string M, string S, char T)[] ColsProvFoto =
        { ("IdProveedor","id_proveedor",'i'),("Archivo","archivo",'s'),("EsPrincipal","es_principal",'b') };
    private static readonly (string M, string S, char T)[] ColsArtFoto =
        { ("IdArticulo","id_articulo",'i'),("Archivo","archivo",'s'),("EsPrincipal","es_principal",'b') };
    private static readonly (string M, string S, char T)[] ColsCostoProv =
        { ("IdProveedor","id_proveedor",'i'),("Concepto","concepto",'s'),("ImporteYuan","importe_yuan",'d') };
    private static readonly (string M, string S, char T)[] ColsCostoArt =
        { ("IdArticulo","id_articulo",'i'),("Concepto","concepto",'s'),("ImporteYuan","importe_yuan",'d') };
    private static readonly (string M, string S, char T)[] ColsCurva =
        { ("Codigo","codigo",'s'),("Descripcion","descripcion",'s'),("ListaTalles","lista_talles",'s') };
    private static readonly (string M, string S, char T)[] ColsCotiz =
        { ("Tipo","tipo",'s'),("Valor","valor",'d'),("FechaActualizacion","fecha_actualizacion",'t') };

    public async Task<ImportarViajeResultadoDto> ImportarAsync(string usuario, CancellationToken ct = default)
    {
        var dbPath = DbPath;
        if (!File.Exists(dbPath))
            return new ImportarViajeResultadoDto { Ok = false, Mensaje = $"No se encontró la base SQLite en: {dbPath}" };

        var aud = $"Import {usuario} | {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
        var res = new ImportarViajeResultadoDto { Ok = true };

        var srcCs = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly }.ToString();
        await using var src = new SqliteConnection(srcCs);
        await src.OpenAsync(ct);

        await using var dst = _db.Create();
        await dst.OpenAsync(ct);
        await using var tx = (SqlTransaction)await dst.BeginTransactionAsync(ct);
        try
        {
            // Viajes (cabecera: además fija Estado=CERRADO e FechaImportacion).
            res.Viajes = UpsertViajes(src, dst, tx, aud);
            res.Proveedores = UpsertPreservandoId(src, dst, tx, "Proveedor", "ProdProveedores", ColsProveedor, aud);
            res.Contenedores = UpsertPreservandoId(src, dst, tx, "Contenedor", "ProdContenedores", ColsContenedor, aud);

            // FKs válidas para los artículos (proveedores/contenedores importados).
            var prov = LeerIds(dst, tx, "ProdProveedores");
            var cont = LeerIds(dst, tx, "ProdContenedores");
            res.Articulos = UpsertPreservandoId(src, dst, tx, "Articulo", "ProdViajeArticulos", ColsArticulo, aud, p =>
            {
                if (p.TryGetValue("IdProveedor", out var ip) && ip is int pi && !prov.Contains(pi)) p["IdProveedor"] = null;
                if (p.TryGetValue("IdContenedor", out var ic) && ic is int ci && !cont.Contains(ci)) p["IdContenedor"] = null;
                return true;
            });

            // Hijos: se saltean los huérfanos (apuntan a un padre que no existe en el .db).
            var art = LeerIds(dst, tx, "ProdViajeArticulos");
            res.Fotos = UpsertPreservandoId(src, dst, tx, "FotoProveedor", "ProdProveedorFotos", ColsProvFoto, aud, p => ParentOk(p, "IdProveedor", prov))
                      + UpsertPreservandoId(src, dst, tx, "FotoArticulo", "ProdViajeArticuloFotos", ColsArtFoto, aud, p => ParentOk(p, "IdArticulo", art));
            UpsertPreservandoId(src, dst, tx, "CostoServicioProveedor", "ProdCostoServicioProveedor", ColsCostoProv, aud, p => ParentOk(p, "IdProveedor", prov));
            UpsertPreservandoId(src, dst, tx, "CostoServicioArticulo", "ProdCostoServicioArticulo", ColsCostoArt, aud, p => ParentOk(p, "IdArticulo", art));

            res.Catalogos = UpsertPreservandoId(src, dst, tx, "CurvaTalles", "ProdCurvaTalles", ColsCurva, aud)
                          + UpsertPreservandoId(src, dst, tx, "Cotizacion", "ProdCotizaciones", ColsCotiz, aud)
                          + UpsertColores(src, dst, tx, aud);

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new ImportarViajeResultadoDto { Ok = false, Mensaje = "Error en el import (se revirtió todo): " + ex.Message };
        }

        // Fotos al filesystem (fuera de la transacción).
        try { res.FotosCopiadas = CopiarFotos(); }
        catch (Exception ex) { res.Mensaje = "Datos OK, pero falló la copia de fotos: " + ex.Message; }

        if (string.IsNullOrEmpty(res.Mensaje))
            res.Mensaje = $"Import OK: {res.Articulos} artículos, {res.Proveedores} proveedores, {res.Contenedores} contenedores, {res.Fotos} fotos ({res.FotosCopiadas} copiadas).";
        return res;
    }

    // Viaje: además de Nombre/Fecha/Pais, fija Estado=CERRADO (viene post-viaje) y FechaImportacion.
    private static int UpsertViajes(SqliteConnection src, SqlConnection dst, SqlTransaction tx, string aud)
    {
        var rows = src.Query("SELECT * FROM Viaje").Cast<IDictionary<string, object>>().ToList();
        if (rows.Count == 0) return 0;
        dst.Execute("SET IDENTITY_INSERT dbo.ProdViajes ON", transaction: tx);
        int n = 0;
        foreach (var r in rows)
        {
            var id = Convert.ToInt32(r["id"]);
            var p = new DynamicParameters();
            p.Add("Id", id);
            p.Add("Nombre", Conv(Get(r, "nombre"), 's'));
            p.Add("Fecha", Conv(Get(r, "fecha"), 't'));
            p.Add("Pais", Conv(Get(r, "pais"), 's'));
            p.Add("Aud", aud);
            var exists = dst.ExecuteScalar<int?>("SELECT Id FROM dbo.ProdViajes WHERE IdSqlite=@Id", new { Id = id }, tx);
            if (exists.HasValue)
                dst.Execute("UPDATE dbo.ProdViajes SET Nombre=@Nombre, Fecha=@Fecha, Pais=@Pais, Estado='CERRADO', FechaImportacion=GETDATE(), Auditoria=@Aud WHERE IdSqlite=@Id", p, tx);
            else
                dst.Execute("INSERT INTO dbo.ProdViajes (Id, IdSqlite, Nombre, Fecha, Pais, Estado, FechaImportacion, Eliminado, Auditoria) VALUES (@Id,@Id,@Nombre,@Fecha,@Pais,'CERRADO',GETDATE(),0,@Aud)", p, tx);
            n++;
        }
        dst.Execute("SET IDENTITY_INSERT dbo.ProdViajes OFF", transaction: tx);
        return n;
    }

    // Colores no tiene id entero en SQLite: upsert por Codigo, Id auto, IdSqlite NULL.
    private static int UpsertColores(SqliteConnection src, SqlConnection dst, SqlTransaction tx, string aud)
    {
        var rows = src.Query("SELECT * FROM Colores").Cast<IDictionary<string, object>>().ToList();
        int n = 0;
        foreach (var r in rows)
        {
            var cod = Conv(Get(r, "codigo"), 's');
            var nom = Conv(Get(r, "nombre"), 's');
            if (cod is null) continue;
            var p = new { Codigo = cod, Nombre = nom, Aud = aud };
            var exists = dst.ExecuteScalar<int?>("SELECT Id FROM dbo.ProdColores WHERE Codigo=@Codigo", new { Codigo = cod }, tx);
            if (exists.HasValue) dst.Execute("UPDATE dbo.ProdColores SET Nombre=@Nombre, Auditoria=@Aud WHERE Codigo=@Codigo", p, tx);
            else dst.Execute("INSERT INTO dbo.ProdColores (Codigo, Nombre, Eliminado, Auditoria) VALUES (@Codigo,@Nombre,0,@Aud)", p, tx);
            n++;
        }
        return n;
    }

    // Motor genérico: espeja una tabla SQLite (con id entero) a una Prod* preservando el id (IDENTITY_INSERT) y upsert por IdSqlite.
    // prep: ajusta la fila (ej. nullar FKs opcionales) y/o decide si se OMITE (return false = no insertar; FK huérfana).
    private static int UpsertPreservandoId(SqliteConnection src, SqlConnection dst, SqlTransaction tx,
        string srcTable, string dstTable, (string M, string S, char T)[] cols, string aud,
        Func<Dictionary<string, object?>, bool>? prep = null)
    {
        var rows = src.Query($"SELECT * FROM {srcTable}").Cast<IDictionary<string, object>>().ToList();
        if (rows.Count == 0) return 0;

        var insCols = "Id, IdSqlite, " + string.Join(", ", cols.Select(c => c.M)) + ", Eliminado, Auditoria";
        var insVals = "@Id, @Id, " + string.Join(", ", cols.Select(c => "@" + c.M)) + ", 0, @Aud";
        var setUpd = string.Join(", ", cols.Select(c => $"{c.M}=@{c.M}")) + ", Auditoria=@Aud";

        dst.Execute($"SET IDENTITY_INSERT dbo.{dstTable} ON", transaction: tx);
        int n = 0;
        foreach (var r in rows)
        {
            var id = Convert.ToInt32(r["id"]);
            var dict = new Dictionary<string, object?> { ["Id"] = id, ["Aud"] = aud };
            foreach (var c in cols) dict[c.M] = Conv(Get(r, c.S), c.T);
            if (prep != null && !prep(dict)) continue;   // fila omitida (huérfana o filtrada)

            var dp = new DynamicParameters();
            foreach (var kv in dict) dp.Add(kv.Key, kv.Value);

            var exists = dst.ExecuteScalar<int?>($"SELECT Id FROM dbo.{dstTable} WHERE IdSqlite=@Id", new { Id = id }, tx);
            if (exists.HasValue) dst.Execute($"UPDATE dbo.{dstTable} SET {setUpd} WHERE IdSqlite=@Id", dp, tx);
            else dst.Execute($"INSERT INTO dbo.{dstTable} ({insCols}) VALUES ({insVals})", dp, tx);
            n++;
        }
        dst.Execute($"SET IDENTITY_INSERT dbo.{dstTable} OFF", transaction: tx);
        return n;
    }

    private static HashSet<int> LeerIds(SqlConnection dst, SqlTransaction tx, string tabla)
        => dst.Query<int>($"SELECT Id FROM dbo.{tabla} WHERE Eliminado=0", transaction: tx).ToHashSet();

    // El padre (IdArticulo / IdProveedor) tiene que existir; si no, la fila es huérfana y se omite.
    private static bool ParentOk(Dictionary<string, object?> d, string key, HashSet<int> validos)
        => d.TryGetValue(key, out var v) && v is int id && validos.Contains(id);

    private int CopiarFotos()
    {
        var origen = FotosOrigen;
        var destino = FotosDestino;
        if (!Directory.Exists(origen)) return 0;
        // Si destino == origen, se sirve en el lugar (no hay nada que copiar).
        if (string.Equals(Path.GetFullPath(origen).TrimEnd('\\'), Path.GetFullPath(destino).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return 0;
        Directory.CreateDirectory(destino);
        int n = 0;
        foreach (var f in Directory.EnumerateFiles(origen))
        {
            File.Copy(f, Path.Combine(destino, Path.GetFileName(f)), overwrite: true);
            n++;
        }
        return n;
    }

    private static object? Get(IDictionary<string, object> r, string key) => r.TryGetValue(key, out var v) ? v : null;

    private static object? Conv(object? v, char t)
    {
        if (v is null or DBNull) return null;
        switch (t)
        {
            case 's': { var s = Convert.ToString(v); return string.IsNullOrEmpty(s) ? null : s; }
            case 'i': return Convert.ToInt32(v, CultureInfo.InvariantCulture);
            case 'd': return Convert.ToDecimal(v, CultureInfo.InvariantCulture);
            case 'b': return Convert.ToInt32(v, CultureInfo.InvariantCulture) != 0;
            case 't':
                {
                    var s = Convert.ToString(v);
                    return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : (object?)null;
                }
            default: return Convert.ToString(v);
        }
    }
}
