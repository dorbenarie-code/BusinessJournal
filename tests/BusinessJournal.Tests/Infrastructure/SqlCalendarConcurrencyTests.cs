using System.Diagnostics;
using BusinessJournal.Application.Common.Exceptions;
using BusinessJournal.Application.Contracts.Appointments;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;
using BusinessJournal.Infrastructure.Data.SqlServer;
using BusinessJournal.Infrastructure.Repositories;
using BusinessJournal.Tests.TestSupport;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace BusinessJournal.Tests.Infrastructure;

/// <summary>
/// Deterministic checks of the calendar lock. An independent connection holds the lock, SQL Server
/// itself confirms that the operation under test is waiting for it, and the test decides exactly
/// when the lock is released. No step relies on a sleep.
/// </summary>
[Collection("SqlServer integration tests")]
public sealed class SqlCalendarConcurrencyTests
{
    // Long enough that only the test's own release can end the wait.
    private static readonly TimeSpan WaitingOperationLockTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ShortLockTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly DateTime Day = new(2026, 5, 4);

    private readonly string _connectionString;
    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly SqlCustomerRepository _customerRepository;

    public SqlCalendarConcurrencyTests()
    {
        // CleanupDatabase deletes all rows, so only the verified disposable database is allowed.
        _connectionString = DisposableTestDatabase.ConnectionString;

        _connectionFactory = new SqlServerConnectionFactory(
            Options.Create(new SqlServerOptions
            {
                ConnectionString = _connectionString
            }));

        _customerRepository = new SqlCustomerRepository(_connectionFactory);

        CleanupDatabase();
    }

    [Fact]
    public async Task TryAdd_WaitsForTheCalendarLock_ThenSeesTheConflictCommittedByTheHolder()
    {
        var customer = CreateCustomer();
        var slot = Range(10, 11);
        var candidate = Appointment.Create(customer.Id, "Candidate", slot);
        var competitor = Appointment.Create(customer.Id, "Competitor", slot);
        var repository = CreateRepository(WaitingOperationLockTimeout);

        using var holder = OpenConnection();
        using var holderTransaction = holder.BeginTransaction();
        SqlCalendarLock.Acquire(holder, holderTransaction, SyncTimeout);

        var operation = Task.Run(() => repository.TryAdd(candidate));

        // Protected point: the operation is parked at the lock while the slot is still free.
        // Without the lock it would have checked the empty slot and inserted by now.
        var waitingSession = await WaitForCalendarLockWaiterAsync();
        Assert.NotEqual(GetSessionId(holder, holderTransaction), waitingSession);
        Assert.False(operation.IsCompleted);
        Assert.Empty(GetActiveOverlappingIds(slot));

        // The holder books the same slot and commits, which also releases the lock.
        Execute(holder, holderTransaction, """
            INSERT INTO dbo.Appointments (Id, CustomerId, Title, StartsAt, EndsAt, Notes, IsCancelled)
            VALUES (@Id, @CustomerId, @Title, @StartsAt, @EndsAt, NULL, 0);
            """,
            ("@Id", competitor.Id),
            ("@CustomerId", competitor.CustomerId),
            ("@Title", competitor.Title),
            ("@StartsAt", competitor.StartsAt),
            ("@EndsAt", competitor.EndsAt));
        holderTransaction.Commit();

        var inserted = await operation.WaitAsync(SyncTimeout);

        Assert.False(inserted);
        Assert.Equal(new[] { competitor.Id }, GetActiveOverlappingIds(slot));
        Assert.Null(repository.FindById(candidate.Id));
    }

    [Fact]
    public async Task TryReschedule_QueuedBehindACancellation_ReturnsCancelled_AndCannotReactivate()
    {
        var customer = CreateCustomer();
        var appointment = Appointment.Create(customer.Id, "Hair Color", Range(10, 11));
        var repository = CreateRepository(WaitingOperationLockTimeout);
        Assert.True(repository.TryAdd(appointment));

        using var holder = OpenConnection();
        using var holderTransaction = holder.BeginTransaction();
        SqlCalendarLock.Acquire(holder, holderTransaction, SyncTimeout);

        // The reschedule starts while the appointment is still active.
        var operation = Task.Run(() => repository.TryReschedule(appointment.Id, Range(12, 13)));
        await WaitForCalendarLockWaiterAsync();
        Assert.False(operation.IsCompleted);

        // The holder cancels it and commits.
        Execute(holder, holderTransaction,
            "UPDATE dbo.Appointments SET IsCancelled = 1 WHERE Id = @Id;",
            ("@Id", appointment.Id));
        holderTransaction.Commit();

        Assert.Equal(RescheduleAppointmentResult.Cancelled, await operation.WaitAsync(SyncTimeout));

        var stored = repository.FindById(appointment.Id)!;
        Assert.True(stored.IsCancelled);
        Assert.Equal(Range(10, 11), stored.Time);
    }

