# MarketWeb — Guía para el agente (onboarding)

> Esta guía explica **cómo está construida la aplicación**, las **convenciones obligatorias**, el **modelo de datos**, y **qué se puede y qué no**. Está pensada para sumarse a desarrollar el módulo **Diseño** (y lo que falte). Leela entera antes de tocar código.

---

## 1. Qué es MarketWeb

Reescritura **web** del sistema de escritorio **MARKET** (VB.NET WinForms, ~130 forms, en `C:\Documentos\Programación\VB.Net\MARKET`). No es un *port* línea por línea: es una **reescritura** que **reusa los Stored Procedures** y la base existente, pero con UI y backend nuevos.

- **Repo:** `C:\Documentos\Programación\VB.Net\MARKET-Web` (GitHub: https://github.com/israelbastarrica/MARKETweb).
- **Base de datos:** `MARKET` en SQL Server (la misma del desktop). Dragonfish (el ERP) se accede por **linked servers** `DRAGONFISH_CENTRAL`, `DRAGONFISH_LURO`, `DRAGONFISH_PERALTA`, `DRAGONFISH_CCENTRAL`.
- **Idioma del equipo:** español. Nombres de clases/SPs/columnas suelen estar en español o como en Dragonfish (mayúsculas, ej. `ARTCOD`).

---

## 2. Stack y estructura de la solución

- **.NET 9**, **Blazor WebAssembly** *hosted* por **ASP.NET Core**, **Dapper** + **Microsoft.Data.SqlClient**, **Bootstrap 5** + **Font Awesome**.
- Proyectos (`src/`):
  - **`MarketWeb.Api`** — host ASP.NET Core: sirve la SPA, expone los controllers REST, autenticación (cookie + Google OAuth), hosted services. `Program.cs` arma todo.
  - **`MarketWeb.Application`** — lógica de negocio: un **servicio por feature** (`IXxxService` + `XxxService`) que habla con la base por Dapper. Registro en `DependencyInjection.cs`.
  - **`MarketWeb.Client`** — Blazor WASM: páginas (`Pages/`), componentes (`Components/`), servicios HTTP cliente (`Services/`), layout (`Layout/`), util (`Util/`).
  - **`MarketWeb.Shared`** — DTOs compartidos entre Api y Client (un namespace por feature).

### Flujo de una request
`Página .razor` → `XxxApi` (cliente HTTP, `Services/`) → `XxxController` (Api) → `IXxxService` (Application, Dapper) → **SQL Server**. Los **DTOs** viajan por todo el camino (proyecto Shared).

---

## 3. Convenciones OBLIGATORIAS (no negociables)

Estas reglas se ganaron a fuerza de correcciones. Respetalas siempre:

1. **Nunca `DELETE`/`TRUNCATE` en MARKET.** Borrado **lógico**: `Eliminado = 1` (y filtrar `WHERE Eliminado = 0`). Vale para toda la base.
2. **Diálogos: usar `UiService`, nunca `confirm()`/`alert()` nativos.** `@inject UiService Ui` → `await Ui.ConfirmAsync(msg, titulo, aceptar, cancelar, peligro: true)` para confirmaciones (el modal rojo es para acciones destructivas) y `Ui.Toast/Exito/Error(msg)` para avisos. El `<UiHost/>` ya está en `MainLayout`.
3. **`ArticuloLink` siempre que se muestre un ARTCOD.** `<ArticuloLink Codigo="@x" />` (con `Badge="false"` si el código ya se ve al lado). Da el ícono que abre la ficha del artículo (foto + datos).
4. **Copiar el .Net fiel primero.** Al portar una pantalla, la **primera versión replica el layout/UX del form de WinForms** (leé el `.Designer.vb` correspondiente en `C:\...\VB.Net\MARKET`). No inventar diseño. Mejoras, después, con OK del usuario. En mobile/tablet sí se compacta.
5. **Mantener estado al volver.** Toda pantalla con filtros + consulta de la que se navega afuera (ej. ficha de artículo) debe **conservar filtros + resultados + scroll al volver**, vía `PageStateService` (`Save`/`TryGet<T>` + clase Snap + `IDisposable`). Mirá `ConsultaReposicion.razor` o `ReporteArticulos.razor` como ejemplo.
6. **Dapper: `decimal` (no `int`) para columnas de `SUM()`/agregados** en records/POCO, o tira excepción de materialización en runtime.
7. **Secretos: SOLO en env/user-secrets, NUNCA en el repo ni hardcodeados.** Cadenas de conexión, claves de Dragonfish, SMTP, Google. En el server van como **variables de entorno de Sistema** (ver §7).
8. **No abrir/editar `wwwroot/index.html`** salvo necesidad real (dispara un preview molesto). Para JS, usá módulos importados por interop (ej. `wwwroot/js/mapa.js`).
9. **Auditoría:** en campos de "quién hizo qué" usar el **mail** del usuario (SSO) primero; nombre de PC solo como fallback.

---

## 4. Cómo agregar una pantalla nueva (patrón)

Ejemplo concreto a seguir: la migración del feature **Mapa** (`MapaController`, `MapaService`, `MapaDtos`, `Pages/Logistica/Mapa.razor`).

1. **DTOs** en `MarketWeb.Shared/<Feature>/` (request + filas/resultado). Si el JSON lo consume JS (no Blazor), cuidá el casing con `[JsonPropertyName]`.
2. **Servicio** en `MarketWeb.Application/<Feature>/`: `IXxxService` + `XxxService` (inyecta `ISqlConnectionFactory _db`; `await using var cn = _db.Create(); await cn.OpenAsync(ct);` y Dapper).
3. **Registrar** en `MarketWeb.Application/DependencyInjection.cs`: `services.AddScoped<IXxxService, XxxService>();`.
4. **Controller** en `MarketWeb.Api/Controllers/`: `[Authorize(Policy="Aprobado")]` (o `"Admin"`), `[ApiController]`, `[Route("api/[controller]")]`, inyecta el servicio.
5. **Cliente HTTP** en `MarketWeb.Client/Services/XxxApi.cs` (usa `HttpClient`, `GetFromJsonAsync`/`PostAsJsonAsync`); registralo en `Client/Program.cs`.
6. **Página** en `MarketWeb.Client/Pages/<Modulo>/Xxx.razor` con `@page "/..."`. Usá `<PageHeader Modulo="..." Titulo="..." Icono="..." />`, las clases `market-page`/`market-card`/`market-table`, `UiService`, y `ArticuloLink` donde haya ARTCOD.
7. **Menú:** en `Layout/NavMenu.razor`, cambiá el ítem de `/wip/...` a la ruta real con `Implementado: true`.
8. Si necesitás opciones de filtro (combos), poné un endpoint tipo `combos` que traiga los `DISTINCT` de Dragonfish (ver `MapaService.CombosAsync`).

Los `@using` de los namespaces Shared están centralizados en `Client/_Imports.razor` (agregá el nuevo ahí).

---

## 5. Autenticación y autorización

- **Login:** Google SSO restringido a `@marketarg.com` (cookie + OAuth, configurado en `Api/Program.cs`).
- **Gating:** toda la app está envuelta en `<AuthorizeView>` (`App.razor`). Estados del usuario: `onboarding` (recién entra, se autoasigna), `pendiente` (espera aprobación) y `ok` (habilitado). Las páginas **no** necesitan `[Authorize]` propio.
- **Policies** (en `Program.cs`):
  - **`Aprobado`** = `estado == ok` (la mayoría de los endpoints).
  - **`Admin`** = `estado == ok` + `perfil == ADMIN`.
- **Aprobación:** tabla `MARKET.dbo.UsuariosPC` (mail + PC + perfil + estado + MailAprobado). ADMIN aprueba en **Configuración → Usuarios**.
- **"Esta PC"** (identidad del equipo físico): es **por navegador** (localStorage `marketweb.pc`/`marketweb.pcId` + header `X-Pc`), separado del login, porque hay cuentas compartidas (ej. Logística en varias PCs). El selector (`EstaPcSelector`) elige de la lista de PCs de `UsuariosPC`. Se usa para rutear impresoras, etc.

---

## 6. Datos: base, Dragonfish y SPs

### Acceso
- Siempre vía `ISqlConnectionFactory` (`_db.Create()`) + **Dapper**. La cadena de conexión vive en config (`ConnectionStrings:MarketDb`), nunca en código.
- **Dragonfish** (catálogos, stock, comprobantes) se lee con prefijo de linked server: `DRAGONFISH_CENTRAL.ZooLogic.<TABLA>`.

### ⚠️ Réplicas vs. EN VIVO
Las réplicas `DRAGONFISH_LURO`/`DRAGONFISH_PERALTA` están **atrasadas ~2 días**. Para datos que tienen que estar al día (ej. venta del día) se lee **EN VIVO** por `OPENQUERY([host], '...')` contra el local (`marketluro.ddns.net` / `marketperalta.ddns.net`), con chequeo `HostVivo` (TCP 1433) y *fallback* a la réplica si el local no responde. Ver `LogisticaDashboardService` y `ReposicionService`.

### Tablas clave (MARKET.dbo)
- **`Mapeo`** + **`MapeoRegistro`**: ubicaciones del depósito (Modulo, Pasillo, Fila, Posicion, Mobiliario, IDUbicacion) y qué artículo/palet hay en cada una. `IDUbicacion = 1` = depósito CENTRAL. *(En `Mapeo`, las columnas `Coord*` NO se usan.)*
- **`Ubicaciones`**: catálogo de ubicaciones (locales/depósitos).
- **`Palets`** / **`PaletsDetalle`**: armado de palets.
- **`UsuariosPC`**: login/aprobación + catálogo de PCs.
- **`InformeColumnas`**: configuración de columnas visibles por **formulario + PC** (para grillas personalizables). Campos: Formulario, Columna, PC, Visible, Eliminado, Auditoria.
- **`RemitoRecepcion`** / **`ImpresorRemito_Cola`**: recepción de remitos y cola de impresión.
- **`TareasProgramadas`** / **`TareasProgramadasLog`**: el programador propio (ver §8).
- **`Reposicion`** y familia (`RepoResto`, `RepoReemplazos`, `EventosReposicion`, etc.): el motor de reposición. **El SP `SP_RepoCalcularPacks` NO se toca desde MarketWeb** (lo maneja otro agente — ver §10).

### Catálogos de Dragonfish (clave para **Diseño**)
En `DRAGONFISH_CENTRAL.ZooLogic`:
- **`ART`** (artículos): `ARTCOD`, `ARTDES`, `MARCA`, `TIPOARTI`, `CATEARTI`, `FAMILIA`, `ATEMPORADA`, `CLASIFART`, `ANO`, `ARTFAB` (cód. proveedor), `ARIMAGEN`, etc.
- **`TIPOART`** (`COD`/`DESCRIP`), **`CATEGART`** (`COD`/`DESCRIP`), **`FAMILIA`** (`COD`/`DESCRIP`), **`TEMPORADA`** (`TCOD`/`TDES`), **`PROV`** (`CLCOD`/`CLNOM`). El "Combo" sale de `ART.CLASIFART` directo.
- **`COMPROBANTEV`** / **`COMPROBANTEVDET`**: comprobantes (remitos `FLETRA='R'`, ventas, etc.). Campos de auditoría del alta: `FALTAFW` (fecha), `HALTAFW` (hora), `UALTAFW` (usuario), **`SALTAFW`** (terminal/licencia que dio el alta — define a qué impresora va).

Cómo poblar combos: `SELECT DISTINCT ... FROM ...ART LEFT JOIN ...<catálogo> ...` (ver `MapaService.CombosAsync`).

---

## 7. Build, publish y deploy

- **Compilar:** `dotnet build src/MarketWeb.Api/MarketWeb.Api.csproj -c Debug`.
- **Publicar:** `dotnet publish src/MarketWeb.Api/MarketWeb.Api.csproj -c Release -r win-x64 --self-contained true -o C:\Documentos\Programación\pyhton\MarketWeb`.
- ⚠️ **El server es OTRA máquina.** El `publish` deja los archivos en una carpeta local; el deploy al server (copiar + reiniciar el servicio) lo hace **Israel**. La app del server corre en **Production**.
- **Secretos en el server:** van como **Variables de entorno de Sistema** (no de usuario — el servicio corre con otra cuenta), con doble guión bajo: `Smtp__Host`, `Dragonfish__User`, etc. **Se leen al arrancar → hay que reiniciar** la app tras cambiarlas.
- **Diagnóstico:** `GET /api/diagnostico/config` (ADMIN) muestra qué ve la app en vivo (environment + si Smtp/MarketDb/Google/Dragonfish están configurados), sin exponer secretos. Útil cuando algo "no anda" en el server.
- **Commits:** terminar el mensaje con `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. Los here-strings de git no deben contener comillas dobles.

---

## 8. Integraciones

- **API Dragonfish:** `DragonfishService` da de alta remitos por API REST (JWT HMAC-SHA256, headers `Authorization`/`IdCliente`/`BaseDeDatos`). Credenciales en config `Dragonfish:*`. Ejemplo de uso: pantalla **Remito Nuevo** (tablet) y **Probar Remito Dragon** (Sistemas).
- **Programador de tareas:** `TareasRunner`/`TareasService` (Sistemas → Tareas, ADMIN) = `BackgroundService` que cada 60s dispara tareas vencidas (reemplaza la tarea de Windows). Tipos: `REPOSICION` (SP + PDF + mail) y `BACKUP` (BACKUP DATABASE → .rar). Tablas autocreadas.
- **Agente de impresión** (python, `C:\...\pyhton`): imprime remitos y confirma recepción contra Dragonfish. **Es de Israel — no se toca desde acá.**
- **PDFs:** `PdfSharpCore` (ej. cuadernillo de reposición). Para dibujar tablas: una sola línea por celda, recortada con `…` al ancho (no usar wrap, que desborda).

---

## 9. Estado del proyecto y FOCO: módulo Diseño

Lo migrado figura en `NavMenu.razor` con `Implementado: true`. Lo que arranca con `/wip/...` **falta**. Módulos hechos: Configuración, Compras, Locales, Logística, Administración, Sistemas (en buena parte).

**Tu tarea: el módulo `Diseño`** (todas `/wip/diseno/...`, falta hacerlas):
- **Órdenes de Pedido** (`/wip/diseno/ordenes-pedido`)
- **Artículos** (`/wip/diseno/articulos`)
- **Catálogos** (`/wip/diseno/catalogos`)
- **Config Canva** (`/wip/diseno/config-canva`)
- **Config Imágenes** (`/wip/diseno/config-imagenes`)
- **Mapa** (`/wip/diseno/mapa`)

Para cada una: encontrá el **form equivalente en el .Net** (`C:\Documentos\Programación\VB.Net\MARKET`, buscá los `frm*` de Diseño), leé su `.Designer.vb` y su lógica, y **replicá fiel** (regla §3.4) siguiendo el patrón de §4. La data sale mayormente de los catálogos de Dragonfish (`ART` y tablas relacionadas, §6) y de tablas propias de MARKET. Ante la duda de negocio (qué hace exactamente una pantalla), **preguntá** — no inventes reglas.

> Otros módulos `/wip/` (Informes, Producción) NO son tu alcance salvo que te lo pidan.

---

## 10. Qué PODÉS y qué NO

**Podés:**
- Crear features nuevos siguiendo el patrón de §4 y las convenciones de §3.
- Leer cualquier tabla de MARKET y de los catálogos de Dragonfish.
- Tocar el SP del SCHED solo si te lo piden explícitamente; en general, crear SPs/queries nuevos para Diseño está OK (consultá si dudás del impacto).

**NO debés:**
- ❌ Hacer `DELETE`/`TRUNCATE` (borrado lógico, §3.1).
- ❌ Tocar **`SP_RepoCalcularPacks`** ni el motor de reposición — **lo maneja otro agente (SQL)**. Coordiná con Israel.
- ❌ Tocar el **agente python** de impresión/recepción (es de Israel).
- ❌ Poner secretos en el repo / hardcodear credenciales (§3.7).
- ❌ Editar `wwwroot/index.html` (§3.8).
- ❌ Usar `confirm`/`alert` nativos (§3.2) ni inventar diseño antes de replicar el .Net (§3.4).
- ❌ Asumir que "publish = deployado": el deploy al server lo hace Israel (§7).

---

## 11. Dónde mirar para aprender el código

- **Patrón completo de un feature:** `Mapa` (Application/Mapa, Api/Controllers/MapaController, Client/Pages/Logistica/Mapa.razor) o cualquier feature de Logística.
- **Grilla con filtros + mantener estado + ArticuloLink:** `ReporteArticulos.razor`, `ConsultaReposicion.razor`.
- **Combos desde Dragonfish:** `MapaService.CombosAsync`.
- **Diálogos/UX:** `Components/UiHost.razor` + `Services/UiService.cs`.
- **Auth:** `App.razor`, `Program.cs` (policies), `UsuariosPc*`.
- **El desktop original (fuente de verdad de negocio):** `C:\Documentos\Programación\VB.Net\MARKET` — los `frm*.vb` + `frm*.Designer.vb`.

Ante cualquier duda de negocio o de alcance: **preguntá a Israel** antes de avanzar.
