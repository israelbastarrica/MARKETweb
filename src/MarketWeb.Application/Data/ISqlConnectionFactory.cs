using Microsoft.Data.SqlClient;

namespace MarketWeb.Application.Data;

/// <summary>
/// Crea conexiones a la base MARKET. Centraliza la cadena de conexión:
/// reemplaza al cConexionSQL del desktop, pero el secreto vive SOLO en el
/// servidor (config / user-secrets), nunca viaja al navegador.
/// </summary>
public interface ISqlConnectionFactory
{
    SqlConnection Create();
}
