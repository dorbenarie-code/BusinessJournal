using System.Text.RegularExpressions;
using BusinessJournal.Tests.TestSupport;
using Microsoft.Data.SqlClient;

namespace BusinessJournal.Tests.Infrastructure;

/// <summary>
/// Runs the real scripts under <c>database/</c> against scratch databases that each test creates
/// and drops on the verified disposable server. The shared test database is never re-initialized.
/// </summary>
[Collection("SqlServer integration tests")]
public sealed class SchemaScriptTests
{
    private const string CustomersScript = "001_create_customers.sql";
    private const string AppointmentsScript = "002_create_appointments.sql";

    private static readonly string DatabaseDirectory = FindDatabaseDirectory();

    [Fact]
    public void AppointmentsScript_OnAFreshDatabase_CreatesTheExpectedSchema()
    {
        using var database = ScratchDatabase.Create();

        database.RunScript(CustomersScript);
        database.RunScript(AppointmentsScript);

        AssertExpectedSchema(database);

        // The constraints are enforced, not just declared.
        var customerId = database.InsertCustomer();
        var invalidRange = Assert.Throws<SqlException>(() =>
            database.InsertAppointment(customerId, new DateTime(2030, 1, 1, 11, 0, 0), new DateTime(2030, 1, 1, 10, 0, 0)));
        var unknownCustomer = Assert.Throws<SqlException>(() =>
            database.InsertAppointment(Guid.NewGuid(), new DateTime(2030, 1, 1, 10, 0, 0), new DateTime(2030, 1, 1, 11, 0, 0)));

        Assert.Equal(547, invalidRange.Number);
        Assert.Equal(547, unknownCustomer.Number);
    }

    [Fact]
    public void AppointmentsScript_RunAgain_KeepsTheSentinelAppointmentUnchanged()
    {
        using var database = ScratchDatabase.Create();
        database.RunScript(CustomersScript);
        database.RunScript(AppointmentsScript);

        var sentinelId = database.InsertAppointment(
            database.InsertCustomer(),
            new DateTime(2030, 1, 1, 10, 0, 0),
            new DateTime(2030, 1, 1, 11, 0, 0),
            isCancelled: true);
        var before = database.ReadAppointmentRow(sentinelId);

        database.RunScript(CustomersScript);
        database.RunScript(AppointmentsScript);

        Assert.Equal(before, database.ReadAppointmentRow(sentinelId));
        Assert.Equal(1, database.CountAppointments());
        AssertExpectedSchema(database);
    }

    [Fact]
    public void AppointmentsScript_WhenAnExpectedIndexIsMissing_AddsItAndKeepsData()
    {
        using var database = ScratchDatabase.Create();
        database.RunScript(CustomersScript);
        database.RunScript(AppointmentsScript);

        var sentinelId = database.InsertAppointment(
            database.InsertCustomer(),
            new DateTime(2030, 1, 1, 10, 0, 0),
            new DateTime(2030, 1, 1, 11, 0, 0));
        var before = database.ReadAppointmentRow(sentinelId);
        database.Execute("DROP INDEX IX_Appointments_TimeWindow ON dbo.Appointments;");

        database.RunScript(AppointmentsScript);

        Assert.Equal(before, database.ReadAppointmentRow(sentinelId));
        AssertExpectedSchema(database);
    }

