namespace MarketWeb.Application.Uso;

/// <summary>Estadística de uso por usuario (mail) para sugerir accesos rápidos.</summary>
public interface IUsoService
{
    /// <summary>Suma 1 a la ruta usada por el mail (upsert en RegistroUsoWeb).</summary>
    Task RegistrarAsync(string mail, string ruta, CancellationToken ct = default);

    /// <summary>Las N rutas más usadas por el mail (por contador, desempata por recencia).</summary>
    Task<IReadOnlyList<string>> TopAsync(string mail, int n, CancellationToken ct = default);
}
