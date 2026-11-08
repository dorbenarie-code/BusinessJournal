using System.Data;
using BusinessJournal.Application.Contracts.Appointments;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;
using BusinessJournal.Infrastructure.Data.SqlServer;
using Microsoft.Data.SqlClient;

namespace BusinessJournal.Infrastructure.Repositories;

public sealed class SqlAppointmentRepository : IAppointmentRepository
{
    private readonly SqlServerConnectionFactory _connectionFactory;
    private readonly TimeSpan _calendarLockTimeout;

    public SqlAppointmentRepository(SqlServerConnectionFactory connectionFactory)
        : this(connectionFactory, SqlCalendarLock.DefaultTimeout)
    {
    }

    /// <param name="connectionFactory">Opens connections to the calendar database.</param>
    /// <param name="calendarLockTimeout">How long a calendar change waits for the calendar lock
    /// before it fails without changing anything.</param>
    public SqlAppointmentRepository(
        SqlServerConnectionFactory connectionFactory,
        TimeSpan calendarLockTimeout)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        SqlCalendarLock.EnsureValidTimeout(calendarLockTimeout);

        _connectionFactory = connectionFactory;
        _calendarLockTimeout = calendarLockTimeout;
    }

    public bool TryAdd(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        if (appointment.IsCancelled)
        {
            throw new InvalidOperationException("Only active appointments can be added to the calendar.");
        }

        return ExecuteCalendarChange((connection, transaction) =>
        {
            if (HasActiveOverlap(connection, transaction, appointment.Time, excludedAppointmentId: null))
            {
                return false;
            }

            Insert(connection, transaction, appointment);
            return true;
        });
    }

    public RescheduleAppointmentResult TryReschedule(Guid appointmentId, TimeRange newTime)
    {
        ArgumentNullException.ThrowIfNull(newTime);

        return ExecuteCalendarChange((connection, transaction) =>
        {
            var isCancelled = ReadIsCancelled(connection, transaction, appointmentId);

            if (isCancelled is null)
            {
                return RescheduleAppointmentResult.NotFound;
            }

            if (isCancelled.Value)
            {
                return RescheduleAppointmentResult.Cancelled;
            }

            if (HasActiveOverlap(connection, transaction, newTime, appointmentId))
            {
                return RescheduleAppointmentResult.SlotTaken;
            }

            // Under the calendar lock the row cannot change between the checks above and this
            // update. The IsCancelled condition still guards against writers that bypass the lock:
            // a mismatch rolls the whole change back instead of reporting success.
            if (UpdateTimeIfActive(connection, transaction, appointmentId, newTime) != 1)
            {
                throw new InvalidOperationException(
                    "The appointment changed outside the calendar lock; the reschedule was rolled back.");
            }

            return RescheduleAppointmentResult.Rescheduled;
        });
    }

    public bool TryCancel(Guid appointmentId)
    {
        return ExecuteCalendarChange((connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, """
                UPDATE dbo.Appointments
                SET IsCancelled = 1
                WHERE Id = @Id;

                SELECT @@ROWCOUNT;
                """);

            AddIdParameter(command, appointmentId);

            // An already cancelled row still matches, so a repeated cancel succeeds.
            return (int)command.ExecuteScalar()! == 1;
        });
    }

    public Appointment? FindById(Guid appointmentId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT Id, CustomerId, Title, StartsAt, EndsAt, Notes, IsCancelled
            FROM dbo.Appointments
            WHERE Id = @Id;
            """;

        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier)
        {
            Value = appointmentId
        });

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return MapAppointment(reader);
    }

    public IReadOnlyCollection<Appointment> GetByCustomerId(Guid customerId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT Id, CustomerId, Title, StartsAt, EndsAt, Notes, IsCancelled
            FROM dbo.Appointments
            WHERE CustomerId = @CustomerId
            ORDER BY StartsAt, Id;
            """;

        command.Parameters.Add(new SqlParameter("@CustomerId", SqlDbType.UniqueIdentifier)
        {
            Value = customerId
        });

        using var reader = command.ExecuteReader();

        var appointments = new List<Appointment>();

        while (reader.Read())
        {
            appointments.Add(MapAppointment(reader));
        }

        return appointments;
    }

    public IReadOnlyCollection<Appointment> GetOverlapping(DateTime startsAt, DateTime endsAt)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT Id, CustomerId, Title, StartsAt, EndsAt, Notes, IsCancelled
            FROM dbo.Appointments
            WHERE IsCancelled = 0
              AND StartsAt < @EndsAt
              AND EndsAt > @StartsAt;
            """;

        command.Parameters.Add(new SqlParameter("@StartsAt", SqlDbType.DateTime2)
        {
            Value = startsAt
        });

        command.Parameters.Add(new SqlParameter("@EndsAt", SqlDbType.DateTime2)
        {
            Value = endsAt
        });

        using var reader = command.ExecuteReader();

        var appointments = new List<Appointment>();

        while (reader.Read())
        {
            appointments.Add(MapAppointment(reader));
        }

        return appointments;
    }

    /// <summary>
    /// Runs one calendar change on a single connection and transaction. The exclusive calendar
    /// lock is acquired before the first read and held until the commit, so the checks and the
    /// write form one atomic step. A result is returned only after the commit succeeds; on any
    /// failure the transaction is rolled back before the exception propagates.
    /// </summary>
    private T ExecuteCalendarChange<T>(Func<SqlConnection, SqlTransaction, T> change)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            SqlCalendarLock.Acquire(connection, transaction, _calendarLockTimeout);

            var result = change(connection, transaction);

            transaction.Commit();
            return result;
        }
        catch
        {
            RollBack(transaction);
            throw;
        }
    }

    private static void RollBack(SqlTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch (Exception exception) when (exception is InvalidOperationException or SqlException)
        {
            // The server already ended the transaction (for example after a deadlock-victim
            // statement error) or the connection is broken. Disposing the connection discards it,
            // and the original exception still propagates, so success is never reported.
        }
    }

    private static bool HasActiveOverlap(
        SqlConnection connection,
        SqlTransaction transaction,
        TimeRange time,
        Guid? excludedAppointmentId)
    {
        // Half-open ranges [start, end): adjacent appointments do not overlap (same rule as TimeRange).
        using var command = CreateCommand(connection, transaction, """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.Appointments
                WHERE IsCancelled = 0
                  AND StartsAt < @EndsAt
                  AND EndsAt > @StartsAt
                  AND (@ExcludedId IS NULL OR Id <> @ExcludedId)
            ) THEN 1 ELSE 0 END;
            """);

        AddTimeParameters(command, time);

        command.Parameters.Add(new SqlParameter("@ExcludedId", SqlDbType.UniqueIdentifier)
        {
            Value = (object?)excludedAppointmentId ?? DBNull.Value
        });

        return (int)command.ExecuteScalar()! == 1;
    }

    private static bool? ReadIsCancelled(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid appointmentId)
    {
        using var command = CreateCommand(
            connection,
            transaction,
            "SELECT IsCancelled FROM dbo.Appointments WHERE Id = @Id;");

        AddIdParameter(command, appointmentId);

        return command.ExecuteScalar() is bool isCancelled ? isCancelled : null;
    }

    private static int UpdateTimeIfActive(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid appointmentId,
        TimeRange newTime)
    {
        using var command = CreateCommand(connection, transaction, """
            UPDATE dbo.Appointments
            SET StartsAt = @StartsAt,
                EndsAt = @EndsAt
            WHERE Id = @Id
              AND IsCancelled = 0;

            SELECT @@ROWCOUNT;
            """);

        AddIdParameter(command, appointmentId);
        AddTimeParameters(command, newTime);

        return (int)command.ExecuteScalar()!;
    }

    private static void Insert(
        SqlConnection connection,
        SqlTransaction transaction,
        Appointment appointment)
    {
        using var command = CreateCommand(connection, transaction, """
            INSERT INTO dbo.Appointments (Id, CustomerId, Title, StartsAt, EndsAt, Notes, IsCancelled)
            VALUES (@Id, @CustomerId, @Title, @StartsAt, @EndsAt, @Notes, 0);
            """);

        AddIdParameter(command, appointment.Id);
        AddTimeParameters(command, appointment.Time);

        command.Parameters.Add(new SqlParameter("@CustomerId", SqlDbType.UniqueIdentifier)
        {
            Value = appointment.CustomerId
        });

        command.Parameters.Add(new SqlParameter("@Title", SqlDbType.NVarChar, 200)
        {
            Value = appointment.Title
        });

        command.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar, 1000)
        {
            Value = appointment.Notes is null ? DBNull.Value : appointment.Notes
        });

        command.ExecuteNonQuery();
    }

    private static SqlCommand CreateCommand(
        SqlConnection connection,
        SqlTransaction transaction,
        string commandText)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        return command;
    }

    private static void AddIdParameter(SqlCommand command, Guid appointmentId)
    {
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier)
        {
            Value = appointmentId
        });
    }

    private static void AddTimeParameters(SqlCommand command, TimeRange time)
    {
        command.Parameters.Add(new SqlParameter("@StartsAt", SqlDbType.DateTime2)
        {
            Value = time.Start
        });

        command.Parameters.Add(new SqlParameter("@EndsAt", SqlDbType.DateTime2)
        {
            Value = time.End
        });
    }

    private static Appointment MapAppointment(SqlDataReader reader)
    {
        var id = reader.GetGuid(reader.GetOrdinal("Id"));
        var customerId = reader.GetGuid(reader.GetOrdinal("CustomerId"));
        var title = reader.GetString(reader.GetOrdinal("Title"));
        var startsAt = reader.GetDateTime(reader.GetOrdinal("StartsAt"));
        var endsAt = reader.GetDateTime(reader.GetOrdinal("EndsAt"));
        var isCancelled = reader.GetBoolean(reader.GetOrdinal("IsCancelled"));

        string? notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
            ? null
            : reader.GetString(reader.GetOrdinal("Notes"));

        return Appointment.Restore(
            id,
            customerId,
            title,
            startsAt,
            endsAt,
            notes,
            isCancelled);
    }
}