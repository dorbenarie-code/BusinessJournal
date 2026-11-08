using System.Net.Http.Headers;

namespace BusinessJournal.Web.Features.Auth;

/// <summary>
/// Builds API requests that carry the bearer token of a single circuit.
/// </summary>
internal static class AuthorizedRequest
{
    public static HttpRequestMessage Create(
        AuthSession authSession,
        HttpMethod method,
        string requestUri,
        HttpContent? content = null)
    {
        ArgumentNullException.ThrowIfNull(authSession);

        var request = new HttpRequestMessage(method, requestUri)
        {
            Content = content
        };

        // The token goes on this request only. It is never stored on
        // HttpClient.DefaultRequestHeaders or in a pooled message handler,
        // because those can be reused by other circuits.
        if (authSession.IsAuthenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                authSession.AccessToken);
        }

        return request;
    }
}
