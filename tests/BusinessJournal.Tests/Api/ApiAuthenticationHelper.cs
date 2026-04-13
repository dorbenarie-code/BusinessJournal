using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;

namespace BusinessJournal.Tests.Api;

internal static class ApiAuthenticationHelper
{
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static string? _cachedAccessToken;

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        ApiWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var client = factory.CreateClient();
        var accessToken = await GetAccessTokenAsync(factory);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    public static async Task<string> GetAccessTokenAsync(
        ApiWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (!string.IsNullOrWhiteSpace(_cachedAccessToken))
        {
            return _cachedAccessToken;
        }

        await TokenLock.WaitAsync();

        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedAccessToken))
            {
                return _cachedAccessToken;
            }

            using var client = factory.CreateClient();

            var loginRequest = new LoginRequest
            {
                Email = "admin@businessjournal.com",
                Password = "Admin123!"
            };

            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            loginResponse.EnsureSuccessStatusCode();

            var payload = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

            if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                throw new InvalidOperationException("Could not obtain access token for API tests.");
            }

            _cachedAccessToken = payload.AccessToken;
            return _cachedAccessToken;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private sealed class LoginRequest
    {
        public string Email { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }

    private sealed class LoginResponse
    {
        public string AccessToken { get; init; } = string.Empty;
        public DateTime ExpiresAtUtc { get; init; }
    }
}