    [Fact]
    public void AppointmentsScript_WhenTheExistingTableIsIncompatible_FailsAndChangesNothing()
    {
        using var database = ScratchDatabase.Create();
        database.RunScript(CustomersScript);

        // Same table name, but Title is shorter and the CHECK constraint is missing.
        database.Execute("""
            CREATE TABLE dbo.Appointments
            (
                Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                CustomerId UNIQUEIDENTIFIER NOT NULL,
                Title NVARCHAR(50) NOT NULL,
                StartsAt DATETIME2 NOT NULL,
                EndsAt DATETIME2 NOT NULL,
                Notes NVARCHAR(1000) NULL,
                IsCancelled BIT NOT NULL CONSTRAINT DF_Appointments_IsCancelled DEFAULT 0,
                CONSTRAINT FK_Appointments_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id)
            );
            """);
        var sentinelId = database.InsertAppointment(
            database.InsertCustomer(),
            new DateTime(2030, 1, 1, 10, 0, 0),
            new DateTime(2030, 1, 1, 11, 0, 0));
        var before = database.ReadAppointmentRow(sentinelId);

        var exception = Assert.Throws<SqlException>(() => database.RunScript(AppointmentsScript));

        Assert.Equal(50001, exception.Number);
        Assert.Contains("does not match the expected schema", exception.Message);
        Assert.Contains("columns", exception.Message);
        Assert.Contains("check constraint", exception.Message);

        // Nothing changed: same row, same column definition, no index added.
        Assert.Equal(before, database.ReadAppointmentRow(sentinelId));
        Assert.Equal((short)100, database.Scalar<short>(
            "SELECT max_length FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Appointments') AND name = N'Title';"));
        Assert.Equal(0, database.Scalar<int>(
            "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Appointments') AND name LIKE N'IX[_]Appointments[_]%';"));
    }

    private static void AssertExpectedSchema(ScratchDatabase database)
    {
        Assert.Equal(
            new[]
            {
                "CustomerId uniqueidentifier 16 0 NOT NULL",
                "EndsAt datetime2 8 7 NOT NULL",
                "Id uniqueidentifier 16 0 NOT NULL",
                "IsCancelled bit 1 0 NOT NULL",
                "Notes nvarchar 2000 0 NULL",
                "StartsAt datetime2 8 7 NOT NULL",
                "Title nvarchar 400 0 NOT NULL"
            },
            database.Strings("""
                SELECT CONCAT(c.name, ' ', t.name, ' ', c.max_length, ' ', c.scale, ' ',
                              CASE WHEN c.is_nullable = 1 THEN 'NULL' ELSE 'NOT NULL' END)
                FROM sys.columns AS c
                JOIN sys.types AS t ON t.user_type_id = c.user_type_id
                WHERE c.object_id = OBJECT_ID(N'dbo.Appointments')
                ORDER BY c.name;
                """));

        Assert.Equal(
            new[] { "PK:Id" },
            database.Strings("""
                SELECT CONCAT('PK:', COL_NAME(ic.object_id, ic.column_id))
                FROM sys.key_constraints AS pk
                JOIN sys.index_columns AS ic ON ic.object_id = pk.parent_object_id AND ic.index_id = pk.unique_index_id
                WHERE pk.parent_object_id = OBJECT_ID(N'dbo.Appointments') AND pk.type = 'PK';
                """));

        Assert.Equal(
            new[] { "FK_Appointments_Customers CustomerId -> Customers.Id enabled" },
            database.Strings("""
                SELECT CONCAT(fk.name, ' ', COL_NAME(fkc.parent_object_id, fkc.parent_column_id), ' -> ',
                              OBJECT_NAME(fk.referenced_object_id), '.', COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id),
                              CASE WHEN fk.is_disabled = 0 THEN ' enabled' ELSE ' disabled' END)
                FROM sys.foreign_keys AS fk
                JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
                WHERE fk.parent_object_id = OBJECT_ID(N'dbo.Appointments');
                """));

        Assert.Equal(
            new[] { "CK_Appointments_TimeRange ([EndsAt]>[StartsAt])", "DF_Appointments_IsCancelled ((0))" },
            database.Strings("""
                SELECT CONCAT(name, ' ', definition) FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Appointments')
                UNION ALL
                SELECT CONCAT(name, ' ', definition) FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Appointments')
                ORDER BY 1;
                """));

        Assert.Equal(
            new[] { "IX_Appointments_CustomerId 1 CustomerId", "IX_Appointments_TimeWindow 1 StartsAt", "IX_Appointments_TimeWindow 2 EndsAt" },
            database.Strings("""
                SELECT CONCAT(i.name, ' ', ic.key_ordinal, ' ', COL_NAME(ic.object_id, ic.column_id))
                FROM sys.indexes AS i
                JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                WHERE i.object_id = OBJECT_ID(N'dbo.Appointments') AND i.name LIKE N'IX[_]%'
                ORDER BY i.name, ic.key_ordinal;
                """));
    }

