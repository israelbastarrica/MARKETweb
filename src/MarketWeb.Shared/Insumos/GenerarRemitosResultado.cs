namespace MarketWeb.Shared.Insumos;

/// <summary>Resultado de generar los remitos de insumos (uno por local) en Dragonfish.</summary>
public sealed class GenerarRemitosResultado
{
    public List<RemitoLocalResultado> Locales { get; set; } = new();
    public int Total => Locales.Count;
    public int Ok => Locales.Count(l => l.Ok);
}

/// <summary>Resultado de un remito por local.</summary>
public sealed class RemitoLocalResultado
{
    public string Local { get; set; } = "";
    public int Pedidos { get; set; }
    public int Articulos { get; set; }
    public int Cantidad { get; set; }
    public bool Ok { get; set; }
    public string Comprobante { get; set; } = "";   // código/número del remito creado
    public string Error { get; set; } = "";
}
