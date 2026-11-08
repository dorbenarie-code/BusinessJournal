using BusinessJournal.Application.Contracts.Appointments;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;
using BusinessJournal.Infrastructure.Data.SqlServer;
using BusinessJournal.Infrastructure.Repositories;
using BusinessJournal.Tests.TestSupport;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace BusinessJournal.Tests.Infrastructure;

[Collection("SqlServer integration tests")]
public sealed class SqlAppointmentRepositoryTests
{
    private readonly string _connectionString;
    private readonly SqlAppointmentRepository _appointmentRepository;
    private readonly SqlCustomerRepository _customerRepository;

    public SqlAppointmentRepositoryTests()
    {
        // CleanupDatabase deletes all rows, so only the verified disposable database is allowed.
        _connectionString = DisposableTestDatabase.ConnectionString;

        var factory = new SqlServerConnectionFactory(
            Options.Create(new SqlServerOptions
            {
                ConnectionString = _connectionString
            }));

        _appointmentRepository = new SqlAppointmentRepository(factory);
        _customerRepository = new SqlCustomerRepository(factory);

        CleanupDatabase();
    }

    [Fact]
    public void TryAdd_WithValidAppointment_ShouldStoreAppointment()
    {
        var customer = CreateCustomer();

        var appointment = Appointment.Create(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 12, 10, 0, 0),
            new DateTime(2026, 4, 12, 11, 0, 0),
            "First visit");

        Assert.True(_appointmentRepository.TryAdd(appointment));

        var storedAppointment = _appointmentRepository.FindById(appointment.Id);

        Assert.NotNull(storedAppointment);
        Assert.Equal(appointment.Id, storedAppointment!.Id);
        Assert.Equal(customer.Id, storedAppointment.CustomerId);
        Assert.Equal("Hair Color", storedAppointment.Title);
        Assert.Equal(new DateTime(2026, 4, 12, 10, 0, 0), storedAppointment.StartsAt);
        Assert.Equal(new DateTime(2026, 4, 12, 11, 0, 0), storedAppointment.EndsAt);
        Assert.Equal("First visit", storedAppointment.Notes);
        Assert.False(storedAppointment.IsCancelled);
    }

    [Fact]
    public void FindById_WhenAppointmentDoesNotExist_ShouldReturnNull()
    {
        var appointment = _appointmentRepository.FindById(Guid.NewGuid());

        Assert.Null(appointment);
    }

    [Fact]
