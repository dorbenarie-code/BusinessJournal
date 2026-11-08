using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessJournal.Api.Contracts.Appointments;
using BusinessJournal.Api.Contracts.Customers;
using BusinessJournal.Infrastructure.Data.SqlServer;
using BusinessJournal.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace BusinessJournal.Tests.Api;

/// <summary>
/// Scheduling guarantees under concurrent HTTP requests, checked against the persisted rows.
/// </summary>
[Collection("API integration tests")]
public sealed class AppointmentConcurrencyApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private const int ConcurrentCreateRequests = 20;
    private const int CreateRounds = 5;
    private const int CancelReschedulePairs = 40;

    private const string SlotTakenDetail = "The requested time slot is not available.";
    private const string CancelledDetail = "Cancelled appointments cannot be rescheduled.";

    private static readonly TimeSpan BatchTimeout = TimeSpan.FromSeconds(60);
    private static readonly DateTime BaseSlot = new(2032, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly ApiWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public AppointmentConcurrencyApiTests(ApiWebApplicationFactory factory, ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(output);

        ApiTestDatabase.Cleanup();
        _factory = factory;
        _output = output;
    }

    [Fact]
    public void ApiHost_ConnectsToTheVerifiedDisposableDatabase()
    {
        var expected = new SqlConnectionStringBuilder(DisposableTestDatabase.ConnectionString);
        var connectionFactory = _factory.Services.GetRequiredService<SqlServerConnectionFactory>();

        using var connection = connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT DB_NAME(),
                   (SELECT COUNT(*) FROM {DisposableTestDatabase.MarkerTable} WHERE Token = @Token);
            """;
        command.Parameters.AddWithValue(
            "@Token",
            Environment.GetEnvironmentVariable(DisposableTestDatabase.MarkerTokenVariable));

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        Assert.Equal(expected.InitialCatalog, reader.GetString(0));
        Assert.EndsWith(DisposableTestDatabase.RequiredDatabaseSuffix, reader.GetString(0));
        Assert.Equal(1, reader.GetInt32(1));
    }

    [Fact]
    public async Task ConcurrentCreate_ForTheSameFreeSlot_ExactlyOneRequestSucceeds()
    {
        using var client = await CreateAuthenticatedClientAsync();

        for (var round = 1; round <= CreateRounds; round++)
        {
            ApiTestDatabase.Cleanup();

            var customer = await CreateCustomerAsync(client, $"Concurrent create round {round}");
            var startsAt = BaseSlot.AddDays(round);
            var endsAt = startsAt.AddHours(1);

            var results = await SendConcurrentlyAsync(
                ConcurrentCreateRequests,
                index => client.PostAsJsonAsync("/api/Appointments", new ScheduleAppointmentRequest
                {
                    CustomerId = customer.Id,
                    Title = $"Round {round} request {index}",
                    StartsAt = startsAt,
                    EndsAt = endsAt
                }));

            var activeRows = CountActiveOverlapping(startsAt, endsAt);
            var diagnostics = $"Round {round}: {Describe(results)}; active rows in slot: {activeRows}";
            _output.WriteLine(diagnostics);

            Assert.True(results.Count(r => r.Status == HttpStatusCode.Created) == 1, diagnostics);
            Assert.True(
                results.Count(r => r.Status == HttpStatusCode.Conflict) == ConcurrentCreateRequests - 1,
                diagnostics);
            Assert.All(
                results.Where(r => r.Status == HttpStatusCode.Conflict),
                r => Assert.Equal(SlotTakenDetail, r.Detail));
            Assert.True(activeRows == 1, diagnostics);
        }
    }

    [Fact]
    public async Task CancelThenReschedule_RescheduleIsRejected_AndAppointmentStaysCancelled()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var customer = await CreateCustomerAsync(client, "Cancel first");
        var original = await CreateAppointmentAsync(client, customer.Id, BaseSlot, BaseSlot.AddHours(1));

        using var cancel = await client.PostAsync($"/api/Appointments/{original.Id}/cancel", content: null);
        using var reschedule = await RescheduleAsync(client, original.Id, BaseSlot.AddHours(3));
        var problem = await reschedule.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reschedule.StatusCode);
        Assert.Equal(CancelledDetail, problem?.Detail);

        var stored = ReadPersisted(original.Id);
        Assert.True(stored.IsCancelled);
        Assert.Equal(original.StartsAt, stored.StartsAt);
        Assert.Equal(original.EndsAt, stored.EndsAt);
    }

    [Fact]
    public async Task RescheduleThenCancel_BothSucceed_AndAppointmentEndsCancelled()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var customer = await CreateCustomerAsync(client, "Reschedule first");
        var original = await CreateAppointmentAsync(client, customer.Id, BaseSlot, BaseSlot.AddHours(1));
        var newStartsAt = BaseSlot.AddHours(3);

        using var reschedule = await RescheduleAsync(client, original.Id, newStartsAt);
        using var cancel = await client.PostAsync($"/api/Appointments/{original.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.NoContent, reschedule.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var stored = ReadPersisted(original.Id);
        Assert.True(stored.IsCancelled);
        Assert.Equal(newStartsAt, stored.StartsAt);
        Assert.Equal(newStartsAt.AddHours(1), stored.EndsAt);
    }

    [Fact]
    public async Task ConcurrentCancelAndReschedule_ASuccessfulCancellationIsNeverLost()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var customer = await CreateCustomerAsync(client, "Cancel versus reschedule");

        // Pair i owns two private slots: the original one and the reschedule target.
        var pairs = new List<(AppointmentResponse Original, DateTime TargetStartsAt)>();
        for (var i = 0; i < CancelReschedulePairs; i++)
        {
            var originalStartsAt = BaseSlot.AddHours(i * 4);
            var appointment = await CreateAppointmentAsync(
                client,
                customer.Id,
                originalStartsAt,
                originalStartsAt.AddHours(1));

            pairs.Add((appointment, originalStartsAt.AddHours(2)));
        }

        var results = await SendConcurrentlyAsync(
            CancelReschedulePairs * 2,
            index =>
            {
                var (original, targetStartsAt) = pairs[index / 2];

                return index % 2 == 0
                    ? client.PostAsync($"/api/Appointments/{original.Id}/cancel", content: null)
                    : RescheduleAsync(client, original.Id, targetStartsAt);
            });

        var cancelFirst = 0;
        var rescheduleFirst = 0;
        var failures = new List<string>();

        for (var i = 0; i < CancelReschedulePairs; i++)
        {
            var (original, targetStartsAt) = pairs[i];
            var cancel = results[i * 2];
            var reschedule = results[(i * 2) + 1];
            var stored = ReadPersisted(original.Id);

            var outcome =
                $"pair {i}: cancel={(int)cancel.Status}, reschedule={(int)reschedule.Status}, " +
                $"persisted IsCancelled={stored.IsCancelled}, StartsAt={stored.StartsAt:O}";

            if (cancel.Status != HttpStatusCode.NoContent || !stored.IsCancelled)
            {
                failures.Add(outcome);
            }
            else if (reschedule.Status == HttpStatusCode.NoContent && stored.StartsAt == targetStartsAt)
            {
                rescheduleFirst++;
            }
            else if (reschedule.Status == HttpStatusCode.Conflict
                     && reschedule.Detail == CancelledDetail
                     && stored.StartsAt == original.StartsAt)
            {
                cancelFirst++;
            }
            else
            {
                failures.Add(outcome);
            }
        }

        var summary =
            $"{CancelReschedulePairs} pairs: cancel first={cancelFirst}, reschedule first={rescheduleFirst}, " +
            $"invariant violations={failures.Count}. Status distribution: {Describe(results)}";
        _output.WriteLine(summary);
        failures.ForEach(_output.WriteLine);

        Assert.True(failures.Count == 0, summary + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Create_WhileTheCalendarLockIsHeldElsewhere_Returns503AfterABoundedWait_AndWritesNothing()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var customer = await CreateCustomerAsync(client, "Lock held elsewhere");
        var startsAt = BaseSlot;
        var endsAt = BaseSlot.AddHours(1);
        var request = new ScheduleAppointmentRequest
        {
            CustomerId = customer.Id,
            Title = "Lock timeout probe",
            StartsAt = startsAt,
            EndsAt = endsAt
        };

        HttpStatusCode status;
        string body;
        TimeSpan elapsed;

        using (var holder = new SqlConnection(DisposableTestDatabase.ConnectionString))
        {
            holder.Open();
            using var holderTransaction = holder.BeginTransaction();
            SqlCalendarLock.Acquire(holder, holderTransaction, TimeSpan.FromSeconds(5));

            var stopwatch = Stopwatch.StartNew();
            using var response = await client.PostAsJsonAsync("/api/Appointments", request).WaitAsync(BatchTimeout);
            stopwatch.Stop();

            status = response.StatusCode;
            body = await response.Content.ReadAsStringAsync();
            elapsed = stopwatch.Elapsed;

            holderTransaction.Rollback();
        }

        var activeRows = CountActiveOverlapping(startsAt, endsAt);
        _output.WriteLine(
            $"Lock held elsewhere: HTTP {(int)status} after {elapsed.TotalMilliseconds:F0} ms; active rows in slot: {activeRows}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.InRange(
            elapsed,
            SqlCalendarLock.DefaultTimeout * 0.8,
            SqlCalendarLock.DefaultTimeout + TimeSpan.FromSeconds(10));

        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Service Unavailable", problem?.Title);
        Assert.Equal(SqlCalendarLock.BusyMessage, problem?.Detail);
        Assert.DoesNotContain("sp_getapplock", body);
        Assert.DoesNotContain(SqlCalendarLock.ResourceName, body);
        Assert.DoesNotContain("Exception", body);
        Assert.Equal(0, activeRows);

        // With the lock free again the same request succeeds: the 503 left no lock or row behind.
        using var retry = await client.PostAsJsonAsync("/api/Appointments", request);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(1, CountActiveOverlapping(startsAt, endsAt));
    }

    private Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        return ApiAuthenticationHelper.CreateAuthenticatedClientAsync(_factory);
    }

    private static Task<HttpResponseMessage> RescheduleAsync(HttpClient client, Guid appointmentId, DateTime startsAt)
    {
        return client.PutAsJsonAsync(
            $"/api/Appointments/{appointmentId}/reschedule",
            new RescheduleAppointmentRequest
            {
                StartsAt = startsAt,
                EndsAt = startsAt.AddHours(1)
            });
    }

    /// <summary>
    /// Starts every request at the same moment and returns the results in request order.
    /// </summary>
    private static async Task<RequestResult[]> SendConcurrentlyAsync(
        int count,
        Func<int, Task<HttpResponseMessage>> send)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var requests = Enumerable.Range(0, count)
            .Select(index => Task.Run(async () =>
            {
                await start.Task;

                using var response = await send(index);
                string? detail = null;

                if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
                {
                    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                    detail = problem?.Detail;
                }

                return new RequestResult(response.StatusCode, detail);
            }))
            .ToArray();

        start.SetResult();

        return await Task.WhenAll(requests).WaitAsync(BatchTimeout);
    }

    private static string Describe(IEnumerable<RequestResult> results)
    {
        return string.Join(
            ", ",
            results
                .GroupBy(r => (int)r.Status)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key} x{group.Count()}"));
    }

    private static int CountActiveOverlapping(DateTime startsAt, DateTime endsAt)
    {
        using var connection = new SqlConnection(DisposableTestDatabase.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM dbo.Appointments
            WHERE IsCancelled = 0
              AND StartsAt < @EndsAt
              AND EndsAt > @StartsAt;
            """;
        command.Parameters.Add(new SqlParameter("@StartsAt", System.Data.SqlDbType.DateTime2) { Value = startsAt });
        command.Parameters.Add(new SqlParameter("@EndsAt", System.Data.SqlDbType.DateTime2) { Value = endsAt });

        return (int)command.ExecuteScalar()!;
    }

    private static PersistedAppointment ReadPersisted(Guid appointmentId)
    {
        using var connection = new SqlConnection(DisposableTestDatabase.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT StartsAt, EndsAt, IsCancelled FROM dbo.Appointments WHERE Id = @Id;";
        command.Parameters.AddWithValue("@Id", appointmentId);

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read(), $"Appointment {appointmentId} is not persisted.");

        return new PersistedAppointment(reader.GetDateTime(0), reader.GetDateTime(1), reader.GetBoolean(2));
    }

    private static async Task<CustomerResponse> CreateCustomerAsync(HttpClient client, string fullNamePrefix)
    {
        var response = await client.PostAsJsonAsync("/api/Customers", new RegisterCustomerRequest
        {
            FullName = $"{fullNamePrefix} {Guid.NewGuid():N}",
            PhoneNumber = "0501234567",
            Email = "concurrency@example.com"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }

    private static async Task<AppointmentResponse> CreateAppointmentAsync(
        HttpClient client,
        Guid customerId,
        DateTime startsAt,
        DateTime endsAt)
    {
        var response = await client.PostAsJsonAsync("/api/Appointments", new ScheduleAppointmentRequest
        {
            CustomerId = customerId,
            Title = "Concurrency probe",
            StartsAt = startsAt,
            EndsAt = endsAt
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<AppointmentResponse>())!;
    }

    private sealed record RequestResult(HttpStatusCode Status, string? Detail);

    private sealed record PersistedAppointment(DateTime StartsAt, DateTime EndsAt, bool IsCancelled);
}
