using System.Data;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Infrastructure.Data.SqlServer;
using Microsoft.Data.SqlClient;

namespace BusinessJournal.Infrastructure.Repositories;

public sealed class SqlAppointmentRepository : IAppointmentRepository
{
    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlAppointmentRepository(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public void Add(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO dbo.Appointments (Id, CustomerId, Title, StartsAt, EndsAt, Notes, IsCancelled)
            VALUES (@Id, @CustomerId, @Title, @StartsAt, @EndsAt, @Notes, @IsCancelled);
            """;

        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier)
        {
            Value = appointment.Id
        });

        command.Parameters.Add(new SqlParameter("@CustomerId", SqlDbType.UniqueIdentifier)
        {
            Value = appointment.CustomerId
        });

        command.Parameters.Add(new SqlParameter("@Title", SqlDbType.NVarChar, 200)
        {
            Value = appointment.Title
        });

        command.Parameters.Add(new SqlParameter("@StartsAt", SqlDbType.DateTime2)
        {
            Value = appointment.StartsAt
        });

        command.Parameters.Add(new SqlParameter("@EndsAt", SqlDbType.DateTime2)
        {
            Value = appointment.EndsAt
        });

        command.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar, 1000)
        {
            Value = appointment.Notes is null ? DBNull.Value : appointment.Notes
        });

        command.Parameters.Add(new SqlParameter("@IsCancelled", SqlDbType.Bit)
        {
            Value = appointment.IsCancelled
        });

        command.ExecuteNonQuery();
    }

    public void Update(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE dbo.Appointments
            SET Title = @Title,
                StartsAt = @StartsAt,
                EndsAt = @EndsAt,
                Notes = @Notes,
                IsCancelled = @IsCancelled
            WHERE Id = @Id;
            """;

        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier)
        {
            Value = appointment.Id
        });

        command.Parameters.Add(new SqlParameter("@Title", SqlDbType.NVarChar, 200)
        {
            Value = appointment.Title
        });

        command.Parameters.Add(new SqlParameter("@StartsAt", SqlDbType.DateTime2)
        {
            Value = appointment.StartsAt
        });

        command.Parameters.Add(new SqlParameter("@EndsAt", SqlDbType.DateTime2)
        {
            Value = appointment.EndsAt
        });

        command.Parameters.Add(new SqlParameter("@Notes", SqlDbType.NVarChar, 1000)
        {
            Value = appointment.Notes is null ? DBNull.Value : appointment.Notes
        });

        command.Parameters.Add(new SqlParameter("@IsCancelled", SqlDbType.Bit)
        {
            Value = appointment.IsCancelled
        });

        command.ExecuteNonQuery();
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