public void GetByCustomerId_ShouldReturnAppointmentsOfRequestedCustomerInStartsAtThenIdOrder()
{
    var firstCustomer = CreateCustomer("Rachel Cohen", "0501234567", "rachel@gmail.com");
    var secondCustomer = CreateCustomer("Michal Levi", "0521234567", "michal@gmail.com");

    var laterAppointment = Appointment.Create(
        firstCustomer.Id,
        "Hair Cut",
        new DateTime(2026, 4, 12, 12, 0, 0),
        new DateTime(2026, 4, 12, 13, 0, 0));

    var earlierAppointment = Appointment.Create(
        firstCustomer.Id,
        "Hair Color",
        new DateTime(2026, 4, 12, 10, 0, 0),
        new DateTime(2026, 4, 12, 11, 0, 0));

    var otherAppointment = Appointment.Create(
        secondCustomer.Id,
        "Nails",
        new DateTime(2026, 4, 12, 9, 0, 0),
        new DateTime(2026, 4, 12, 10, 0, 0));

    Assert.True(_appointmentRepository.TryAdd(laterAppointment));
    Assert.True(_appointmentRepository.TryAdd(earlierAppointment));
    Assert.True(_appointmentRepository.TryAdd(otherAppointment));

    var appointments = _appointmentRepository.GetByCustomerId(firstCustomer.Id).ToList();

    Assert.Equal(2, appointments.Count);

    Assert.Equal(earlierAppointment.Id, appointments[0].Id);
    Assert.Equal(laterAppointment.Id, appointments[1].Id);

    Assert.DoesNotContain(appointments, appointment => appointment.Id == otherAppointment.Id);
}

    [Fact]
    public void GetOverlapping_ShouldReturnOnlyActiveOverlappingAppointments()
    {
        var customer = CreateCustomer();

        var overlappingAppointment = Appointment.Create(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 12, 10, 0, 0),
            new DateTime(2026, 4, 12, 11, 0, 0));

        var cancelledAppointment = Appointment.Create(
            customer.Id,
            "Cancelled",
            new DateTime(2026, 4, 12, 10, 15, 0),
            new DateTime(2026, 4, 12, 10, 45, 0));

        var nonOverlappingAppointment = Appointment.Create(
            customer.Id,
            "Nails",
            new DateTime(2026, 4, 12, 12, 0, 0),
            new DateTime(2026, 4, 12, 13, 0, 0));

        Assert.True(_appointmentRepository.TryAdd(cancelledAppointment));
        Assert.True(_appointmentRepository.TryCancel(cancelledAppointment.Id));
        Assert.True(_appointmentRepository.TryAdd(overlappingAppointment));
        Assert.True(_appointmentRepository.TryAdd(nonOverlappingAppointment));

        var result = _appointmentRepository.GetOverlapping(
            new DateTime(2026, 4, 12, 10, 30, 0),
            new DateTime(2026, 4, 12, 11, 30, 0));

        Assert.Single(result);
        Assert.Equal(overlappingAppointment.Id, result.Single().Id);
    }

    [Fact]
    public void TryCancel_WhenAppointmentExists_ShouldPersistCancellation()
    {
        var customer = CreateCustomer();

        var appointment = Appointment.Create(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 12, 10, 0, 0),
            new DateTime(2026, 4, 12, 11, 0, 0));

        Assert.True(_appointmentRepository.TryAdd(appointment));

        Assert.True(_appointmentRepository.TryCancel(appointment.Id));

        var updatedAppointment = _appointmentRepository.FindById(appointment.Id);

        Assert.NotNull(updatedAppointment);
        Assert.True(updatedAppointment!.IsCancelled);
    }

    [Fact]
    public void TryReschedule_WithFreeSlot_ShouldPersistNewTime()
    {
        var customer = CreateCustomer();

        var appointment = Appointment.Create(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 12, 10, 0, 0),
            new DateTime(2026, 4, 12, 11, 0, 0));

        Assert.True(_appointmentRepository.TryAdd(appointment));

        var result = _appointmentRepository.TryReschedule(
            appointment.Id,
            TimeRange.Create(
                new DateTime(2026, 4, 12, 12, 0, 0),
                new DateTime(2026, 4, 12, 13, 0, 0)));

        Assert.Equal(RescheduleAppointmentResult.Rescheduled, result);

        var updatedAppointment = _appointmentRepository.FindById(appointment.Id);

        Assert.NotNull(updatedAppointment);
        Assert.Equal(new DateTime(2026, 4, 12, 12, 0, 0), updatedAppointment!.StartsAt);
        Assert.Equal(new DateTime(2026, 4, 12, 13, 0, 0), updatedAppointment.EndsAt);
    }

    [Fact]
    public void TryAdd_WhenSlotOverlapsActiveAppointment_ShouldReturnFalseAndNotInsert()
    {
        var customer = CreateCustomer();
        Assert.True(_appointmentRepository.TryAdd(CreateAppointment(customer, 10, 0, 11, 0)));
        var overlapping = CreateAppointment(customer, 10, 30, 11, 30);

        Assert.False(_appointmentRepository.TryAdd(overlapping));
        Assert.Null(_appointmentRepository.FindById(overlapping.Id));
    }

    [Fact]
    public void TryAdd_WhenAdjacentToActiveAppointment_ShouldSucceed()
    {
        var customer = CreateCustomer();

        Assert.True(_appointmentRepository.TryAdd(CreateAppointment(customer, 10, 0, 11, 0)));
        Assert.True(_appointmentRepository.TryAdd(CreateAppointment(customer, 11, 0, 12, 0)));
        Assert.True(_appointmentRepository.TryAdd(CreateAppointment(customer, 9, 0, 10, 0)));
    }

    [Fact]
    public void TryAdd_WhenAppointmentIsCancelled_ShouldThrowAndNotInsert()
    {
        var customer = CreateCustomer();
        var appointment = CreateAppointment(customer, 10, 0, 11, 0);
        appointment.Cancel();

        Assert.Throws<InvalidOperationException>(() => _appointmentRepository.TryAdd(appointment));
        Assert.Null(_appointmentRepository.FindById(appointment.Id));
    }

    [Fact]
    public void TryReschedule_WhenAppointmentDoesNotExist_ShouldReturnNotFound()
    {
        var result = _appointmentRepository.TryReschedule(Guid.NewGuid(), Range(12, 0, 13, 0));

        Assert.Equal(RescheduleAppointmentResult.NotFound, result);
    }

    [Fact]
    public void TryReschedule_WhenAppointmentIsCancelled_ShouldReturnCancelledAndLeaveRowUnchanged()
    {
        var customer = CreateCustomer();
        var appointment = CreateAppointment(customer, 10, 0, 11, 0);
        Assert.True(_appointmentRepository.TryAdd(appointment));
        Assert.True(_appointmentRepository.TryCancel(appointment.Id));

        var result = _appointmentRepository.TryReschedule(appointment.Id, Range(12, 0, 13, 0));

        Assert.Equal(RescheduleAppointmentResult.Cancelled, result);
        var stored = _appointmentRepository.FindById(appointment.Id)!;
        Assert.True(stored.IsCancelled);
        Assert.Equal(Range(10, 0, 11, 0), stored.Time);
    }

    [Fact]
    public void TryReschedule_WhenNewTimeOverlapsAnotherActiveAppointment_ShouldReturnSlotTakenAndKeepTime()
    {
        var customer = CreateCustomer();
        var appointment = CreateAppointment(customer, 10, 0, 11, 0);
        Assert.True(_appointmentRepository.TryAdd(appointment));
        Assert.True(_appointmentRepository.TryAdd(CreateAppointment(customer, 12, 0, 13, 0)));

        var result = _appointmentRepository.TryReschedule(appointment.Id, Range(12, 30, 13, 30));

        Assert.Equal(RescheduleAppointmentResult.SlotTaken, result);
        Assert.Equal(Range(10, 0, 11, 0), _appointmentRepository.FindById(appointment.Id)!.Time);
    }

    [Fact]
    public void TryReschedule_WhenNewTimeOverlapsOnlyItsOwnCurrentTime_ShouldReschedule()
    {
        var customer = CreateCustomer();
        var appointment = CreateAppointment(customer, 10, 0, 11, 0);
        Assert.True(_appointmentRepository.TryAdd(appointment));

        var result = _appointmentRepository.TryReschedule(appointment.Id, Range(10, 30, 11, 30));

        Assert.Equal(RescheduleAppointmentResult.Rescheduled, result);
        Assert.Equal(Range(10, 30, 11, 30), _appointmentRepository.FindById(appointment.Id)!.Time);
    }

    [Fact]
    public void TryReschedule_ShouldChangeOnlyTheSchedulingColumns()
    {
        var customer = CreateCustomer();
        var appointment = Appointment.Create(customer.Id, "Hair Color", Range(10, 0, 11, 0), "First visit");
        Assert.True(_appointmentRepository.TryAdd(appointment));
        SetTitleAndNotesDirectly(appointment.Id, "Changed elsewhere", "Newer notes");

        Assert.Equal(
            RescheduleAppointmentResult.Rescheduled,
            _appointmentRepository.TryReschedule(appointment.Id, Range(12, 0, 13, 0)));

        var stored = _appointmentRepository.FindById(appointment.Id)!;
        Assert.Equal(Range(12, 0, 13, 0), stored.Time);
        Assert.Equal("Changed elsewhere", stored.Title);
        Assert.Equal("Newer notes", stored.Notes);
        Assert.Equal(customer.Id, stored.CustomerId);
        Assert.False(stored.IsCancelled);
    }

    [Fact]
    public void TryCancel_WhenAppointmentDoesNotExist_ShouldReturnFalse()
    {
        Assert.False(_appointmentRepository.TryCancel(Guid.NewGuid()));
    }

    [Fact]
    public void TryCancel_WhenCalledTwice_ShouldSucceedBothTimes()
    {
        var customer = CreateCustomer();
        var appointment = CreateAppointment(customer, 10, 0, 11, 0);
        Assert.True(_appointmentRepository.TryAdd(appointment));

        Assert.True(_appointmentRepository.TryCancel(appointment.Id));
        Assert.True(_appointmentRepository.TryCancel(appointment.Id));
        Assert.True(_appointmentRepository.FindById(appointment.Id)!.IsCancelled);
    }

    [Fact]
    public void TryCancel_ShouldChangeOnlyTheCancellationFlag()
    {
        var customer = CreateCustomer();
        var appointment = Appointment.Create(customer.Id, "Hair Color", Range(10, 0, 11, 0), "First visit");
        Assert.True(_appointmentRepository.TryAdd(appointment));
        SetTitleAndNotesDirectly(appointment.Id, "Changed elsewhere", "Newer notes");

        Assert.True(_appointmentRepository.TryCancel(appointment.Id));

        var stored = _appointmentRepository.FindById(appointment.Id)!;
        Assert.True(stored.IsCancelled);
        Assert.Equal(Range(10, 0, 11, 0), stored.Time);
        Assert.Equal("Changed elsewhere", stored.Title);
        Assert.Equal("Newer notes", stored.Notes);
    }

    private static TimeRange Range(int startHour, int startMinute, int endHour, int endMinute)
    {
        var day = new DateTime(2026, 4, 12);

        return TimeRange.Create(
            day.AddHours(startHour).AddMinutes(startMinute),
            day.AddHours(endHour).AddMinutes(endMinute));
    }

    private static Appointment CreateAppointment(
        Customer customer,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute)
    {
        return Appointment.Create(
            customer.Id,
            "Hair Color",
            Range(startHour, startMinute, endHour, endMinute));
    }

    /// <summary>
    /// Simulates a newer value written by someone else, to prove a calendar change does not
    /// overwrite columns it does not own.
    /// </summary>
    private void SetTitleAndNotesDirectly(Guid appointmentId, string title, string notes)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE dbo.Appointments SET Title = @Title, Notes = @Notes WHERE Id = @Id;";
        command.Parameters.AddWithValue("@Title", title);
        command.Parameters.AddWithValue("@Notes", notes);
        command.Parameters.AddWithValue("@Id", appointmentId);

        Assert.Equal(1, command.ExecuteNonQuery());
    }

    private Customer CreateCustomer(
        string fullName = "Rachel Cohen",
        string phoneNumber = "0501234567",
        string? email = "rachel@gmail.com")
    {
        var customer = Customer.Create(fullName, phoneNumber, email);
        _customerRepository.Add(customer);
        return customer;
    }

    private void CleanupDatabase()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM dbo.Appointments;
            DELETE FROM dbo.Customers;
            """;

        command.ExecuteNonQuery();
    }
}

[CollectionDefinition("SqlServer integration tests", DisableParallelization = true)]
public sealed class SqlServerIntegrationTestsCollection
{
}