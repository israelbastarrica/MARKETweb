using MarketWeb.Shared.Despachos;

namespace MarketWeb.Application.Despachos;

/// <summary>
/// Despachos / control de remitos hacia el local (espejo de frmRepoControlRemitosLocal).
/// Listado read-only vía SP_RemitosControlListado; el escaneo de recepción lo hace la app móvil.
/// </summary>
public interface IDespachosService
{
    Task<IReadOnlyList<DespachoLocalDto>> ListarLocalesAsync(CancellationToken ct = default);

    /// <summary>Resuelve el IDLocal (Ubicaciones.ID) a partir del nombre/perfil. 0 si no es un local.</summary>
    Task<int> ResolverIdLocalAsync(string? nombre, CancellationToken ct = default);

    Task<IReadOnlyList<DespachoDto>> ListarAsync(DateTime desde, DateTime hasta, int? idLocalDestino, CancellationToken ct = default);

    Task<IReadOnlyList<DespachoArticuloDto>> ListarArticulosAsync(string remitoId, string origen, CancellationToken ct = default);

    /// <summary>
    /// Valida y prepara el QR de pantalla de un remito (etiqueta rota). Devuelve el código a
    /// codificar (con "-PC" si esPc) o un error. Loguea en RemitoQRGenerado_Log. NO genera el PNG
    /// (eso lo hace la capa Api con QRCoder).
    /// </summary>
    Task<QrRemitoDto> PrepararQrAsync(string remitoCodigo, bool esPc, int idLocalUsuario, string? localUsuario, string machineName, CancellationToken ct = default);
}
