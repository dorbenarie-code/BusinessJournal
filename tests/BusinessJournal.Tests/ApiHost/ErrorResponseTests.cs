using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BusinessJournal.Api.ExceptionHandling;
using BusinessJournal.Application.Contracts.Appointments;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;
using BusinessJournal.Infrastructure.Data.SqlServer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BusinessJournal.Tests.ApiHost;

[Collection(ApiHostCollection.Name)]
public sealed class ErrorResponseTests : IClassFixture<SqlFreeApiFactory>
{
    private readonly SqlFreeApiFactory _factory;

    public ErrorResponseTests(SqlFreeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnexpectedServerError_Returns500WithoutInternalDetails()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/customers");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertGenericServerError(response, body);
    }

    [Fact]
    public async Task ServerGuardClauseFailure_IsTreatedAsUnexpected_NotAsBadRequest()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/customers/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertGenericServerError(response, body);
        Assert.DoesNotContain(SqlFreeApiFactory.InternalParameterName, body);
        Assert.DoesNotContain("Value cannot be null", body);
    }

    [Fact]
    public async Task KnownBusinessError_KeepsStatusAndMessage()
    {
        using var client = CreateAuthenticatedClient();

        var response = await client.PostAsync($"/api/appointments/{Guid.NewGuid()}/cancel", content: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(body);
        var root = problem.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Resource Not Found", root.GetProperty("title").GetString());
        Assert.Equal("Appointment does not exist.", root.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task CalendarLockTimeout_Returns503WithSafeMessage_NotConflict()
    {
        using var factory = WithCalendarLockReturnCode(-1);
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsync($"/api/appointments/{Guid.NewGuid()}/cancel", content: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(body);
        var root = problem.RootElement;

        Assert.Equal(503, root.GetProperty("status").GetInt32());
        Assert.Equal("Service Unavailable", root.GetProperty("title").GetString());
        Assert.Equal(SqlCalendarLock.BusyMessage, root.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));

        Assert.DoesNotContain("sp_getapplock", body);
        Assert.DoesNotContain(SqlCalendarLock.ResourceName, body);
        Assert.DoesNotContain("Exception", body);
    }

    [Fact]
    public async Task CalendarLockDeadlockVictim_IsAGenericServerError_NotConflictOrUnavailable()
    {
        using var factory = WithCalendarLockReturnCode(-3);
        using var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsync($"/api/appointments/{Guid.NewGuid()}/cancel", content: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertGenericServerError(response, body);
        Assert.DoesNotContain("sp_getapplock", body);
    }

    private WebApplicationFactory<Program> WithCalendarLockReturnCode(int returnCode)
    {
        return _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAppointmentRepository>();
            services.AddSingleton<IAppointmentRepository>(new LockFailingAppointmentRepository(returnCode));
        }));
    }

    /// <summary>
    /// Every calendar change fails the way the SQL repository fails for the given sp_getapplock code.
    /// </summary>
    private sealed class LockFailingAppointmentRepository : IAppointmentRepository
    {
        private readonly int _returnCode;

        public LockFailingAppointmentRepository(int returnCode)
        {
            _returnCode = returnCode;
        }

        public bool TryAdd(Appointment appointment)
        {
            SqlCalendarLock.ThrowIfNotGranted(_returnCode);
            throw new InvalidOperationException("The lock was expected to fail.");
        }

        public RescheduleAppointmentResult TryReschedule(Guid appointmentId, TimeRange newTime)
        {
            SqlCalendarLock.ThrowIfNotGranted(_returnCode);
            throw new InvalidOperationException("The lock was expected to fail.");
        }

        public bool TryCancel(Guid appointmentId)
        {
            SqlCalendarLock.ThrowIfNotGranted(_returnCode);
            throw new InvalidOperationException("The lock was expected to fail.");
        }

        public Appointment? FindById(Guid appointmentId)
        {
            return null;
        }

        public IReadOnlyCollection<Appointment> GetByCustomerId(Guid customerId)
        {
            return Array.Empty<Appointment>();
        }

        public IReadOnlyCollection<Appointment> GetOverlapping(DateTime startsAt, DateTime endsAt)
        {
            return Array.Empty<Appointment>();
        }
    }

    private static void AssertGenericServerError(HttpResponseMessage response, string body)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(body);
        var root = problem.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal(GlobalExceptionHandler.UnexpectedErrorTitle, root.GetProperty("title").GetString());
        Assert.Equal(GlobalExceptionHandler.UnexpectedErrorDetail, root.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));

        // Nothing from the original exception may leak: message, type or stack trace.
        Assert.DoesNotContain(SqlFreeApiFactory.SensitiveDetailMarker, body);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("BusinessJournal.Tests", body);
        Assert.DoesNotContain("StackTrace", body);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        return CreateAuthenticatedClient(_factory);
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();

        // Token from the host's own generator; no credentials are involved.
        var tokenGenerator = factory.Services.GetRequiredService<IJwtTokenGenerator>();
        var token = tokenGenerator.Generate(
            AppUser.Create("error-tests@example.invalid", "not-a-real-password-hash"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);

        return client;
    }
}
