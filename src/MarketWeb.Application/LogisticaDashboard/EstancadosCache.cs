using MarketWeb.Shared.LogisticaDashboard;

namespace MarketWeb.Application.LogisticaDashboard;

/// <summary>
/// Cache singleton para el panel 5 (artículos estancados). La query es PESADA
/// (cross-server OPENQUERY con LURO/PERALTA + #temp tables), así que se calcula
/// en background y el endpoint HTTP devuelve siempre el último valor conocido.
/// Espejo del cache de 5 min de LogisticaDashboard/queries.py.
/// </summary>
public sealed class EstancadosCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly object _lock = new();
    private List<ArticuloEstancadoDto> _rows = new();
    private DateTime _ts = DateTime.MinValue;
    private bool _refreshing;

    public (List<ArticuloEstancadoDto> Rows, DateTime Ts, bool Refreshing) Snapshot()
    {
        lock (_lock) return (_rows, _ts, _refreshing);
    }

    public bool Stale()
    {
        lock (_lock) return _rows.Count == 0 || (DateTime.Now - _ts) >= Ttl;
    }

    /// <summary>Marca refreshing=true sólo si no lo estaba ya. Devuelve true si tomó el turno.</summary>
    public bool TryBeginRefresh()
    {
        lock (_lock)
        {
            if (_refreshing) return false;
            _refreshing = true;
            return true;
        }
    }

    public void Complete(List<ArticuloEstancadoDto> rows)
    {
        lock (_lock) { _rows = rows; _ts = DateTime.Now; _refreshing = false; }
    }

    public void Fail()
    {
        lock (_lock) _refreshing = false;   // mantenemos el último valor conocido
    }
}
