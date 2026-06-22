namespace MarketWeb.Client.Services;

/// <summary>
/// Agrega el header X-Pc / X-Pc-Id (la PC física de ESTE navegador) a cada request del API,
/// para que el server sepa desde qué equipo se opera (remitos impresos, auditoría) aun con
/// cuenta compartida. El valor lo mantiene DevicePcState en memoria.
/// </summary>
public sealed class DevicePcHandler : DelegatingHandler
{
    private readonly DevicePcState _state;
    public DevicePcHandler(DevicePcState state) => _state = state;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_state.PcId is int id && id > 0)
        {
            request.Headers.Remove("X-Pc-Id");
            request.Headers.TryAddWithoutValidation("X-Pc-Id", id.ToString());
            if (!string.IsNullOrEmpty(_state.PcNombre))
            {
                request.Headers.Remove("X-Pc");
                request.Headers.TryAddWithoutValidation("X-Pc", _state.PcNombre);
            }
        }
        return base.SendAsync(request, ct);
    }
}
