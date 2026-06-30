# Órdenes de Pedido — Handoff (MarketWeb)

Documento para continuar el módulo **Órdenes de Pedido**. Estado al 2026-06-30.
Repo: https://github.com/israelbastarrica/MARKETweb · Local: `C:\Documentos\Programación\VB.Net\MARKET-Web`
Stack: ASP.NET Core 9 (API) + Blazor WASM hosted, Dapper + Microsoft.Data.SqlClient, Google SSO @marketarg.com.

---

## 1. Objetivo

Reemplazar la carga manual por CSV de artículos y presupuestos a Dragon (ZooLogic) por un flujo
estructurado en MarketWeb. Una **Orden de Pedido** agrupa **N artículos** (renglones) y termina en Dragon
con **alta de artículo + precios + PRECOMPRA** (presupuesto de compra), todo por **API de Dragon** (Fase 2, pendiente).

Hay **dos tipos** de orden con ciclos de vida distintos:
- **IMPORTADO** (viene de un Viaje): packs, cantidades, colores, curva y costo **ya están definidos** (módulo Viajes / `ProdViajeArticulos`). Va a Dragon casi directo.
- **NACIONAL**: arranca con una **Ficha Técnica** que se manda al proveedor; el **pack/cantidades recién se cierran al terminar la producción**.

Menú: **Producción → Órdenes de Pedido** (ruta `/diseno/ordenes`). *(El módulo "Diseño" se unificó dentro de "Producción".)*

---

## 2. Estado actual (qué está HECHO)

**Fase 1 (esqueleto, sin Dragon) — COMPLETA y deployada:**
- Tablas `ProdOrdenes` + `ProdOrdenesDetalle` (auto-heal idempotente desde el servicio).
- Listado de órdenes + ABM de detalle (cabecera editable + grilla de renglones).
- **Import desde lo viejo**: trae órdenes de `PedidosOrdenes` (Asana) + Dragon `ART` + planilla `ProdCodigosMarket`.
  - `Importar muestra`: 1 IMPORTADO + 1 NACIONAL juguetes (`ARTCOD LIKE 'JU%'`) + 1 NACIONAL indumentaria (`'I%'`).
  - **Import por N° de orden exacto** (recomendado; determinístico).
- **Campos del "machete"** por renglón (modal lápiz): Talles, Curva, Armado, Prioridad, Corte, Costo, Fecha de entrega (texto libre).
- **Precio de venta (combo)**: se calcula del Costo leyendo la tabla **`PreciosLista`**.
- **Cabecera con combos de Dragon**: Temporada, Material, Familia, Subfamilia (Grupo).
- **B2 — Colores/rollos** (modal paleta): elige del **stock real de Telas** (depósito de Lito): filtros depósito + telera (proveedor) + material → busca colores con stock → agrega.
- **C — Producción color×talle** (modal grilla): matriz colores × talles con **E=estimado** (nuestro) + **R=real** (lo que produjo el proveedor).
- Mobiliario (Armado) = combo **Mesa / Perchero**.

**Pendiente:** Fase 2 (Dragon por API), datos completos de la ficha Nacional, mapeo Material Dragon↔Telas, consumo de stock de rollos, migrar todas las órdenes, re-sync de Asana. Ver sección 9.

---

## 3. Modelo de datos

### Tablas NUEVAS (las crea el servicio, auto-heal idempotente; ref: `sql/Prod_Ordenes.sql`)

**`ProdOrdenes`** (cabecera de la orden):
`Id` IDENTITY PK · `NroOrden` int · `Tipo` (NACIONAL/IMPORTADO) · `Estado` · `ProveedorCod` (=PROV.CLCOD) · `ProveedorNombre` · `IdViaje` · `Moneda` · `FechaLlegada` · `Etiquetador` · `Temporada` · `Anio` · `Material` · `Familia` · `Subfamilia` (=Grupo) · `Finalizada` · `Eliminado` · `Auditoria`.

