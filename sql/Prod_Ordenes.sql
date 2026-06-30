-- ============================================================
-- ÓRDENES DE PEDIDO (Producción) — Fase 1 (esqueleto, sin Dragon).
-- Cabecera ProdOrdenes + detalle ProdOrdenesDetalle.
-- Lo crea solo el servicio (auto-heal idempotente); este .sql queda de referencia.
-- Convención: Id INT IDENTITY, Eliminado BIT (constraint nombrada), Auditoria.
-- Una orden = filas de PedidosOrdenes (Asana) con el mismo NroOrden.
-- Tipo IMPORTADO (datos del viaje, packs/precios) vs NACIONAL (ficha técnica, pack al cerrar producción).
-- ============================================================
SET NOCOUNT ON;

IF OBJECT_ID('dbo.ProdOrdenes','U') IS NULL
CREATE TABLE dbo.ProdOrdenes(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NroOrden        INT           NOT NULL,
    Tipo            NVARCHAR(20)  NOT NULL,                 -- NACIONAL / IMPORTADO
    Estado          NVARCHAR(40)  NOT NULL CONSTRAINT DF_ProdOrdenes_Estado DEFAULT('Borrador'),
    ProveedorCod    NVARCHAR(50)  NULL,                     -- PROV.CLCOD (se completa en fases siguientes)
    ProveedorNombre NVARCHAR(150) NULL,
    IdViaje         INT           NULL,
    Moneda          NVARCHAR(20)  NULL,
    FechaLlegada    DATE          NULL,
    Etiquetador     NVARCHAR(150) NULL,                     -- PROV o Telera
    Temporada       NVARCHAR(40)  NULL,
    Anio            INT           NULL,
    Material        NVARCHAR(50)  NULL,
    Familia         NVARCHAR(50)  NULL,
    Subfamilia      NVARCHAR(80)  NULL,                     -- = GRUPO de Dragon
    Finalizada      BIT           NOT NULL CONSTRAINT DF_ProdOrdenes_Final DEFAULT(0),
    Eliminado       BIT           NOT NULL CONSTRAINT DF_ProdOrdenes_Elim  DEFAULT(0),
    Auditoria       NVARCHAR(200) NULL);

IF OBJECT_ID('dbo.ProdOrdenesDetalle','U') IS NULL
CREATE TABLE dbo.ProdOrdenesDetalle(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdOrden          INT           NOT NULL,
    ARTCOD           NVARCHAR(50)  NULL,
    CodigoProveedor  NVARCHAR(100) NULL,
    Descripcion      NVARCHAR(300) NULL,
    ExisteEnDragon   BIT           NOT NULL CONSTRAINT DF_ProdOrdDet_Existe  DEFAULT(0),
    TieneFicha       BIT           NOT NULL CONSTRAINT DF_ProdOrdDet_Ficha   DEFAULT(0),
    EquiTalle        NVARCHAR(150) NULL,
    MobiliarioDestino NVARCHAR(50) NULL,                    -- por renglón
    Cantidad         INT           NULL,                    -- derivada de packs (Importado); pendiente en Nacional
    Packs            INT           NULL,
    CostoUnit        DECIMAL(18,2) NULL,
    PrecioVenta      DECIMAL(18,2) NULL,
    Origen           NVARCHAR(20)  NULL,                    -- Viaje / Asana
    IdPedidoOrden    INT           NULL,                    -- traza a PedidosOrdenes.ID
    IdViajeArticulo  INT           NULL,
    Estado           NVARCHAR(40)  NULL,
    Finalizada       BIT           NOT NULL CONSTRAINT DF_ProdOrdDet_Final DEFAULT(0),
    NroItem          INT           NULL,
    Eliminado        BIT           NOT NULL CONSTRAINT DF_ProdOrdDet_Elim  DEFAULT(0),
    Auditoria        NVARCHAR(200) NULL,
    CONSTRAINT FK_ProdOrdDet_Orden FOREIGN KEY (IdOrden) REFERENCES dbo.ProdOrdenes(Id));

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ProdOrdDet_Orden')
    CREATE INDEX IX_ProdOrdDet_Orden ON dbo.ProdOrdenesDetalle(IdOrden) WHERE Eliminado=0;
