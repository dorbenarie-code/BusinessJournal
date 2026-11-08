using BusinessJournal.Web.Features.Appointments;
using BusinessJournal.Web.Features.Auth;
using BusinessJournal.Web.Features.Customers;
using Microsoft.Extensions.Options;

namespace BusinessJournal.Web.Configuration;

public static class ApiClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers the sign-in state and the typed API clients.
    /// </summary>
    /// <remarks>
    /// Blazor Server creates one DI scope per circuit, so a scoped <see cref="AuthSession"/>
    /// belongs to exactly one circuit (one browser tab). The API clients are transient and
    /// receive the <see cref="AuthSession"/> of the circuit that resolved them, then attach
    /// the token to each request themselves.
    /// The token is deliberately not attached by a DelegatingHandler: IHttpClientFactory
    /// builds and pools handlers in its own DI scope and shares them across all circuits.
    /// </remarks>
    public static IServiceCollection AddBusinessJournalApiClients(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<AuthSession>();

        services.AddHttpClient<AuthApiClient>(ConfigureApiBaseAddress);
        services.AddHttpClient<CustomersApiClient>(ConfigureApiBaseAddress);
        services.AddHttpClient<AppointmentsApiClient>(ConfigureApiBaseAddress);

        return services;
    }

    private static void ConfigureApiBaseAddress(IServiceProvider serviceProvider, HttpClient httpClient)
    {
        var apiOptions = serviceProvider
            .GetRequiredService<IOptions<ApiOptions>>()
            .Value;

        if (string.IsNullOrWhiteSpace(apiOptions.BaseUrl))
        {
            throw new InvalidOperationException("Api:BaseUrl is required.");
        }

        httpClient.BaseAddress = new Uri(apiOptions.BaseUrl);
    }
}
