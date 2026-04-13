using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
namespace BusinessJournal.Infrastructure.Data.SqlServer;

public sealed class SqlServerConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(IOptions<SqlServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _connectionString = options.Value.ConnectionString;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new ArgumentException("SqlServer:ConnectionString is required.");
        }
    }

    public SqlConnection CreateOpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}