    private static string FindDatabaseDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BusinessJournal.sln")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not locate the repository root.")
            : Path.Combine(directory.FullName, "database");
    }

    /// <summary>
    /// A database created and dropped by one test, on the server of the verified disposable test
    /// database. Its name is generated here and keeps the required disposable suffix.
    /// </summary>
    private sealed class ScratchDatabase : IDisposable
    {
        private readonly string _masterConnectionString;

        private ScratchDatabase(string name, string connectionString, string masterConnectionString)
        {
            Name = name;
            ConnectionString = connectionString;
            _masterConnectionString = masterConnectionString;
        }

        public string Name { get; }

        public string ConnectionString { get; }

        public static ScratchDatabase Create()
        {
            // Reading ConnectionString runs the full guard: loopback server, suffix, marker.
            var verified = new SqlConnectionStringBuilder(DisposableTestDatabase.ConnectionString);
            var name = $"BJ_SchemaScript_{Guid.NewGuid():N}{DisposableTestDatabase.RequiredDatabaseSuffix}";

            var master = new SqlConnectionStringBuilder(verified.ConnectionString)
            {
                InitialCatalog = "master",
                Pooling = false
            }.ConnectionString;

            var scratch = new SqlConnectionStringBuilder(verified.ConnectionString)
            {
                InitialCatalog = name,
                Pooling = false
            }.ConnectionString;

            ExecuteOn(master, $"CREATE DATABASE [{name}];");

            return new ScratchDatabase(name, scratch, master);
        }

        public void RunScript(string fileName)
        {
            var script = File.ReadAllText(Path.Combine(DatabaseDirectory, fileName));

            using var connection = new SqlConnection(ConnectionString);
            connection.Open();

            // Batches are separated by GO lines, as sqlcmd and SSMS do.
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }
        }

        public Guid InsertCustomer()
        {
            var id = Guid.NewGuid();
            Execute(
                "INSERT INTO dbo.Customers (Id, FullName, PhoneNumber, Email) VALUES (@Id, N'Schema Sentinel', N'000-0000', NULL);",
                ("@Id", id));
            return id;
        }

        public Guid InsertAppointment(Guid customerId, DateTime startsAt, DateTime endsAt, bool isCancelled = false)
        {
            var id = Guid.NewGuid();
            Execute(
                """
                INSERT INTO dbo.Appointments (Id, CustomerId, Title, StartsAt, EndsAt, Notes, IsCancelled)
                VALUES (@Id, @CustomerId, N'Schema sentinel', @StartsAt, @EndsAt, N'Must survive re-initialization', @IsCancelled);
                """,
                ("@Id", id),
                ("@CustomerId", customerId),
                ("@StartsAt", startsAt),
                ("@EndsAt", endsAt),
                ("@IsCancelled", isCancelled));
            return id;
        }

        public string ReadAppointmentRow(Guid id)
        {
            return Strings(
                $"""
                SELECT CONCAT(CustomerId, '|', Title, '|', CONVERT(varchar(27), StartsAt, 126), '|',
                              CONVERT(varchar(27), EndsAt, 126), '|', Notes, '|', IsCancelled)
                FROM dbo.Appointments WHERE Id = '{id}';
                """).Single();
        }

        public int CountAppointments()
        {
            return Scalar<int>("SELECT COUNT(*) FROM dbo.Appointments;");
        }

        public void Execute(string sql, params (string Name, object Value)[] parameters)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            foreach (var (parameterName, value) in parameters)
            {
                command.Parameters.AddWithValue(parameterName, value);
            }

            command.ExecuteNonQuery();
        }

        public T Scalar<T>(string sql)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            return (T)command.ExecuteScalar()!;
        }

        public string[] Strings(string sql)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            using var reader = command.ExecuteReader();
            var values = new List<string>();

            while (reader.Read())
            {
                values.Add(reader.GetString(0));
            }

            return values.ToArray();
        }

        public void Dispose()
        {
            ExecuteOn(
                _masterConnectionString,
                $"ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}];");
        }

        private static void ExecuteOn(string connectionString, string sql)
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
