/* ============================================================================
   Prod_Viajes.sql  —  Módulo Producción / Viajes de compra
   Importa el .db (SQLite) de la app offline ViajePedidos a MARKET.

   Convención (igual que SQL_Telas.sql):
     - Id        INT IDENTITY(1,1) PRIMARY KEY
     - IdSqlite  INT NULL   -> id original de la base SQLite (ancla del import / upsert).
                              NULL para filas creadas desde la web.
     - Eliminado BIT NOT NULL DEFAULT 0   (borrado lógico, nunca DELETE)
     - Auditoria NVARCHAR(200) NULL

   El importador usa SET IDENTITY_INSERT ON para que Id == IdSqlite en lo importado,
   e inserta los padres antes que los hijos (FKs). Idempotente: re-ejecutable.
   ============================================================================ */

-- ---------------------------------------------------------------- ProdViajes
IF OBJECT_ID('dbo.ProdViajes','U') IS NULL
CREATE TABLE dbo.ProdViajes (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite         INT NULL,
    Nombre           NVARCHAR(150) NULL,
    Fecha            DATE NULL,
    Pais             NVARCHAR(80) NULL,
    Estado           NVARCHAR(20) NOT NULL CONSTRAINT DF_ProdViajes_Estado DEFAULT('ABIERTO'),
    FechaImportacion DATETIME NULL,
    Eliminado        BIT NOT NULL CONSTRAINT DF_ProdViajes_Elim DEFAULT(0),
    Auditoria        NVARCHAR(200) NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdViajes_IdSqlite')
    CREATE UNIQUE INDEX UX_ProdViajes_IdSqlite ON dbo.ProdViajes(IdSqlite) WHERE IdSqlite IS NOT NULL;

-- ----------------------------------------------------------- ProdProveedores  (globales, compartidos entre viajes)
IF OBJECT_ID('dbo.ProdProveedores','U') IS NULL
CREATE TABLE dbo.ProdProveedores (
    Id                 INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite           INT NULL,
    Nombre             NVARCHAR(150) NULL,
    Celular            NVARCHAR(50) NULL,
    Email              NVARCHAR(150) NULL,
    Ciudad             NVARCHAR(100) NULL,
    Pais               NVARCHAR(80) NULL,
    Codigo             NVARCHAR(50) NULL,
    Broker             NVARCHAR(150) NULL,
    Observaciones      NVARCHAR(MAX) NULL,
    DiasEntrega        INT NULL,
    Foto               NVARCHAR(300) NULL,
    ImagenTablaTalles  NVARCHAR(300) NULL,
    ImagenTablaColores NVARCHAR(300) NULL,
    Eliminado          BIT NOT NULL CONSTRAINT DF_ProdProveedores_Elim DEFAULT(0),
    Auditoria          NVARCHAR(200) NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdProveedores_IdSqlite')
    CREATE UNIQUE INDEX UX_ProdProveedores_IdSqlite ON dbo.ProdProveedores(IdSqlite) WHERE IdSqlite IS NOT NULL;

-- -------------------------------------------------------- ProdProveedorFotos
IF OBJECT_ID('dbo.ProdProveedorFotos','U') IS NULL
CREATE TABLE dbo.ProdProveedorFotos (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite    INT NULL,
    IdProveedor INT NOT NULL,
    Archivo     NVARCHAR(300) NULL,
    EsPrincipal BIT NOT NULL CONSTRAINT DF_ProdProvFotos_Pri DEFAULT(0),
    Eliminado   BIT NOT NULL CONSTRAINT DF_ProdProvFotos_Elim DEFAULT(0),
    Auditoria   NVARCHAR(200) NULL,
    CONSTRAINT FK_ProdProvFotos_Prov FOREIGN KEY (IdProveedor) REFERENCES dbo.ProdProveedores(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdProvFotos_IdSqlite')
    CREATE UNIQUE INDEX UX_ProdProvFotos_IdSqlite ON dbo.ProdProveedorFotos(IdSqlite) WHERE IdSqlite IS NOT NULL;

-- ------------------------------------------------- ProdCostoServicioProveedor
IF OBJECT_ID('dbo.ProdCostoServicioProveedor','U') IS NULL
CREATE TABLE dbo.ProdCostoServicioProveedor (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite    INT NULL,
    IdProveedor INT NOT NULL,
    Concepto    NVARCHAR(150) NULL,
    ImporteYuan DECIMAL(18,4) NULL,
    Eliminado   BIT NOT NULL CONSTRAINT DF_ProdCostoProv_Elim DEFAULT(0),
    Auditoria   NVARCHAR(200) NULL,
    CONSTRAINT FK_ProdCostoProv_Prov FOREIGN KEY (IdProveedor) REFERENCES dbo.ProdProveedores(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdCostoProv_IdSqlite')
    CREATE UNIQUE INDEX UX_ProdCostoProv_IdSqlite ON dbo.ProdCostoServicioProveedor(IdSqlite) WHERE IdSqlite IS NOT NULL;

-- --------------------------------------------------------- ProdContenedores  (por viaje)
IF OBJECT_ID('dbo.ProdContenedores','U') IS NULL
CREATE TABLE dbo.ProdContenedores (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite         INT NULL,
    IdViaje          INT NOT NULL,
    NombreContenedor NVARCHAR(100) NULL,
    Tipo             NVARCHAR(50) NULL,
    CapacidadMaxCbm  DECIMAL(18,4) NULL,
    Eliminado        BIT NOT NULL CONSTRAINT DF_ProdContenedores_Elim DEFAULT(0),
    Auditoria        NVARCHAR(200) NULL,
    CONSTRAINT FK_ProdContenedores_Viaje FOREIGN KEY (IdViaje) REFERENCES dbo.ProdViajes(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdContenedores_IdSqlite')
    CREATE UNIQUE INDEX UX_ProdContenedores_IdSqlite ON dbo.ProdContenedores(IdSqlite) WHERE IdSqlite IS NOT NULL;

-- ----------------------------------------------------- ProdViajeArticulos  (el corazón, fiel a las 44 cols)
IF OBJECT_ID('dbo.ProdViajeArticulos','U') IS NULL
CREATE TABLE dbo.ProdViajeArticulos (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite            INT NULL,
    IdViaje             INT NULL,
    IdProveedor         INT NULL,
    IdContenedor        INT NULL,
    -- identificación / descripción
    CodigoInterno       NVARCHAR(50) NULL,
    CodigoProveedor     NVARCHAR(50) NULL,
    NumeroGeneral       INT NULL,
    Descripcion         NVARCHAR(300) NULL,
    Tipo                NVARCHAR(50) NULL,
    Genero              NVARCHAR(50) NULL,
    Material            NVARCHAR(100) NULL,
    -- talles / colores (texto fiel; se normaliza más adelante)
    Talles              NVARCHAR(200) NULL,
    CurvaTalles         NVARCHAR(100) NULL,
    TablaTalles         NVARCHAR(MAX) NULL,
    Colores             NVARCHAR(MAX) NULL,
    CurvaColores        NVARCHAR(200) NULL,
    ColoresProveedor    NVARCHAR(MAX) NULL,
    -- bultos / packs
    PrendasPorPack      INT NULL,
    PrendasPackBase     INT NULL,
    PacksPorCaja        INT NULL,
    CajasPedidas        INT NULL,
    CantidadTotalPrendas INT NULL,
    TipoBulto           NVARCHAR(50) NULL,
    PacksArmados        NVARCHAR(MAX) NULL,   -- JSON tal cual (ej. [{"packs":84,...}])
    ComboGuardado       NVARCHAR(50) NULL,    -- ej. "2x6000"
    -- cubicaje
    CbmCaja             DECIMAL(18,4) NULL,
    CbmUnitario         DECIMAL(18,4) NULL,
    -- precios / costos
    PrecioYuanes        DECIMAL(18,4) NULL,
    PrecioDolares       DECIMAL(18,4) NULL,
    PrecioNacionalizado DECIMAL(18,4) NULL,
    PrecioVenta         DECIMAL(18,4) NULL,
    ValorTotalArticulo  DECIMAL(18,4) NULL,
    PNac                DECIMAL(18,4) NULL,
    PDesc               DECIMAL(18,4) NULL,
    TasaRmb             DECIMAL(18,6) NULL,
    TasaArs             DECIMAL(18,6) NULL,
    TipoDolar           NVARCHAR(50) NULL,
    -- MOQ / entrega
    MoqUnidades         INT NULL,
    MoqColores          INT NULL,
    DiasEntrega         INT NULL,
    -- imágenes / varios
    Foto                NVARCHAR(300) NULL,
    ImagenTablaTalles   NVARCHAR(300) NULL,
    ImagenTablaColores  NVARCHAR(300) NULL,
    Observaciones       NVARCHAR(MAX) NULL,
    Eliminado           BIT NOT NULL CONSTRAINT DF_ProdViajeArticulos_Elim DEFAULT(0),
    Auditoria           NVARCHAR(200) NULL,
    CONSTRAINT FK_ProdVArt_Viaje  FOREIGN KEY (IdViaje)      REFERENCES dbo.ProdViajes(Id),
    CONSTRAINT FK_ProdVArt_Prov   FOREIGN KEY (IdProveedor)  REFERENCES dbo.ProdProveedores(Id),
    CONSTRAINT FK_ProdVArt_Conten FOREIGN KEY (IdContenedor) REFERENCES dbo.ProdContenedores(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdViajeArticulos_IdSqlite')
    CREATE UNIQUE INDEX UX_ProdViajeArticulos_IdSqlite ON dbo.ProdViajeArticulos(IdSqlite) WHERE IdSqlite IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ProdViajeArticulos_Viaje')
    CREATE INDEX IX_ProdViajeArticulos_Viaje ON dbo.ProdViajeArticulos(IdViaje) WHERE Eliminado = 0;

-- --------------------------------------------------- ProdViajeArticuloFotos
IF OBJECT_ID('dbo.ProdViajeArticuloFotos','U') IS NULL
CREATE TABLE dbo.ProdViajeArticuloFotos (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite    INT NULL,
    IdArticulo  INT NOT NULL,
    Archivo     NVARCHAR(300) NULL,
    EsPrincipal BIT NOT NULL CONSTRAINT DF_ProdVArtFotos_Pri DEFAULT(0),
    Eliminado   BIT NOT NULL CONSTRAINT DF_ProdVArtFotos_Elim DEFAULT(0),
    Auditoria   NVARCHAR(200) NULL,
    CONSTRAINT FK_ProdVArtFotos_Art FOREIGN KEY (IdArticulo) REFERENCES dbo.ProdViajeArticulos(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdVArtFotos_IdSqlite')
    CREATE UNIQUE INDEX UX_ProdVArtFotos_IdSqlite ON dbo.ProdViajeArticuloFotos(IdSqlite) WHERE IdSqlite IS NOT NULL;

-- -------------------------------------------------- ProdCostoServicioArticulo
IF OBJECT_ID('dbo.ProdCostoServicioArticulo','U') IS NULL
CREATE TABLE dbo.ProdCostoServicioArticulo (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite    INT NULL,
    IdArticulo  INT NOT NULL,
    Concepto    NVARCHAR(150) NULL,
    ImporteYuan DECIMAL(18,4) NULL,
    Eliminado   BIT NOT NULL CONSTRAINT DF_ProdCostoArt_Elim DEFAULT(0),
    Auditoria   NVARCHAR(200) NULL,
    CONSTRAINT FK_ProdCostoArt_Art FOREIGN KEY (IdArticulo) REFERENCES dbo.ProdViajeArticulos(Id)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdCostoArt_IdSqlite')
    CREATE UNIQUE INDEX UX_ProdCostoArt_IdSqlite ON dbo.ProdCostoServicioArticulo(IdSqlite) WHERE IdSqlite IS NOT NULL;

-- ================= Catálogos =================

-- ProdColores (Colores en SQLite no tiene id entero: codigo/nombre). IdSqlite queda NULL.
IF OBJECT_ID('dbo.ProdColores','U') IS NULL
CREATE TABLE dbo.ProdColores (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite  INT NULL,
    Codigo    NVARCHAR(20) NULL,
    Nombre    NVARCHAR(150) NULL,
    Eliminado BIT NOT NULL CONSTRAINT DF_ProdColores_Elim DEFAULT(0),
    Auditoria NVARCHAR(200) NULL
);

-- ProdCurvaTalles
IF OBJECT_ID('dbo.ProdCurvaTalles','U') IS NULL
CREATE TABLE dbo.ProdCurvaTalles (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite    INT NULL,
    Codigo      NVARCHAR(20) NULL,
    Descripcion NVARCHAR(150) NULL,
    ListaTalles NVARCHAR(300) NULL,
    Eliminado   BIT NOT NULL CONSTRAINT DF_ProdCurvaTalles_Elim DEFAULT(0),
    Auditoria   NVARCHAR(200) NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdCurvaTalles_IdSqlite')
    CREATE UNIQUE INDEX UX_ProdCurvaTalles_IdSqlite ON dbo.ProdCurvaTalles(IdSqlite) WHERE IdSqlite IS NOT NULL;

-- ProdCotizaciones
IF OBJECT_ID('dbo.ProdCotizaciones','U') IS NULL
CREATE TABLE dbo.ProdCotizaciones (
    Id                INT IDENTITY(1,1) PRIMARY KEY,
    IdSqlite          INT NULL,
    Tipo              NVARCHAR(50) NULL,
    Valor             DECIMAL(18,4) NULL,
    FechaActualizacion DATETIME NULL,
    Eliminado         BIT NOT NULL CONSTRAINT DF_ProdCotizaciones_Elim DEFAULT(0),
    Auditoria         NVARCHAR(200) NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ProdCotizaciones_IdSqlite')
    CREATE UNIQUE INDEX UX_ProdCotizaciones_IdSqlite ON dbo.ProdCotizaciones(IdSqlite) WHERE IdSqlite IS NOT NULL;

-- ============================================================================
-- FIN. 11 tablas Prod* (espejan las 11 de logistica_china.db).
-- ============================================================================