**`ProdOrdenesDetalle`** (renglón = un artículo):
`Id` PK · `IdOrden` (FK) · `ARTCOD` · `CodigoProveedor` · `Descripcion` · `ExisteEnDragon` · `TieneFicha` · `EquiTalle` · `MobiliarioDestino` (Armado) · `Cantidad` · `Packs` · `CostoUnit` · `PrecioVenta` · `Origen` (Viaje/Asana) · `IdPedidoOrden` (traza a PedidosOrdenes.ID) · `IdViajeArticulo` · `Estado` · `Finalizada` · `NroItem` · `Eliminado` · `Auditoria` · **machete:** `Corte` · `Prioridad` · `Talles` · `Curva` · `FechaEntregaTexto`.

**`ProdOrdenesColores`** (B — colores/rollos por renglón):
`Id` PK · `IdRenglon` (FK) · `ColorCod` · `ColorNombre` · `Rollos` · `Eliminado` · `Auditoria`.

**`ProdOrdenesProduccion`** (C — producción color×talle):
`Id` PK · `IdRenglon` (FK) · `ColorCod` · `Talle` · `CantEstimada` · `CantReal` · `Eliminado` · `Auditoria`.

> Convención (toda la base MARKET): `Id INT IDENTITY(1,1)` PK, `Eliminado BIT` con constraint nombrada, `Auditoria NVARCHAR(200)`. **NUNCA DELETE/TRUNCATE** — siempre baja lógica `Eliminado=1`.

### Tablas EXISTENTES que se leen (NO crear nada nuevo de esto)

- **`PedidosOrdenes`** (MARKET, datos de Asana, fuente del import): `id, NroOrden, ARTCOD, AsanaTaskID, FichaTecnica` (PDF/imagen en blob), `DescripcionALT, IDEquiTalle, Tipo` (NACIONAL/IMPORTADO), `Finalizada, Estado, Auditoria, Eliminado`. Una orden = filas con el mismo `NroOrden`. **Dato desactualizado** (hace tiempo no se corre la descarga de Asana).
- **`ProdCodigosMarket`** (planilla "Importado"): `CodigoProveedor → CodigoMarket (=ARTCOD), DescripcionMarket`. **OJO**: un ARTCOD puede mapear a muchos códigos de proveedor → usar `OUTER APPLY ... TOP 1` para no multiplicar renglones.
- **`PreciosLista`** (MARKET): `Combo, PrecioDesde, PrecioHasta, Vigencia, Eliminado`. Lookup precio de venta: `SELECT TOP 1 Combo WHERE @Costo BETWEEN PrecioDesde AND PrecioHasta AND Vigencia=1 AND Eliminado=0`.
- **Dragon** `DRAGONFISH_CENTRAL.ZooLogic`: `ART` (ARTCOD, ARTDES, ARTFAB=proveedor, ATEMPORADA, FAMILIA, GRUPO, MAT), `TEMPORADA` (TCOD/TDES), `MAT` (MATCOD/MATDES), `FAMILIA` (COD/DESCRIP), `GRUPO` (COD/DESCRIP), `PROV` (CLCOD/CLNOM).
- **Telas** (módulo de Gaspar, MARKET): `TelasRollos` (un rollo por fila: IdMaterial/IdColor/IdDeposito/IdTelera/Cantidad), `TelasColores` (Codigo/Descripcion — **los códigos coinciden con los de la ficha**: 09 gris oscuro, 03 negro, 22 crudo…), `TelasMateriales`, `TelasTeleras`, `TelasDepositos`.
- **`CatalogosConfigImagenes`**: equivalencia de talle (IDEquiTalle → Descripcion).

---

## 4. Flujo por tipo

**Estado** es la columna vertebral, distinto por Tipo:
- **IMPORTADO:** `Borrador → Confirmada → Generada en Dragon`.
- **NACIONAL:** `Borrador → Ficha enviada → En producción → Pack armado → Generada en Dragon`.

