# MARKET Web

Migración progresiva del sistema MARKET (desktop WinForms VB.NET) a una aplicación web.

## Stack

- **.NET 9**
- **MarketWeb.Api** — ASP.NET Core Web API. Única que abre conexión a SQL Server. También hostea el cliente Blazor.
- **MarketWeb.Client** — Blazor WebAssembly (UI).
- **MarketWeb.Application** — lógica de negocio + acceso a datos (Dapper). Reemplaza lo que en el desktop vivía en los `frm*` + `Globales.vb`.
- **MarketWeb.Shared** — DTOs compartidos entre cliente y servidor.

```
Navegador (Blazor WASM)  ──HTTPS/JSON──►  MarketWeb.Api  ──►  MarketWeb.Application  ──►  SQL Server MARKET
```

## Configurar la conexión a la base

La contraseña **no** se versiona. Definir la cadena con user-secrets (sólo en tu máquina):

```powershell
dotnet user-secrets set "ConnectionStrings:MarketDb" "Server=TU_SERVIDOR;Database=MARKET;User Id=MARKET;Password=LA_PASS;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=True" --project src/MarketWeb.Api
```

En producción se usa una variable de entorno o Azure Key Vault, nunca el `appsettings.json`.

## Configurar el login (Google SSO)

El login es con Google (cuentas **@marketarg.com**). Las credenciales del cliente OAuth **no** se versionan; se cargan con user-secrets:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "EL_CLIENT_ID.apps.googleusercontent.com" --project src/MarketWeb.Api
dotnet user-secrets set "Authentication:Google:ClientSecret" "EL_CLIENT_SECRET" --project src/MarketWeb.Api
```

Las credenciales salen del cliente OAuth 2.0 del proyecto en **Google Cloud Console** (APIs y servicios → Credenciales). Para correr en local, ese cliente OAuth tiene que tener:
- **URI de redireccionamiento autorizado**: `http://localhost:5080/signin-google` (la callback es siempre `/signin-google`; ajustar el puerto si arrancás en otro).
- **Origen JavaScript autorizado**: `http://localhost:5080`.

Recomendado para dev: crear un cliente OAuth "Web" aparte (con el redirect de localhost) en vez de usar el de producción. Sin estas claves, la app arranca pero el botón "Entrar con Google" no funciona. Tras el primer login, la cuenta queda **pendiente de aprobación** (Sistemas → Usuarios; un ADMIN la aprueba).

## Correr

```powershell
dotnet run --project src/MarketWeb.Api --urls http://localhost:5080
```

Abrir http://localhost:5080. La SPA se sirve desde la misma API.

## Estructura modular (cómo agregar una pantalla)

Cada feature toca su carpeta en las cuatro capas. Ejemplo, el feature **Locales**:

| Capa | Archivo |
|------|---------|
| Shared | `Locales/LocalDto.cs`, `LocalTipoDto.cs`, `LocalSaveRequest.cs` |
| Application | `Locales/ILocalesService.cs`, `LocalesService.cs` (+ registrar en `DependencyInjection.cs`) |
| Api | `Controllers/LocalesController.cs` |
| Client | `Services/LocalesApi.cs`, `Pages/Configuracion/Locales.razor` (+ link en `Layout/NavMenu.razor`) |

## Estado

- [x] Shell con el menú de `frmPrincipal` (9 módulos, RRHH queda en el desktop).
- [x] **Configuración → Locales** (lista + filtro + ABM + baja lógica). Espejo de `frmRepoLocales` + `frmABMLocales`.
- [ ] Resto de pantallas (marcadas con `·` en el menú → placeholder "en construcción").

## Convenciones heredadas de MARKET

- **Nunca DELETE físico**: baja lógica con `Eliminado = 1`.
- Consultas **siempre parametrizadas** (el desktop concatenaba strings).
- Campo `Auditoria` con formato `Acción | origen | fecha`.
