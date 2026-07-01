-- ============================================================
-- Órdenes de Pedido: columnas nuevas para integrar con Dragon + proceso del Sheets.
-- ALTER idempotente (IF COL_LENGTH ... IS NULL), patrón del auto-heal de Israel.
-- Solo AGREGA columnas. No borra, no modifica datos. Convención: baja lógica / Auditoria.
-- La corre Israel. NO ejecutar contra producción desde acá.
--
-- NOTA: 'Tipo' ya existe en ProdOrdenes (NACIONAL/IMPORTADO). El tipo de artículo
--       (Indumentaria/Accesorios) se agrega como 'TipoArticulo' para no colisionar.
-- Las fechas/cantidades de producción van en ProdOrdenesDetalle (nivel artículo).
-- ProdOrdenesProduccion NO se toca (queda como matriz color×talle).
-- ============================================================
SET NOCOUNT ON;

-- ============================================================
-- 1) ProdOrdenes (cabecera)
-- ============================================================
IF COL_LENGTH('dbo.ProdOrdenes','CodTemporada')  IS NULL ALTER TABLE dbo.ProdOrdenes ADD CodTemporada  NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenes','TipoArticulo')  IS NULL ALTER TABLE dbo.ProdOrdenes ADD TipoArticulo  NVARCHAR(20)  NULL;  -- Indumentaria, Accesorios, etc. (NO es el Tipo NACIONAL/IMPORTADO)
IF COL_LENGTH('dbo.ProdOrdenes','CodTipo')       IS NULL ALTER TABLE dbo.ProdOrdenes ADD CodTipo       NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenes','Categoria')     IS NULL ALTER TABLE dbo.ProdOrdenes ADD Categoria     NVARCHAR(50)  NULL;
IF COL_LENGTH('dbo.ProdOrdenes','CodCategoria')  IS NULL ALTER TABLE dbo.ProdOrdenes ADD CodCategoria  NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenes','Linea')         IS NULL ALTER TABLE dbo.ProdOrdenes ADD Linea         NVARCHAR(50)  NULL;
IF COL_LENGTH('dbo.ProdOrdenes','CodLinea')      IS NULL ALTER TABLE dbo.ProdOrdenes ADD CodLinea      NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenes','CodMaterial')   IS NULL ALTER TABLE dbo.ProdOrdenes ADD CodMaterial   NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenes','CodFamilia')    IS NULL ALTER TABLE dbo.ProdOrdenes ADD CodFamilia    NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenes','CodSubfamilia') IS NULL ALTER TABLE dbo.ProdOrdenes ADD CodSubfamilia NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenes','FechaOrden')    IS NULL ALTER TABLE dbo.ProdOrdenes ADD FechaOrden    DATE          NULL;
IF COL_LENGTH('dbo.ProdOrdenes','EsTelaPropia')  IS NULL ALTER TABLE dbo.ProdOrdenes ADD EsTelaPropia  BIT NOT NULL CONSTRAINT DF_ProdOrdenes_EsTelaPropia DEFAULT(0);

-- ============================================================
-- 2) ProdOrdenesDetalle (renglón / artículo)
-- ============================================================
-- Catálogo / Dragon
IF COL_LENGTH('dbo.ProdOrdenesDetalle','DescripcionEcommerce') IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD DescripcionEcommerce NVARCHAR(300) NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','EsMuestra')            IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD EsMuestra            BIT NOT NULL CONSTRAINT DF_ProdOrdDet_EsMuestra DEFAULT(0);
IF COL_LENGTH('dbo.ProdOrdenesDetalle','CodCurva')             IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD CodCurva             NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','PaletaColores')        IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD PaletaColores        NVARCHAR(50)  NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','CodPaleta')            IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD CodPaleta            NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','Caracteristica')       IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD Caracteristica       NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','CodCaracteristica')    IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD CodCaracteristica    NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','TratamientoMaterial')  IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD TratamientoMaterial  NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','Marca')                IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD Marca                NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','CodMarca')             IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD CodMarca             NVARCHAR(20)  NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','NroPedidoTextil')      IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD NroPedidoTextil      NVARCHAR(50)  NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','CantRollosEntregados') IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD CantRollosEntregados INT           NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','PesoRollosKg')         IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD PesoRollosKg         DECIMAL(12,2) NULL;
-- Proceso de producción (fechas + cantidades) -- movido acá desde ProdOrdenesProduccion
IF COL_LENGTH('dbo.ProdOrdenesDetalle','FechaEntregaTela')            IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD FechaEntregaTela            DATE NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','FechaPedidoEtiqueta')         IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD FechaPedidoEtiqueta         DATE NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','FechaEnvioEtiquetas')         IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD FechaEnvioEtiquetas         DATE NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','FechaEstimadaEntregaProv')    IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD FechaEstimadaEntregaProv    DATE NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','FechaEstimadaEntregaReporte') IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD FechaEstimadaEntregaReporte DATE NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','FechaEntrega')                IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD FechaEntrega                DATE NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','SegundaFechaEntrega')         IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD SegundaFechaEntrega         DATE NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','CantPrendasEstimadas')        IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD CantPrendasEstimadas        INT  NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','CantPrendasRecibidas')        IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD CantPrendasRecibidas        INT  NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','CantEtiquetasEnviadas')       IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD CantEtiquetasEnviadas       INT  NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','PrendasEtiquetadas')          IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD PrendasEtiquetadas          INT  NULL;
IF COL_LENGTH('dbo.ProdOrdenesDetalle','PrendasParaEtiquetar')        IS NULL ALTER TABLE dbo.ProdOrdenesDetalle ADD PrendasParaEtiquetar        INT  NULL;

-- ProdOrdenesProduccion: NO se toca (matriz color×talle: Id, IdRenglon, ColorCod, Talle, CantEstimada, CantReal, Eliminado, Auditoria).

-- ============================================================
-- 3) Control: columnas actuales de las 3 tablas
-- ============================================================
SELECT TABLE_NAME, ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('ProdOrdenes','ProdOrdenesDetalle','ProdOrdenesProduccion')
ORDER BY TABLE_NAME, ORDINAL_POSITION;