    [Fact]
    public async Task TryCancel_QueuedBehindAReschedule_StillCancels()
    {
        var customer = CreateCustomer();
        var appointment = Appointment.Create(customer.Id, "Hair Color", Range(10, 11));
        var repository = CreateRepository(WaitingOperationLockTimeout);
        Assert.True(repository.TryAdd(appointment));

        using var holder = OpenConnection();
        using var holderTransaction = holder.BeginTransaction();
        SqlCalendarLock.Acquire(holder, holderTransaction, SyncTimeout);

        var operation = Task.Run(() => repository.TryCancel(appointment.Id));
        await WaitForCalendarLockWaiterAsync();
        Assert.False(operation.IsCompleted);

        // The holder reschedules it and commits.
        var newTime = Range(12, 13);
        Execute(holder, holderTransaction,
            "UPDATE dbo.Appointments SET StartsAt = @StartsAt, EndsAt = @EndsAt WHERE Id = @Id;",
            ("@Id", appointment.Id),
            ("@StartsAt", newTime.Start),
            ("@EndsAt", newTime.End));
        holderTransaction.Commit();

        Assert.True(await operation.WaitAsync(SyncTimeout));

        var stored = repository.FindById(appointment.Id)!;
        Assert.True(stored.IsCancelled);
        Assert.Equal(newTime, stored.Time);
    }

    [Fact]
    public void TryAdd_WhenTheLockIsNotGrantedInTime_ThrowsServiceUnavailable_AndWritesNothing()
    {
        var customer = CreateCustomer();
        var candidate = Appointment.Create(customer.Id, "Candidate", Range(10, 11));
        var repository = CreateRepository(ShortLockTimeout);

        AssertLockTimeoutLeavesNothingBehind(() => repository.TryAdd(candidate));

        Assert.Null(repository.FindById(candidate.Id));
        Assert.True(repository.TryAdd(candidate));
    }

    [Fact]
    public void TryReschedule_WhenTheLockIsNotGrantedInTime_ThrowsServiceUnavailable_AndChangesNothing()
    {
        var customer = CreateCustomer();
        var appointment = Appointment.Create(customer.Id, "Hair Color", Range(10, 11));
        var repository = CreateRepository(ShortLockTimeout);
        Assert.True(repository.TryAdd(appointment));

        AssertLockTimeoutLeavesNothingBehind(() => repository.TryReschedule(appointment.Id, Range(12, 13)));

        var stored = repository.FindById(appointment.Id)!;
        Assert.Equal(Range(10, 11), stored.Time);
        Assert.False(stored.IsCancelled);
        Assert.Equal(RescheduleAppointmentResult.Rescheduled, repository.TryReschedule(appointment.Id, Range(12, 13)));
    }

    [Fact]
    public void TryCancel_WhenTheLockIsNotGrantedInTime_ThrowsServiceUnavailable_AndChangesNothing()
    {
        var customer = CreateCustomer();
        var appointment = Appointment.Create(customer.Id, "Hair Color", Range(10, 11));
        var repository = CreateRepository(ShortLockTimeout);
        Assert.True(repository.TryAdd(appointment));

        AssertLockTimeoutLeavesNothingBehind(() => repository.TryCancel(appointment.Id));

        Assert.False(repository.FindById(appointment.Id)!.IsCancelled);
        Assert.True(repository.TryCancel(appointment.Id));
    }

