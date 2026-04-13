using BusinessJournal.Domain.Entities;
using BusinessJournal.Infrastructure.Data.SqlServer;
using BusinessJournal.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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
        _connectionString = GetConnectionString();

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
    public void Add_WithValidAppointment_ShouldStoreAppointment()
    {
        var customer = CreateCustomer();

        var appointment = Appointment.Create(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 12, 10, 0, 0),
            new DateTime(2026, 4, 12, 11, 0, 0),
            "First visit");

        _appointmentRepository.Add(appointment);

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

    _appointmentRepository.Add(laterAppointment);
    _appointmentRepository.Add(earlierAppointment);
    _appointmentRepository.Add(otherAppointment);

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

        cancelledAppointment.Cancel();

        var nonOverlappingAppointment = Appointment.Create(
            customer.Id,
            "Nails",
            new DateTime(2026, 4, 12, 12, 0, 0),
            new DateTime(2026, 4, 12, 13, 0, 0));

        _appointmentRepository.Add(overlappingAppointment);
        _appointmentRepository.Add(cancelledAppointment);
        _appointmentRepository.Add(nonOverlappingAppointment);

        var result = _appointmentRepository.GetOverlapping(
            new DateTime(2026, 4, 12, 10, 30, 0),
            new DateTime(2026, 4, 12, 11, 30, 0));

        Assert.Single(result);
        Assert.Equal(overlappingAppointment.Id, result.Single().Id);
    }

    [Fact]
    public void Update_WhenAppointmentIsCancelled_ShouldPersistCancellation()
    {
        var customer = CreateCustomer();

        var appointment = Appointment.Create(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 12, 10, 0, 0),
            new DateTime(2026, 4, 12, 11, 0, 0));

        _appointmentRepository.Add(appointment);

        appointment.Cancel();
        _appointmentRepository.Update(appointment);

        var updatedAppointment = _appointmentRepository.FindById(appointment.Id);

        Assert.NotNull(updatedAppointment);
        Assert.True(updatedAppointment!.IsCancelled);
    }

    [Fact]
    public void Update_WhenAppointmentIsRescheduled_ShouldPersistNewTime()
    {
        var customer = CreateCustomer();

        var appointment = Appointment.Create(
            customer.Id,
            "Hair Color",
            new DateTime(2026, 4, 12, 10, 0, 0),
            new DateTime(2026, 4, 12, 11, 0, 0));

        _appointmentRepository.Add(appointment);

        appointment.Reschedule(
            new DateTime(2026, 4, 12, 12, 0, 0),
            new DateTime(2026, 4, 12, 13, 0, 0));

        _appointmentRepository.Update(appointment);

        var updatedAppointment = _appointmentRepository.FindById(appointment.Id);

        Assert.NotNull(updatedAppointment);
        Assert.Equal(new DateTime(2026, 4, 12, 12, 0, 0), updatedAppointment!.StartsAt);
        Assert.Equal(new DateTime(2026, 4, 12, 13, 0, 0), updatedAppointment.EndsAt);
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

    private static string GetConnectionString()
{
    var projectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    var apiProjectPath = Path.Combine(projectRoot, "src", "BusinessJournal.Api");

    var configuration = new ConfigurationBuilder()
        .SetBasePath(apiProjectPath)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddUserSecrets<SqlAppointmentRepositoryTests>(optional: true)
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

[CollectionDefinition("SqlServer integration tests", DisableParallelization = true)]
public sealed class SqlServerIntegrationTestsCollection
{
}