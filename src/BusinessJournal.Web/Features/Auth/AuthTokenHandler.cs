using System.Net.Http.Headers;

namespace BusinessJournal.Web.Features.Auth;

public sealed class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthSession _authSession;

    public AuthTokenHandler(AuthSession authSession)
    {
        ArgumentNullException.ThrowIfNull(authSession);
        _authSession = authSession;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_authSession.IsAuthenticated &&
            !string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _authSession.AccessToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}