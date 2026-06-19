using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MarketWeb.Application.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MarketDb")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'MarketDb'. Configúrela en appsettings o con dotnet user-secrets.");
    }

    public SqlConnection Create() => new(_connectionString);
}
