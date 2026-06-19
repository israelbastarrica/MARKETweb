using System.Security.Claims;
using MarketWeb.Api.Auth;
using MarketWeb.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.WebUtilities;

var builder = WebApplication.CreateBuilder(args);

// Capa de aplicación (servicios + acceso a datos a la base MARKET).
builder.Services.AddMarketApplication();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ---- Autenticación: cookie + Google (Workspace marketarg.com) ----
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

var auth = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        // El challenge por defecto va a la cookie (devuelve 401 en la API).
        // El login con Google se dispara explícitamente desde AuthController.Login.
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "MarketWeb.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        // API: ante falta de auth devolvemos 401/403, no redirección a una página de login.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });

// Solo registramos Google si están las credenciales (así la app arranca aunque
// todavía no se haya cargado el Secret en user-secrets).
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    auth.AddGoogle(options =>
    {
        options.ClientId = googleClientId!;
        options.ClientSecret = googleClientSecret!;

        // Limitamos al dominio del Workspace (hd) y forzamos el selector de cuentas
        // (prompt=select_account) para poder cambiar de usuario en cada login.
        options.Events.OnRedirectToAuthorizationEndpoint = context =>
        {
            var extra = new Dictionary<string, string?>
            {
                ["hd"] = "marketarg.com",
                ["prompt"] = "select_account",
            };
            context.Response.Redirect(QueryHelpers.AddQueryString(context.RedirectUri, extra));
            return Task.CompletedTask;
        };

        // Validación dura del lado servidor: el mail DEBE ser @marketarg.com.
        options.Events.OnTicketReceived = context =>
        {
            var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email) || !email.EndsWith("@marketarg.com", StringComparison.OrdinalIgnoreCase))
            {
                context.HandleResponse();
                context.Response.Redirect("/?error=dominio");
            }
            return Task.CompletedTask;
        };
    });
}

// Claims de MARKET (estado/perfil/pc) en cada request autenticado.
builder.Services.AddScoped<IClaimsTransformation, MarketClaimsTransformation>();

builder.Services.AddAuthorization(options =>
{
    // Datos: requiere estar aprobado.
    options.AddPolicy("Aprobado", p => p.RequireClaim("estado", "ok"));
    // Gestión de usuarios/aprobaciones: requiere perfil ADMIN.
    options.AddPolicy("Admin", p => p.RequireClaim("estado", "ok").RequireClaim("perfil", "ADMIN"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();