    [Fact]
    public void TryAdd_WhenTheInsertFailsAfterTheLockIsGranted_RollsBackAndReleasesTheLock()
    {
        var repository = CreateRepository(SyncTimeout);
        var orphan = Appointment.Create(Guid.NewGuid(), "No such customer", Range(10, 11));

        var exception = Assert.Throws<SqlException>(() => repository.TryAdd(orphan));

        Assert.Equal(547, exception.Number); // FOREIGN KEY violation, raised after the lock was granted.
        Assert.Equal(0, CountCalendarLockRequests());
        Assert.Null(repository.FindById(orphan.Id));

        // A fail-fast repository proves the lock is free right away, not after some timeout.
        var customer = CreateCustomer();
        var fastRepository = CreateRepository(ShortLockTimeout);
        Assert.True(fastRepository.TryAdd(Appointment.Create(customer.Id, "Valid", Range(10, 11))));
    }

    private void AssertLockTimeoutLeavesNothingBehind(Action operation)
    {
        using (var holder = OpenConnection())
        using (var holderTransaction = holder.BeginTransaction())
        {
            SqlCalendarLock.Acquire(holder, holderTransaction, SyncTimeout);

            var stopwatch = Stopwatch.StartNew();
            var exception = Assert.Throws<ServiceUnavailableException>(operation);
            stopwatch.Stop();

            Assert.Equal(SqlCalendarLock.BusyMessage, exception.Message);

            // Bounded: it waited for its own timeout, not for the holder.
            Assert.InRange(
                stopwatch.Elapsed,
                ShortLockTimeout * 0.8,
                ShortLockTimeout + TimeSpan.FromSeconds(5));

            // Only the holder's granted lock remains; the failed operation left no request behind.
            Assert.Equal(1, CountCalendarLockRequests());

            holderTransaction.Rollback();
        }

        Assert.Equal(0, CountCalendarLockRequests());
    }

    /// <summary>
    /// Polls SQL Server until a session other than the holder waits for the calendar lock.
    /// </summary>
    private async Task<int> WaitForCalendarLockWaiterAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < SyncTimeout)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (1) request_session_id
                FROM sys.dm_tran_locks
                WHERE resource_type = N'APPLICATION'
                  AND resource_database_id = DB_ID()
                  AND request_status = N'WAIT'
                  AND CHARINDEX(@Resource, resource_description) > 0;
                """;
            command.Parameters.AddWithValue("@Resource", SqlCalendarLock.ResourceName);

            if (command.ExecuteScalar() is int sessionId)
            {
                return sessionId;
            }

            await Task.Delay(PollInterval);
        }

        throw new Xunit.Sdk.XunitException(
            $"No session waited for the calendar lock within {SyncTimeout.TotalSeconds} s.");
    }

    private int CountCalendarLockRequests()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.dm_tran_locks
            WHERE resource_type = N'APPLICATION'
              AND resource_database_id = DB_ID()
              AND CHARINDEX(@Resource, resource_description) > 0;
            """;
        command.Parameters.AddWithValue("@Resource", SqlCalendarLock.ResourceName);

        return (int)command.ExecuteScalar()!;
    }

    private Guid[] GetActiveOverlappingIds(TimeRange time)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id
            FROM dbo.Appointments
            WHERE IsCancelled = 0
              AND StartsAt < @EndsAt
              AND EndsAt > @StartsAt;
            """;
        command.Parameters.AddWithValue("@StartsAt", time.Start);
        command.Parameters.AddWithValue("@EndsAt", time.End);

        using var reader = command.ExecuteReader();
        var ids = new List<Guid>();

        while (reader.Read())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids.ToArray();
    }

    private static int GetSessionId(SqlConnection connection, SqlTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT CAST(@@SPID AS int);";

        return (int)command.ExecuteScalar()!;
    }

    private static void Execute(
        SqlConnection connection,
        SqlTransaction transaction,
        string commandText,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        Assert.Equal(1, command.ExecuteNonQuery());
    }

    private SqlAppointmentRepository CreateRepository(TimeSpan calendarLockTimeout)
    {
        return new SqlAppointmentRepository(_connectionFactory, calendarLockTimeout);
    }

    private SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private Customer CreateCustomer()
    {
        var customer = Customer.Create($"Lock Test {Guid.NewGuid():N}", "0501234567", "lock@example.com");
        _customerRepository.Add(customer);
        return customer;
    }

    private static TimeRange Range(int startHour, int endHour)
    {
        return TimeRange.Create(Day.AddHours(startHour), Day.AddHours(endHour));
    }

    private void CleanupDatabase()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM dbo.Appointments;
            DELETE FROM dbo.Customers;
            """;

        command.ExecuteNonQuery();
    }
}
