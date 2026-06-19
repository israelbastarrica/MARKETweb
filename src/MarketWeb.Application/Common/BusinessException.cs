namespace MarketWeb.Application.Common;

/// <summary>
/// Error de regla de negocio (ej: descripción duplicada). El controller lo
/// traduce a un 400 con el mensaje, igual que el MensajeAtencion del desktop.
/// </summary>
public sealed class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}
