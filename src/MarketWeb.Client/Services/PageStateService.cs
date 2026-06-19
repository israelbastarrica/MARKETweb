namespace MarketWeb.Client.Services;

/// <summary>
/// Guarda el estado de una pantalla (filtros + datos cargados + scroll) para restaurarlo
/// al volver, sin perder el hilo. Singleton: vive mientras dura la app (SPA).
/// Cada pantalla guarda su estado al navegar afuera (RegisterLocationChangingHandler) y
/// lo restaura en OnInitialized.
/// </summary>
public sealed class PageStateService
{
    private readonly Dictionary<string, object> _state = new();
    private readonly Dictionary<string, double> _scroll = new();

    public void Save(string key, object state, double scroll)
    {
        _state[key] = state;
        _scroll[key] = scroll;
    }

    public bool TryGet<T>(string key, out T state, out double scroll) where T : class
    {
        scroll = _scroll.TryGetValue(key, out var s) ? s : 0;
        if (_state.TryGetValue(key, out var o) && o is T t) { state = t; return true; }
        state = null!;
        return false;
    }

    public void Clear(string key)
    {
        _state.Remove(key);
        _scroll.Remove(key);
    }
}
