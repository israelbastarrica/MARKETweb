namespace MarketWeb.Shared.Dashboard;

/// <summary>Resumen de ventas del día para la vista mobile del dashboard de Locales.</summary>
public sealed class DashboardVentasMobileDto
{
    public string Role { get; set; } = "";          // admin (LURO+PERALTA) | cajero (1 local)
    public List<VentaLocalResumenDto> Locales { get; set; } = new();
    public string Actualizado { get; set; } = "";
}

public sealed class VentaLocalResumenDto
{
    public string Local { get; set; } = "";
    public decimal Monto { get; set; }
    public int Tickets { get; set; }
    public decimal Prendas { get; set; }
    public List<TopArticuloDto> TopArticulos { get; set; } = new();
    public List<CajeroTicketsDto> Cajeros { get; set; } = new();

    // Proyección (para la vista cajero): tickets proyectados del día y los
    // acumulados hasta la hora actual (para saber si va adelantado o atrasado).
    // null si no hay proyección cargada.
    public int? ProyTicketsDia { get; set; }
    public int? ProyTicketsAhora { get; set; }
}

public sealed class TopArticuloDto
{
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal Monto { get; set; }
}

public sealed class CajeroTicketsDto
{
    public string Nombre { get; set; } = "";
    public int Tickets { get; set; }
}
