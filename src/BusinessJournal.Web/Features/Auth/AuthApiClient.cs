using System.Net;
using System.Net.Http.Json;

namespace BusinessJournal.Web.Features.Auth;

public sealed class AuthApiClient
{
    private readonly HttpClient _httpClient;

    public AuthApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task<LoginAttemptResult> LoginAsync(
        LoginInputModel input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var response = await _httpClient.PostAsJsonAsync(
            "/api/auth/login",
            input,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return LoginAttemptResult.Failure("Invalid email or password.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return LoginAttemptResult.Failure("The submitted form is invalid. Please check the fields and try again.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return LoginAttemptResult.Failure(
                $"Unexpected server error. Status code: {(int)response.StatusCode}");
        }

        var payload = await response.Content.ReadFromJsonAsync<LoginResponseModel>(
            cancellationToken: cancellationToken);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return LoginAttemptResult.Failure("The server returned an invalid response.");
        }

        return LoginAttemptResult.Success(payload);
    }
}