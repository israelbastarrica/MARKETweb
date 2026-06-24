namespace MarketWeb.Client.Services;

/// <summary>
/// Estado de layout compartido (colapso del menú lateral) entre MainLayout y las páginas.
/// Permite que, por ejemplo, "Pantalla ancha" en Remito nuevo colapse el menú para ganar espacio.
/// </summary>
public sealed class LayoutState
{
    public bool MenuColapsado { get; private set; }

    public event Action? OnChange;

    public void SetMenuColapsado(bool valor)
    {
        if (valor == MenuColapsado) return;
        MenuColapsado = valor;
        OnChange?.Invoke();
    }

    public void Toggle() => SetMenuColapsado(!MenuColapsado);
}
