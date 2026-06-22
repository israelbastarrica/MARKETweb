namespace MarketWeb.Client.Services;

public enum ToastTipo { Info, Exito, Error }

/// <summary>
/// Diálogos de la app (reemplazan los confirm()/alert() nativos del navegador, feos).
/// Singleton; un <UiHost/> en el layout escucha los eventos y dibuja el modal/los toasts.
/// </summary>
public sealed class UiService
{
    public sealed record ConfirmRequest(
        string Titulo, string Mensaje, string Aceptar, string Cancelar, bool Peligro,
        TaskCompletionSource<bool> Tcs);

    public sealed record ToastItem(Guid Id, string Mensaje, ToastTipo Tipo);

    public event Action<ConfirmRequest>? OnConfirm;
    public event Action<ToastItem>? OnToast;

    /// <summary>Confirmación in-app. Devuelve true si el usuario acepta.</summary>
    public Task<bool> ConfirmAsync(string mensaje, string titulo = "Confirmar",
        string aceptar = "Aceptar", string cancelar = "Cancelar", bool peligro = false)
    {
        var req = new ConfirmRequest(titulo, mensaje, aceptar, cancelar, peligro, new TaskCompletionSource<bool>());
        if (OnConfirm is null) req.Tcs.TrySetResult(false);
        else OnConfirm.Invoke(req);
        return req.Tcs.Task;
    }

    public void Toast(string mensaje, ToastTipo tipo = ToastTipo.Info)
        => OnToast?.Invoke(new ToastItem(Guid.NewGuid(), mensaje, tipo));

    public void Exito(string mensaje) => Toast(mensaje, ToastTipo.Exito);
    public void Error(string mensaje) => Toast(mensaje, ToastTipo.Error);
}
