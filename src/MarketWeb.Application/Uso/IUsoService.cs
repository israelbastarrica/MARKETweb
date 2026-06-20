using MarketWeb.Shared.Uso;

namespace MarketWeb.Application.Uso;

/// <summary>Estadística de uso por usuario (mail) para sugerir accesos rápidos.</summary>
public interface IUsoService
{
    /// <summary>Suma 1 a la PANTALLA usada por el mail (sin título). Upsert en RegistroUsoWeb.</summary>
    Task RegistrarAsync(string mail, string ruta, CancellationToken ct = default);

    /// <summary>Las N pantallas más usadas por el mail (por contador, desempata por recencia).</summary>
    Task<IReadOnlyList<string>> TopAsync(string mail, int n, CancellationToken ct = default);

    /// <summary>Registra el acceso a un REGISTRO puntual (ruta completa + título legible).</summary>
    Task RegistrarRegistroAsync(string mail, string ruta, string titulo, CancellationToken ct = default);

    /// <summary>Últimos registros abiertos por el mail (por recencia), para "Seguir trabajando".</summary>
    Task<IReadOnlyList<RegistroRecienteDto>> RecientesAsync(string mail, int n, CancellationToken ct = default);
}