**Alta NACIONAL (real):** una persona organiza con el proveedor y pasa un "machete": Tipo, Corte (origen tela, ej "Retira de lo de Lito"), Proveedor (002), Descripción (=ARTCOD), Talle, Curva (1-1-2-2), Armado (Mesa/Perchero), Precio de venta (combo), Fecha de entrega (puede ser texto), Prioridad (Urgente). El equipo carga eso + fotos. Después se completan en el sistema: **colores** (selección de rollos de Lito), **costos** (proveedor), **producción** (estimado→real al cerrar).

**Origen de datos NACIONAL:** Colores = rollos de tela del depósito de Lito (Telas). Costos = los pasa el proveedor (manual). Precio venta = combo de `PreciosLista` según costo. Armado = manual. Fotos = suben a carpeta del server.

---

## 5. La Ficha Técnica (NO la genera MarketWeb)

La ficha visual final la arman **las chicas de Diseño en Corel** (pegan las imágenes a mano). **MarketWeb solo arma los DATOS estructurados** (machete + colores/rollos + curva + costos + producción). La parte visual no se sistematiza.

Modelo de la ficha (referencia, orden 4648 IH001.097): Género (banda lateral) · Código/Nombre/Descripción/Talles · Fecha máx entrega · Observación (multi-línea, ej "Capacidad productiva: 2 semanas") · **Colores = color→código market + cantidad en ROLLOS + TOTAL rollos** · Curva de talles · **Armado (mobiliario)** · Costo textil / Costo / **Precio de venta (combo "2xNNNNN")** · Costo x atraso 1 sem (−5%) / 2 sem (−10%) sobre el Costo · **Cantidad de prendas por rollo × total rollos = ESTIMADAS** · grilla **producción color×talle (Estimadas/Recibidas)** = la planilla que manda el proveedor.

---

## 6. APIs de Dragon para Fase 2 (confirmadas, aún NO implementadas)

Secuencia: **alta de artículo → precios → PRECOMPRA**.
1. **`POST /Articulo/`** — alta. `Codigo`=ARTCOD (lo definimos nosotros, de la planilla), Descripcion, Proveedor, Familia, Material, Grupo, CategoriaDeArticulo, Clasificacion, TipodeArticulo, Temporada, Ano, Marca, Importado, **Paletadecolores**, **Curvadetalles**, **ParticipantesDetalle[]** (color×talle), InformacionAdicional{...ZADSFW...}.
2. **`POST /Preciodearticulo/`** — una por lista: `Articulo`=ARTCOD, `ListaDePrecio` (LISTA0=costo / LISTA1=venta), `PrecioDirecto`, `FechaVigencia`.
3. **PRECOMPRA** ("Alta de Presupuesto de Compra"): `Letra="X"`, `PuntoDeVenta=1`, `Numero=NroOrden`, `Proveedor`=PROV.CLCOD, `MonedaComprobante`, `FacturaDetalle[]` {Articulo=ARTCOD, Color, Talle, Cantidad, Precio}.

Patrón JWT/headers de la API Dragon: ver el servicio python `AutomatizacionRemitos` y el "Remito Nuevo" de la web. En el alta por API el comprobante queda con `SALTAFW=808601` (server).

---

## 7. Archivos del módulo

- `sql/Prod_Ordenes.sql` — DDL de referencia (el servicio igual auto-crea).
- `src/MarketWeb.Shared/Produccion/OrdenesDtos.cs` — DTOs: OrdenDto, OrdenDetalleDto, OrdenRenglonDto, OrdenSaveRequest, OrdenRenglonSaveRequest, ComboRangoDto, OrdenCabeceraCombosDto, TelaColorDto, OrdenColorDto, OrdenProduccionCeldaDto, ImportarOrdenesResultadoDto.
- `src/MarketWeb.Application/Produccion/OrdenesService.cs` — `IOrdenesService` + impl (registrado en `DependencyInjection.cs`).
- `src/MarketWeb.Api/Controllers/OrdenesController.cs` — ruta `api/ordenes` (`[Authorize(Policy="Aprobado")]`; import/eliminar = `Admin`).
- `src/MarketWeb.Client/Services/OrdenesApi.cs` — cliente (registrado en `Program.cs` del Client).
- `src/MarketWeb.Client/Pages/Produccion/Ordenes.razor` — listado + import.
- `src/MarketWeb.Client/Pages/Produccion/OrdenDetalle.razor` — ABM (cabecera + grilla + 3 modales por renglón: machete / colores-rollos / producción).
- `src/MarketWeb.Client/Layout/NavMenu.razor` — ítem "Órdenes de Pedido" en módulo Producción.
- Reusa `TelasApi` (cliente) del módulo Telas para B2.

