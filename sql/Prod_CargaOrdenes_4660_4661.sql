-- ============================================================
-- Carga de prueba: órdenes 4660 y 4661 (de la planilla "Pedido corte")
-- → ProdOrdenes (cabecera) + ProdOrdenesDetalle (renglón) + ProdOrdenesColores.
-- Idempotente: si la orden ya existe (NroOrden, Eliminado=0), no la vuelve a cargar.
-- ExisteEnDragon se calcula contra DRAGONFISH_CENTRAL.Zoologic.ART.
-- El nombre de color sale de TelasColores por código.
-- Ambas: NACIONAL, 1 artículo. La corre Israel (o vos) cuando confirmen.
-- ============================================================
SET NOCOUNT ON;

DECLARE @aud NVARCHAR(200) =
    N'Carga manual planilla | WEB | ' + CONVERT(varchar(10), GETDATE(), 103) + N' ' + CONVERT(varchar(8), GETDATE(), 108);
DECLARE @idOrden INT, @idDet INT;

-- ================= Orden 4660 =================
IF NOT EXISTS (SELECT 1 FROM dbo.ProdOrdenes WHERE NroOrden = 4660 AND Eliminado = 0)
BEGIN
    INSERT INTO dbo.ProdOrdenes
        (NroOrden, Tipo, Estado, ProveedorCod, ProveedorNombre, Temporada, Anio, Material, Familia, Subfamilia, Finalizada, Eliminado, Auditoria)
    VALUES
        (4660, N'NACIONAL', N'Entrega pendiente', N'005', N'LOLA (GUTIRREZ)', N'Oto-Inv', 26,
         N'Frisa cardada', N'Pantalon', N'Pantalon Straigt/ Recto', 0, 0, @aud);
    SET @idOrden = SCOPE_IDENTITY();

    INSERT INTO dbo.ProdOrdenesDetalle
        (IdOrden, ARTCOD, Descripcion, ExisteEnDragon, TieneFicha, MobiliarioDestino, CostoUnit, Origen, Estado, NroItem, Talles, Curva, Finalizada, Eliminado, Auditoria)
    VALUES
        (@idOrden, N'IH005.100', N'PANT FRI CARDADA RECTO JOGGER TRES TIRAS ARG',
         CASE WHEN EXISTS (SELECT 1 FROM DRAGONFISH_CENTRAL.Zoologic.ART WHERE ARTCOD = N'IH005.100') THEN 1 ELSE 0 END,
         0, N'Mesa', 9500, N'Planilla', N'Entrega pendiente', 1, N'S AL XL', N'S a 2XL', 0, 0, @aud);
    SET @idDet = SCOPE_IDENTITY();

    INSERT INTO dbo.ProdOrdenesColores (IdRenglon, ColorCod, ColorNombre, Rollos, Eliminado, Auditoria)
    SELECT @idDet, c.cod, (SELECT TOP 1 Descripcion FROM dbo.TelasColores WHERE Codigo = c.cod AND Eliminado = 0), NULL, 0, @aud
    FROM (VALUES (N'03'), (N'08'), (N'21'), (N'05'), (N'16'), (N'04')) AS c(cod);
END

-- ================= Orden 4661 =================
IF NOT EXISTS (SELECT 1 FROM dbo.ProdOrdenes WHERE NroOrden = 4661 AND Eliminado = 0)
BEGIN
    INSERT INTO dbo.ProdOrdenes
        (NroOrden, Tipo, Estado, ProveedorCod, ProveedorNombre, Temporada, Anio, Material, Familia, Subfamilia, Finalizada, Eliminado, Auditoria)
    VALUES
        (4661, N'NACIONAL', N'Entrega de etiquetas pendiente', N'002', N'DIONICIO (KRB BRIAN) H02', N'Oto-Inv', 26,
         N'Frisa cardada', N'Pantalon', N'Pantalon Straigt/ Recto', 0, 0, @aud);
    SET @idOrden = SCOPE_IDENTITY();

    INSERT INTO dbo.ProdOrdenesDetalle
        (IdOrden, ARTCOD, Descripcion, ExisteEnDragon, TieneFicha, MobiliarioDestino, CostoUnit, Origen, Estado, NroItem, Talles, Curva, Finalizada, Eliminado, Auditoria)
    VALUES
        (@idOrden, N'IH002.108', N'PANT FRISA CARDADA RECTO C/BOLSILLO',
         CASE WHEN EXISTS (SELECT 1 FROM DRAGONFISH_CENTRAL.Zoologic.ART WHERE ARTCOD = N'IH002.108') THEN 1 ELSE 0 END,
         0, N'Mesa', 10500, N'Planilla', N'Entrega de etiquetas pendiente', 1, N'S AL XL', N'S a 2XL', 0, 0, @aud);
    SET @idDet = SCOPE_IDENTITY();

    INSERT INTO dbo.ProdOrdenesColores (IdRenglon, ColorCod, ColorNombre, Rollos, Eliminado, Auditoria)
    SELECT @idDet, c.cod, (SELECT TOP 1 Descripcion FROM dbo.TelasColores WHERE Codigo = c.cod AND Eliminado = 0), NULL, 0, @aud
    FROM (VALUES (N'07'), (N'06'), (N'16'), (N'03'), (N'08'), (N'09')) AS c(cod);
END

-- ================= Control =================
SELECT O.NroOrden, O.Tipo, O.Estado, O.ProveedorCod, O.ProveedorNombre, O.Material, O.Familia,
       D.ARTCOD, D.Descripcion, D.ExisteEnDragon, D.CostoUnit, D.MobiliarioDestino, D.Talles, D.Curva,
       Colores = (SELECT COUNT(*) FROM dbo.ProdOrdenesColores WHERE IdRenglon = D.Id AND Eliminado = 0)
FROM dbo.ProdOrdenes O
JOIN dbo.ProdOrdenesDetalle D ON D.IdOrden = O.Id AND D.Eliminado = 0
WHERE O.NroOrden IN (4660, 4661) AND O.Eliminado = 0;
