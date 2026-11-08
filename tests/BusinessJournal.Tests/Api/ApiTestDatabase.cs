using BusinessJournal.Tests.TestSupport;
using Microsoft.Data.SqlClient;

namespace BusinessJournal.Tests.Api;

internal static class ApiTestDatabase
{
    public static void Cleanup()
    {
        // Deletes all rows, so only the verified disposable database is allowed.
        using var connection = new SqlConnection(DisposableTestDatabase.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM dbo.Appointments;
            DELETE FROM dbo.Customers;
            """;

        command.ExecuteNonQuery();
    }
}