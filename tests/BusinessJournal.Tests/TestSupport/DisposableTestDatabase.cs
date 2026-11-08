using System.Net;
using Microsoft.Data.SqlClient;

namespace BusinessJournal.Tests.TestSupport;

/// <summary>
/// Fail-closed guard for every test that connects to SQL Server. Several of those tests
/// delete all customers and appointments, so they must never reach a database with real data.
/// </summary>
/// <remarks>
/// A database is accepted only when all of the following hold:
/// <list type="number">
/// <item>The connection string comes from <see cref="ConnectionStringVariable"/>. There is no
/// fallback to user-secrets or appsettings, which can point at a real development database.</item>
/// <item>The server is a loopback address.</item>
/// <item>The database name ends with <see cref="RequiredDatabaseSuffix"/>.</item>
/// <item>The database actually connected to (DB_NAME()) is that database and contains
/// <see cref="MarkerTable"/> with the token from <see cref="MarkerTokenVariable"/>. The marker
/// is written only when a disposable test database is created.</item>
/// </list>
/// Rejection messages never include the connection string.
/// </remarks>
internal static class DisposableTestDatabase
{
    public const string ConnectionStringVariable = "BUSINESSJOURNAL_SQL_CONNECTION";
    public const string MarkerTokenVariable = "BUSINESSJOURNAL_TEST_DB_TOKEN";
    public const string RequiredDatabaseSuffix = "_DisposableIT";
    public const string MarkerTable = "dbo.__DisposableTestDatabaseMarker";

    private static readonly Lazy<string> VerifiedConnectionString = new(() =>
        Verify(
            Environment.GetEnvironmentVariable(ConnectionStringVariable),
            Environment.GetEnvironmentVariable(MarkerTokenVariable)));

    /// <summary>
    /// Connection string of the verified disposable database. Throws when it cannot be verified.
    /// </summary>
    public static string ConnectionString => VerifiedConnectionString.Value;

    public static string Verify(string? connectionString, string? expectedMarkerToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw Reject($"{ConnectionStringVariable} is not set.");
        }

        if (string.IsNullOrWhiteSpace(expectedMarkerToken))
        {
            throw Reject($"{MarkerTokenVariable} is not set.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or KeyNotFoundException)
        {
            throw Reject("the connection string cannot be parsed.");
        }

        if (!IsLoopback(builder.DataSource))
        {
            throw Reject("the server is not a loopback address.");
        }

        var databaseName = builder.InitialCatalog;
        if (!databaseName.EndsWith(RequiredDatabaseSuffix, StringComparison.Ordinal))
        {
            throw Reject($"the database name does not end with '{RequiredDatabaseSuffix}'.");
        }

        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        using (var identity = connection.CreateCommand())
        {
            identity.CommandText = $"SELECT DB_NAME(), OBJECT_ID(N'{MarkerTable}', N'U');";

            using var reader = identity.ExecuteReader();
            reader.Read();

            if (!string.Equals(reader.GetString(0), databaseName, StringComparison.Ordinal))
            {
                throw Reject("the connected database is not the requested database.");
            }

            if (reader.IsDBNull(1))
            {
                throw Reject("the database has no disposable-test marker.");
            }
        }

        using var marker = connection.CreateCommand();
        marker.CommandText = $"SELECT COUNT(*) FROM {MarkerTable} WHERE Token = @Token;";
        marker.Parameters.AddWithValue("@Token", expectedMarkerToken);

        if ((int)marker.ExecuteScalar()! != 1)
        {
            throw Reject("the disposable-test marker does not match.");
        }

        return connectionString;
    }

    private static bool IsLoopback(string dataSource)
    {
        var host = dataSource.Trim();

        if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        host = host.Split(',', '\\')[0].Trim().Trim('[', ']');

        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
    }

    private static InvalidOperationException Reject(string reason)
    {
        return new InvalidOperationException(
            $"Refusing to use this database for tests: {reason} "
            + "Database tests only run against a designated disposable test database.");
    }
}
