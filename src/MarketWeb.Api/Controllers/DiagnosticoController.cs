using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarketWeb.Api.Controllers;

// Diagnóstico de configuración del server (ADMIN). Muestra qué está viendo la app EN VIVO,
// sin exponer secretos (de la clave SMTP solo presencia + longitud).
[Authorize(Policy = "Admin")]
[ApiController]
[Route("api/[controller]")]
public sealed class DiagnosticoController : ControllerBase
{
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;
    public DiagnosticoController(IConfiguration cfg, IWebHostEnvironment env)
    {
        _cfg = cfg;
        _env = env;
    }

    [HttpGet("config")]
    public IActionResult Config()
    {
        static bool Has(string? v) => !string.IsNullOrWhiteSpace(v);

        var cs = _cfg.GetConnectionString("MarketDb") ?? "";
        var smtpHost = _cfg["Smtp:Host"] ?? "";
        var smtpPass = _cfg["Smtp:Pass"] ?? "";

        return Ok(new
        {
            environment = _env.EnvironmentName,
            smtp = new
            {
                configurado = Has(smtpHost),     // == ISmtpSender.Configurado
                host = smtpHost,                  // no es secreto
                puerto = _cfg["Smtp:Port"],
                userPresente = Has(_cfg["Smtp:User"]),
                passPresente = Has(smtpPass),
                passLargo = smtpPass.Length,      // para detectar truncado/comillas
                from = _cfg["Smtp:From"],
                fromName = _cfg["Smtp:FromName"]
            },
            marketDbConfigurada = Has(cs) && !cs.Contains("DEFINIR_EN_USER_SECRETS"),
            googleConfigurado = Has(_cfg["Authentication:Google:ClientId"]) && Has(_cfg["Authentication:Google:ClientSecret"]),
            dragonfishConfigurado = Has(_cfg["Dragonfish:User"]) && Has(_cfg["Dragonfish:Password"])
        });
    }

    // Estado liviano del SMTP para el chip de la barra (ADMIN). Sirve para ver, después de un
    // reinicio del servicio, si las variables del SMTP cargaron (si no, reiniciar hasta que levante).
    [HttpGet("smtp")]
    public IActionResult Smtp()
    {
        var host = _cfg["Smtp:Host"] ?? "";
        return Ok(new { configurado = !string.IsNullOrWhiteSpace(host), host });
    }
}
