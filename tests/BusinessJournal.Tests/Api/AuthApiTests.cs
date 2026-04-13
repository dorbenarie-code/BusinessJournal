using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace BusinessJournal.Tests.Api;

[Collection("API integration tests")]
public sealed class AuthApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public AuthApiTests(ApiWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnAccessToken()
    {
        using var client = _factory.CreateClient();

        var request = new LoginRequest
        {
            Email = "admin@businessjournal.com",
            Password = "Admin123!"
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.True(payload.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var request = new LoginRequest
        {
            Email = "admin@businessjournal.com",
            Password = "WrongPassword123!"
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var request = new LoginRequest
        {
            Email = "unknown@businessjournal.com",
            Password = "Admin123!"
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomers_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomers_WithInvalidToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Fact]
public async Task Login_WithEmailLongerThan320Characters_ShouldReturnBadRequest()
{
    using var client = _factory.CreateClient();

    var localPart = new string('a', 311);

    var request = new LoginRequest
    {
        Email = $"{localPart}@gmail.com",
        Password = "Admin123!"
    };

    var response = await client.PostAsJsonAsync("/api/auth/login", request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}

    [Fact]
    public async Task GetCustomers_WithValidToken_ShouldReturnOk()
    {
        using var client = _factory.CreateClient();

        var accessToken = await ApiAuthenticationHelper.GetAccessTokenAsync(_factory);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
