using System.Net;
using System.Net.Http.Json;
using System.Threading;
using BusinessJournal.Api.Contracts.Appointments;
using BusinessJournal.Api.Contracts.Customers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BusinessJournal.Tests.Api;

[Collection("API integration tests")]
public sealed class AppointmentsApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private static int _slotCounter;

    private readonly ApiWebApplicationFactory _factory;

    public AppointmentsApiTests(ApiWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        ApiTestDatabase.Cleanup();
        _factory = factory;
    }

    [Fact]
    public async Task CreateAppointment_WhenCustomerDoesNotExist_ShouldReturnNotFound()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var (startsAt, endsAt) = CreateUniqueSlot();

        var request = new ScheduleAppointmentRequest
        {
            CustomerId = Guid.NewGuid(),
            Title = "Hair Color",
            StartsAt = startsAt,
            EndsAt = endsAt,
            Notes = "First visit"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Resource Not Found", problem!.Title);
        Assert.Equal("Customer does not exist.", problem.Detail);
    }

    [Fact]
    public async Task CreateAppointment_WithValidRequest_ShouldReturnCreatedAppointment()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var customer = await CreateCustomerAsync(
            client,
            "Rachel Cohen",
            "0501234567",
            "Rachel@Gmail.com");

        var (startsAt, endsAt) = CreateUniqueSlot();

        var request = new ScheduleAppointmentRequest
        {
            CustomerId = customer.Id,
            Title = "Hair Color",
            StartsAt = startsAt,
            EndsAt = endsAt,
            Notes = "First visit"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var appointment = await response.Content.ReadFromJsonAsync<AppointmentResponse>();

        Assert.NotNull(appointment);
        Assert.NotEqual(Guid.Empty, appointment!.Id);
        Assert.Equal(customer.Id, appointment.CustomerId);
        Assert.Equal("Hair Color", appointment.Title);
        Assert.Equal(startsAt, appointment.StartsAt);
        Assert.Equal(endsAt, appointment.EndsAt);
        Assert.Equal("First visit", appointment.Notes);
        Assert.False(appointment.IsCancelled);

        Assert.NotNull(response.Headers.Location);
        Assert.Contains($"/api/Appointments/{appointment.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task CancelAppointment_WhenAppointmentExists_ShouldReturnNoContentAndPersistCancellation()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var customer = await CreateCustomerAsync(
            client,
            "Michal Levi",
            "0521234567",
            "Michal@Gmail.com");

        var (startsAt, endsAt) = CreateUniqueSlot();

        var appointment = await CreateAppointmentAsync(
            client,
            customer.Id,
            "Hair Color",
            startsAt,
            endsAt,
            "First visit");

        var cancelResponse = await client.PostAsync(
            $"/api/Appointments/{appointment.Id}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/Appointments/{appointment.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var storedAppointment = await getResponse.Content.ReadFromJsonAsync<AppointmentResponse>();

        Assert.NotNull(storedAppointment);
        Assert.True(storedAppointment!.IsCancelled);
    }

    [Fact]
    public async Task RescheduleAppointment_WhenAppointmentIsCancelled_ShouldReturnConflict()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var customer = await CreateCustomerAsync(
            client,
            "Dana Levi",
            "0501111111",
            "Dana@Gmail.com");

        var (startsAt, endsAt) = CreateUniqueSlot();

        var appointment = await CreateAppointmentAsync(
            client,
            customer.Id,
            "Hair Cut",
            startsAt,
            endsAt);

        var cancelResponse = await client.PostAsync(
            $"/api/Appointments/{appointment.Id}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var (newStartsAt, newEndsAt) = CreateUniqueSlot();

        var request = new RescheduleAppointmentRequest
        {
            StartsAt = newStartsAt,
            EndsAt = newEndsAt
        };

        var response = await client.PutAsJsonAsync(
            $"/api/Appointments/{appointment.Id}/reschedule",
            request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Business Conflict", problem!.Title);
        Assert.Equal("Cancelled appointments cannot be rescheduled.", problem.Detail);
    }

    [Fact]
    public async Task GetAppointmentById_WhenAppointmentDoesNotExist_ShouldReturnNotFound()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/Appointments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelAppointment_WhenAppointmentDoesNotExist_ShouldReturnNotFound()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsync(
            $"/api/Appointments/{Guid.NewGuid()}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Resource Not Found", problem!.Title);
        Assert.Equal("Appointment does not exist.", problem.Detail);
    }

    [Fact]
    public async Task CreateAppointment_WithInvalidTimeRange_ShouldReturnBadRequest()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var customer = await CreateCustomerAsync(
            client,
            "Shani Cohen",
            "0503333333",
            "Shani@Gmail.com");

        var startsAt = new DateTime(2031, 1, 1, 12, 0, 0);
        var endsAt = new DateTime(2031, 1, 1, 11, 0, 0);

        var request = new ScheduleAppointmentRequest
        {
            CustomerId = customer.Id,
            Title = "Hair Color",
            StartsAt = startsAt,
            EndsAt = endsAt,
            Notes = "Invalid time range"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RescheduleAppointment_WithInvalidTimeRange_ShouldReturnBadRequest()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var customer = await CreateCustomerAsync(
            client,
            "Noa Levi",
            "0504444444",
            "Noa@Gmail.com");

        var (startsAt, endsAt) = CreateUniqueSlot();

        var appointment = await CreateAppointmentAsync(
            client,
            customer.Id,
            "Hair Cut",
            startsAt,
            endsAt);

        var request = new RescheduleAppointmentRequest
        {
            StartsAt = new DateTime(2031, 1, 2, 15, 0, 0),
            EndsAt = new DateTime(2031, 1, 2, 14, 0, 0)
        };

        var response = await client.PutAsJsonAsync(
            $"/api/Appointments/{appointment.Id}/reschedule",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAppointment_WhenTimeSlotIsTaken_ShouldReturnConflict()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var firstCustomer = await CreateCustomerAsync(
            client,
            "Dana Levi",
            "0505555555",
            "Dana@Gmail.com");

        var secondCustomer = await CreateCustomerAsync(
            client,
            "Michal Cohen",
            "0506666666",
            "Michal@Gmail.com");

        var (startsAt, endsAt) = CreateUniqueSlot();

        await CreateAppointmentAsync(
            client,
            firstCustomer.Id,
            "Hair Color",
            startsAt,
            endsAt);

        var request = new ScheduleAppointmentRequest
        {
            CustomerId = secondCustomer.Id,
            Title = "Hair Cut",
            StartsAt = startsAt.AddMinutes(30),
            EndsAt = endsAt.AddMinutes(30),
            Notes = "Overlapping request"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Business Conflict", problem!.Title);
        Assert.Equal("The requested time slot is not available.", problem.Detail);
    }

    [Fact]
    public async Task GetAppointmentsForCustomer_WhenCustomerHasAppointments_ShouldReturnOkAndAppointments()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var customer = await CreateCustomerAsync(
            client,
            "Rachel Cohen",
            "0501234567",
            "Rachel@Gmail.com");

        var (firstStartsAt, firstEndsAt) = CreateUniqueSlot();
        var (secondStartsAt, secondEndsAt) = CreateUniqueSlot();

        var firstAppointment = await CreateAppointmentAsync(
            client,
            customer.Id,
            "Hair Color",
            firstStartsAt,
            firstEndsAt,
            "First visit");

        var secondAppointment = await CreateAppointmentAsync(
            client,
            customer.Id,
            "Hair Cut",
            secondStartsAt,
            secondEndsAt,
            "Follow-up");

        var response = await client.GetAsync($"/api/Appointments/by-customer/{customer.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var appointments = await response.Content.ReadFromJsonAsync<List<AppointmentResponse>>();

        Assert.NotNull(appointments);
        Assert.Contains(appointments, appointment => appointment.Id == firstAppointment.Id);
        Assert.Contains(appointments, appointment => appointment.Id == secondAppointment.Id);
    }
    [Fact]
public async Task CreateAppointment_WithTitleLongerThan200Characters_ShouldReturnBadRequest()
{
    using var client = await CreateAuthenticatedClientAsync();

    var customer = await CreateCustomerAsync(
        client,
        "Rachel Cohen",
        "0501234567",
        "Rachel@Gmail.com");

    var (startsAt, endsAt) = CreateUniqueSlot();

    var request = new ScheduleAppointmentRequest
    {
        CustomerId = customer.Id,
        Title = new string('A', 201),
        StartsAt = startsAt,
        EndsAt = endsAt,
        Notes = "First visit"
    };

    var response = await client.PostAsJsonAsync("/api/Appointments", request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}

    [Fact]
    public async Task GetAppointmentsForCustomer_WhenCustomerHasNoAppointments_ShouldReturnOkAndEmptyList()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var customer = await CreateCustomerAsync(
            client,
            "Michal Levi",
            "0521234567",
            "Michal@Gmail.com");

        var response = await client.GetAsync($"/api/Appointments/by-customer/{customer.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var appointments = await response.Content.ReadFromJsonAsync<List<AppointmentResponse>>();

        Assert.NotNull(appointments);
        Assert.Empty(appointments!);
    }

    private Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        return ApiAuthenticationHelper.CreateAuthenticatedClientAsync(_factory);
    }

    private static async Task<CustomerResponse> CreateCustomerAsync(
        HttpClient client,
        string fullNamePrefix,
        string phoneNumber,
        string email)
    {
        var request = new RegisterCustomerRequest
        {
            FullName = $"{fullNamePrefix} {Guid.NewGuid():N}",
            PhoneNumber = phoneNumber,
            Email = email
        };

        var response = await client.PostAsJsonAsync("/api/Customers", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>();

        Assert.NotNull(customer);

        return customer!;
    }

    private static async Task<AppointmentResponse> CreateAppointmentAsync(
        HttpClient client,
        Guid customerId,
        string title,
        DateTime startsAt,
        DateTime endsAt,
        string? notes = null)
    {
        var request = new ScheduleAppointmentRequest
        {
            CustomerId = customerId,
            Title = title,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Notes = notes
        };

        var response = await client.PostAsJsonAsync("/api/Appointments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var appointment = await response.Content.ReadFromJsonAsync<AppointmentResponse>();

        Assert.NotNull(appointment);

        return appointment!;
    }

    private static (DateTime StartsAt, DateTime EndsAt) CreateUniqueSlot()
    {
        var slot = Interlocked.Increment(ref _slotCounter);

        var startsAt = DateTime.UtcNow
            .AddDays(30)
            .AddHours(slot * 2);

        return (startsAt, startsAt.AddHours(1));
    }
}