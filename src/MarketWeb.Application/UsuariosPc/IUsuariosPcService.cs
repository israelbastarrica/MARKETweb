using MarketWeb.Shared.UsuariosPc;

namespace MarketWeb.Application.UsuariosPc;

public interface IUsuariosPcService
{
    Task<IReadOnlyList<UsuarioPcDto>> ListarAsync(string? filtro, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListarPerfilesAsync(CancellationToken ct = default);
    Task<UsuarioPcDto?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Resuelve la fila por mail (para el login web): perfil + PC asociados.</summary>
    Task<UsuarioPcDto?> ObtenerPorMailAsync(string mail, CancellationToken ct = default);

    /// <summary>Todas las PCs físicas (máquinas) activas, para el selector "Esta PC" por dispositivo.</summary>
    Task<IReadOnlyList<UsuarioPcDto>> ListarTodasPcsAsync(CancellationToken ct = default);

    /// <summary>
    /// El usuario que no tiene PC (o no la encuentra) pide acceso eligiendo su
    /// perfil (área/local). Crea una solicitud pendiente para que el admin apruebe.
    /// </summary>
    Task SolicitarAccesoAsync(string mail, string perfil, CancellationToken ct = default);

    /// <summary>Aprueba el match mail↔PC (lo habilita el administrador).</summary>
    Task AprobarAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Resuelve el acceso de un mail: estado (onboarding/pendiente/ok) + perfil + PC.
    /// Aplica el bootstrap: si no hay ningún ADMIN aprobado, un ADMIN se auto-aprueba.
    /// </summary>
    Task<AccesoResultado> ResolverAccesoAsync(string mail, CancellationToken ct = default);
    Task<int> CrearAsync(UsuarioPcSaveRequest req, CancellationToken ct = default);
    Task ModificarAsync(int id, UsuarioPcSaveRequest req, CancellationToken ct = default);
    Task EliminarAsync(int id, CancellationToken ct = default);
}