### Endpoints (`api/ordenes`)
`GET /` listar · `GET /{id}` detalle · `POST /importar-muestra` (Admin) · `POST /importar/{nroOrden}` (Admin) · `GET /combos` (PreciosLista) · `GET /combos-cabecera` (Dragon: Temporada/Material/Familia/Subfamilia) · `GET /colores-tela` · `GET|POST /renglon/{id}/colores` · `GET|POST /renglon/{id}/produccion` · `POST /guardar` (cabecera) · `POST /renglon` (guardar renglón) · `DELETE /renglon/{id}` · `DELETE /{id}` (Admin).

---

## 8. Build / deploy

- Publish self-contained (el server no tiene .NET):
  `dotnet publish src\MarketWeb.Api\MarketWeb.Api.csproj -c Release -r win-x64 --self-contained true -o "C:\Documentos\Programación\pyhton\MarketWeb"`
- **Israel sube el build con su propia app** (copia al server + reinicia servicio). NO darle pasos de robocopy/nssm.
- No hay `appsettings.Production.json`; config por código/env vars. Connection string en user-secrets.

---

## 9. Pendientes / próximos pasos

1. **Fase 2 — Dragon por API**: las 3 llamadas (Artículo → Precio ×2 → PRECOMPRA) + vista previa "qué se va a generar" con semáforo existe/falta. Para Importado, color/talle/cant salen de los packs del viaje (`ProdViajeArticulos.PacksArmados`).
2. **Mapeo Material Dragon ↔ Telas**: hoy el filtro de rollos toma el Material de la cabecera por **nombre**; el Material de cabecera es de Dragon (MAT.MATDES) y los de Telas son `TelasMateriales` — si no coinciden, hay que elegir a mano. Definir/cargar el mapeo para que filtre solo.
3. **Consumo de stock de rollos**: B2 hoy LISTA/ELIGE del stock de Telas pero **no descuenta ni reserva** rollos. Definir si al confirmar la orden se consume.
4. **Migrar todas las órdenes** (hoy solo se importan de muestra / por número).
5. **Re-sync de Asana**: `PedidosOrdenes` está desactualizado. Plan acordado = **portar la descarga de Asana a MarketWeb** (token por `Tipo`, adjuntos, fotos) corriendo en el programador de tareas, hasta dejar de usar Asana. Las **fotos de Asana** hoy son blobs en `GoogleDriveFotosArticulos.FotoDrive` → hay que pasarlas a **archivos en el server**.
6. **Estados / acciones por estado** y generación del **documento de datos** que toma Diseño para Corel.

---

## 10. Lecciones / convenciones (importante)

- **Antes de crear una tabla/lógica nueva, revisar cómo lo hace el .Net** (`C:\Documentos\Programación\VB.Net\MARKET`) y **de qué tabla sale el dato** — casi siempre ya existe (pasó con `PreciosLista`).
- **NUNCA DELETE** en MARKET: baja lógica `Eliminado=1`.
- Auditoría: mail del usuario primero (`ClaimTypes.Email`), PC como fallback.
- Importes idempotentes (baja lógica + reinserta).
- Políticas de auth: registrar en cliente Y server (`AddAuthorizationCore`: "Aprobado" RequireClaim estado=ok, "Admin" + perfil=ADMIN).
- `bool?` en query params: "true"/"false", no "1".
- Dapper: declarar `decimal`/`int?` correcto para columnas de agregado.
