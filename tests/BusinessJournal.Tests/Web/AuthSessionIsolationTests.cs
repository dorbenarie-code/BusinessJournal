using System.Net;
using System.Text;
using BusinessJournal.Web.Configuration;
using BusinessJournal.Web.Features.Appointments;
using BusinessJournal.Web.Features.Auth;
using BusinessJournal.Web.Features.Customers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace BusinessJournal.Tests.Web;

/// <summary>
/// Regression tests for sign-in isolation in the Blazor Server app.
/// </summary>
/// <remarks>
/// They use the app's real registrations (<see cref="ApiClientServiceCollectionExtensions"/>)
/// and the real IHttpClientFactory; only the network is replaced by a recording handler.
/// Blazor Server gives every circuit its own DI scope, so each scope here stands for one
/// circuit. These tests prove DI scoping and token propagation. They do not run real
/// circuits; that is a separate runtime check with two browsers.
/// </remarks>
public sealed class AuthSessionIsolationTests : IDisposable
{
    private const string TokenA = "test-token-circuit-a";
    private const string TokenB = "test-token-circuit-b";

    private readonly RecordingApiHandler _api = new();
    private readonly ServiceProvider _services;

    public AuthSessionIsolationTests()
    {
        var services = new ServiceCollection();

        services.Configure<ApiOptions>(options => options.BaseUrl = "http://api.test");
        services.AddBusinessJournalApiClients();

        // IHttpClientFactory builds this handler chain once per client name and shares it
        // across scopes, the same way it is shared across circuits in the running app.
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = _api));

        _services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    public void Dispose()
    {
        _services.Dispose();
    }

    [Fact]
    public void Registration_AuthSessionIsScoped_AndNoDelegatingHandlerIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddBusinessJournalApiClients();

        var authSession = Assert.Single(services, d => d.ServiceType == typeof(AuthSession));
        Assert.Equal(ServiceLifetime.Scoped, authSession.Lifetime);

        Assert.DoesNotContain(services, d =>
            !d.IsKeyedService
            && d.ImplementationType is not null
            && typeof(DelegatingHandler).IsAssignableFrom(d.ImplementationType));
    }

    [Fact]
    public void NewCircuit_DoesNotInheritAnotherCircuitsSignIn()
    {
        using var circuitA = _services.CreateScope();
        SignIn(circuitA, TokenA);

        using var circuitB = _services.CreateScope();
        var sessionA = circuitA.ServiceProvider.GetRequiredService<AuthSession>();
        var sessionB = circuitB.ServiceProvider.GetRequiredService<AuthSession>();

        Assert.NotSame(sessionA, sessionB);
        Assert.True(sessionA.IsAuthenticated);
        Assert.False(sessionB.IsAuthenticated);
        Assert.Null(sessionB.AccessToken);
    }

    [Fact]
    public async Task SignedInCircuit_SendsItsToken_AndAnonymousCircuitSendsNoBearer()
    {
        using var circuitA = _services.CreateScope();
        using var circuitB = _services.CreateScope();
        SignIn(circuitA, TokenA);

        await GetCustomersAsync(circuitA);
        await GetCustomersAsync(circuitB);
        await GetAppointmentsAsync(circuitB);

        Assert.Equal(3, _api.Requests.Count);
        AssertBearer(_api.Requests[0], TokenA);
        AssertAnonymous(_api.Requests[1]);
        AssertAnonymous(_api.Requests[2]);
    }

    [Fact]
    public async Task TwoSignedInCircuits_EachSendOnlyTheirOwnToken()
    {
        using var circuitA = _services.CreateScope();
        using var circuitB = _services.CreateScope();
        SignIn(circuitA, TokenA);
        SignIn(circuitB, TokenB);

        // Interleaved calls through both typed clients, which share pooled handlers.
        await GetCustomersAsync(circuitA);
        await GetCustomersAsync(circuitB);
        await GetAppointmentsAsync(circuitB);
        await GetAppointmentsAsync(circuitA);
        await CancelAppointmentAsync(circuitA);
        await CancelAppointmentAsync(circuitB);

        Assert.Equal(6, _api.Requests.Count);
        AssertBearer(_api.Requests[0], TokenA);
        AssertBearer(_api.Requests[1], TokenB);
        AssertBearer(_api.Requests[2], TokenB);
        AssertBearer(_api.Requests[3], TokenA);
        AssertBearer(_api.Requests[4], TokenA);
        AssertBearer(_api.Requests[5], TokenB);
    }

    [Fact]
    public async Task SignOutInOneCircuit_DoesNotAffectAnotherCircuit()
    {
        using var circuitA = _services.CreateScope();
        using var circuitB = _services.CreateScope();
        SignIn(circuitA, TokenA);
        SignIn(circuitB, TokenB);

        circuitA.ServiceProvider.GetRequiredService<AuthSession>().Clear();

        await GetCustomersAsync(circuitA);
        await GetCustomersAsync(circuitB);

        Assert.False(circuitA.ServiceProvider.GetRequiredService<AuthSession>().IsAuthenticated);
        Assert.True(circuitB.ServiceProvider.GetRequiredService<AuthSession>().IsAuthenticated);

        Assert.Equal(2, _api.Requests.Count);
        AssertAnonymous(_api.Requests[0]);
        AssertBearer(_api.Requests[1], TokenB);
    }

    private static void SignIn(IServiceScope circuit, string token)
    {
        circuit.ServiceProvider
            .GetRequiredService<AuthSession>()
            .SetSession(token, DateTime.UtcNow.AddMinutes(30));
    }

    private static Task GetCustomersAsync(IServiceScope circuit)
    {
        return circuit.ServiceProvider
            .GetRequiredService<CustomersApiClient>()
            .GetAllAsync();
    }

    private static Task GetAppointmentsAsync(IServiceScope circuit)
    {
        return circuit.ServiceProvider
            .GetRequiredService<AppointmentsApiClient>()
            .GetByCustomerIdAsync(Guid.NewGuid());
    }

    private static async Task CancelAppointmentAsync(IServiceScope circuit)
    {
        var error = await circuit.ServiceProvider
            .GetRequiredService<AppointmentsApiClient>()
            .CancelAsync(Guid.NewGuid());

        Assert.Null(error);
    }

    private static void AssertBearer(RecordedRequest request, string expectedToken)
    {
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal(expectedToken, request.AuthorizationParameter);
    }

    private static void AssertAnonymous(RecordedRequest request)
    {
        Assert.Null(request.AuthorizationScheme);
        Assert.Null(request.AuthorizationParameter);
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        string Path,
        string? AuthorizationScheme,
        string? AuthorizationParameter);

    private sealed class RecordingApiHandler : HttpMessageHandler
    {
        private readonly List<RecordedRequest> _requests = new();

        public IReadOnlyList<RecordedRequest> Requests
        {
            get
            {
                lock (_requests)
                {
                    return _requests.ToList();
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var authorization = request.Headers.Authorization;

            lock (_requests)
            {
                _requests.Add(new RecordedRequest(
                    request.Method,
                    request.RequestUri?.AbsolutePath ?? string.Empty,
                    authorization?.Scheme,
                    authorization?.Parameter));
            }

            var response = request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.NoContent);

            return Task.FromResult(response);
        }
    }
}
