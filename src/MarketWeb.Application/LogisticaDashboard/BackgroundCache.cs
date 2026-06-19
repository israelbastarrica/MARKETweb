namespace MarketWeb.Application.LogisticaDashboard;

/// <summary>
/// Cache singleton genérico con refresh en background. Para paneles cuya query es
/// PESADA (cross-server OPENQUERY): el endpoint HTTP devuelve siempre el último
/// valor conocido y dispara el recálculo aparte. Espejo del cache del Dash.
/// </summary>
public sealed class BackgroundCache<T> where T : class
{
    private readonly object _lock = new();
    private T? _value;
    private DateTime _ts = DateTime.MinValue;
    private bool _refreshing;

    public TimeSpan Ttl { get; init; } = TimeSpan.FromMinutes(3);

    public (T? Value, DateTime Ts, bool Refreshing) Snapshot()
    {
        lock (_lock) return (_value, _ts, _refreshing);
    }

    public bool Stale()
    {
        lock (_lock) return _value is null || (DateTime.Now - _ts) >= Ttl;
    }

    /// <summary>Marca refreshing=true sólo si no lo estaba. Devuelve true si tomó el turno.</summary>
    public bool TryBeginRefresh()
    {
        lock (_lock)
        {
            if (_refreshing) return false;
            _refreshing = true;
            return true;
        }
    }

    public void Complete(T value)
    {
        lock (_lock) { _value = value; _ts = DateTime.Now; _refreshing = false; }
    }

    public void Fail()
    {
        lock (_lock) _refreshing = false;   // mantenemos el último valor conocido
    }
}
