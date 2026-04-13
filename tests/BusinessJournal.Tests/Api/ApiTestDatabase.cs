using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace BusinessJournal.Tests.Api;

internal static class ApiTestDatabase
{
    public static void Cleanup()
    {
        var connectionString = GetConnectionString();

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM dbo.Appointments;
            DELETE FROM dbo.Customers;
            """;

        command.ExecuteNonQuery();
    }

    private static string GetConnectionString()
    {
        var projectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        var apiProjectPath = Path.Combine(projectRoot, "src", "BusinessJournal.Api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<ApiWebApplicationFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration["BUSINESSJOURNAL_SQL_CONNECTION"]
            ?? configuration["SqlServer:ConnectionString"]
            ?? configuration.GetConnectionString("BusinessJournalDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Could not find a SQL connection string. Configure user-secrets, environment variables, or appsettings.");
        }

        return connectionString;
    }
}