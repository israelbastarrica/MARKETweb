using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MarketWeb.Shared.Dragonfish;
using Microsoft.Extensions.Configuration;

namespace MarketWeb.Application.Dragonfish;

/// <summary>
/// Cliente de la API Dragonfish (api.Dragonfish). Replica el JWT HMAC-SHA256 del agente python
/// (AutomatizacionRemitos) y crea el Remito de venta CENTRAL→local de insumos. Credenciales en
/// config (user-secrets/env, sección Dragonfish), NUNCA en el repo.
/// </summary>
public sealed class DragonfishService : IDragonfishService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public DragonfishService(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
    }

    private string Base => (_cfg["Dragonfish:BaseCentral"] ?? "").TrimEnd('/');
    private string User => _cfg["Dragonfish:User"] ?? "";
    private string Password => _cfg["Dragonfish:Password"] ?? "";
    private string Clave => _cfg["Dragonfish:Clave"] ?? "";
    private string IdCliente => _cfg["Dragonfish:IdCliente"] ?? "";

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(Base) && !string.IsNullOrWhiteSpace(User) &&
        !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(Clave) && !string.IsNullOrWhiteSpace(IdCliente);

    public Task<DragonRemitoResultDto> CrearRemitoAsync(DragonRemitoRequest req, CancellationToken ct = default)
        => CrearRemitoConExtrasAsync(req, null, ct);

    /// <summary>
    /// Igual que CrearRemitoAsync, pero permite inyectar campos extra de primer nivel en el body (diagnóstico:
    /// probar nombres de campos de "observaciones" para ver cuál persiste Dragon en COMPROBANTEV). extras=null = alta normal.
    /// </summary>
    public async Task<DragonRemitoResultDto> CrearRemitoConExtrasAsync(DragonRemitoRequest req, IDictionary<string, object?>? extras, CancellationToken ct = default)
    {
        if (!Configurado)
            return new DragonRemitoResultDto { Ok = false, Error = "La API Dragonfish no está configurada (faltan credenciales en el servidor)." };

        var body = new Dictionary<string, object?>
        {
            ["Letra"] = "R",
            ["PuntoDeVenta"] = 1,
            ["Cliente"] = (req.Local ?? "").Trim().ToUpperInvariant(),
            ["Motivo"] = string.IsNullOrWhiteSpace(req.Motivo) ? "13" : req.Motivo.Trim(),
            ["MonedaComprobante"] = "PESOS",
            ["ListaDePrecios"] = "LISTA1",
            ["MercaderiaConsignacion"] = false,
            ["Vendedor"] = "",
            ["ForPago"] = "",
            // InformacionAdicional/ZADSFW NO persiste por la API (probado: 0/70). El campo que SÍ existe en
            // el Swagger y persiste en COMPROBANTEV.FOBS es "Obs": ahí mandamos la licencia/terminal del tablet
            // para que el agente rutee la impresora leyendo FOBS. (ZADSFW queda por las dudas, no molesta.)
            ["InformacionAdicional"] = new { ZADSFW = (req.InformacionAdicional ?? "").Trim() },
            ["Obs"] = (req.InformacionAdicional ?? "").Trim(),
            ["FacturaDetalle"] = req.Items.Select(i => new
            {
                Articulo = (i.Articulo ?? "").Trim(),
                Color = (i.Color ?? "").Trim(),
                Talle = (i.Talle ?? "").Trim(),
                Cantidad = i.Cantidad,
                Precio = 0
            }).ToList()
        };

        // Campos candidatos para el test (los desconocidos por el contrato WCF se ignoran sin error).
        if (extras is not null)
            foreach (var kv in extras) body[kv.Key] = kv.Value;

        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true });
        var result = new DragonRemitoResultDto { JsonEnviado = json };

        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"{Base}/Remito/")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            msg.Headers.TryAddWithoutValidation("Authorization", MakeJwt());
            msg.Headers.TryAddWithoutValidation("IdCliente", IdCliente);
            msg.Headers.TryAddWithoutValidation("BaseDeDatos", "CENTRAL");
            msg.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var resp = await _http.SendAsync(msg, ct);
            result.HttpStatus = (int)resp.StatusCode;
            result.Respuesta = await resp.Content.ReadAsStringAsync(ct);
            result.Ok = resp.IsSuccessStatusCode;
            TryParseCodigoNumero(result);
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Error = ex.Message;
        }
        return result;
    }

    // JWT HMAC-SHA256, idéntico a api_dragonfish.py::_make_jwt.
    private string MakeJwt()
    {
        var claveBytes = Encoding.UTF8.GetBytes(Clave);

        using var h = new HMACSHA256(claveBytes);
        var pwHash = Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(Password))).ToLowerInvariant();

        var header = B64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payloadJson = JsonSerializer.Serialize(new
        {
            exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600,
            usuario = User,
            password = pwHash
        });
        var payload = B64Url(Encoding.UTF8.GetBytes(payloadJson));

        using var hSig = new HMACSHA256(claveBytes);
        var sig = B64Url(hSig.ComputeHash(Encoding.UTF8.GetBytes($"{header}.{payload}")));

        return $"{header}.{payload}.{sig}";
    }

    private static string B64Url(byte[] b)
        => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void TryParseCodigoNumero(DragonRemitoResultDto r)
    {
        if (string.IsNullOrWhiteSpace(r.Respuesta)) return;
        try
        {
            using var doc = JsonDocument.Parse(r.Respuesta);
            var root = doc.RootElement;
            foreach (var p in root.EnumerateObject())
            {
                if (p.NameEquals("Codigo") && p.Value.ValueKind == JsonValueKind.String) r.Codigo = p.Value.GetString();
                else if (p.NameEquals("Numero") && p.Value.ValueKind == JsonValueKind.Number) r.Numero = p.Value.GetInt64();
            }
        }
        catch { /* la respuesta puede no ser JSON; queda en Respuesta cruda */ }
    }
}
