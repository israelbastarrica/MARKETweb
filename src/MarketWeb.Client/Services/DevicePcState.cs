namespace MarketWeb.Client.Services;

/// <summary>
/// "Esta PC": identidad del equipo FÍSICO (navegador), independiente del login.
/// Como Logística usa una cuenta compartida, cada máquina elige su nombre de PC y queda
/// guardado en el navegador (localStorage). Se manda al server por header X-Pc para los
/// remitos impresos y la auditoría. Singleton: lo cachea en memoria; lo persiste el selector.
/// </summary>
public sealed class DevicePcState
{
    public const string KeyId = "marketweb.pcId";
    public const string KeyNombre = "marketweb.pc";

    public int? PcId { get; private set; }
    public string? PcNombre { get; private set; }
    public bool Configurada => PcId is > 0 && !string.IsNullOrWhiteSpace(PcNombre);

    public event Action? OnChange;

    public void Set(int? id, string? nombre)
    {
        PcId = id;
        PcNombre = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();
        OnChange?.Invoke();
    }
}
