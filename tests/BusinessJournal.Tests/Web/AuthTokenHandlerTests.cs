using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BusinessJournal.Web.Features.Auth;
using Xunit;

namespace BusinessJournal.Tests.Web;

public sealed class AuthTokenHandlerTests
{
    [Fact]
    public async Task SendAsync_WhenUserIsNotAuthenticated_ShouldNotAddAuthorizationHeader()
    {
        var session = new AuthSession();

        var handler = new AuthTokenHandler(session)
        {
            InnerHandler = new FakeHttpMessageHandler()
        };

        var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/test");

        var request = FakeHttpMessageHandler.LastRequest!;

        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task SendAsync_WhenUserIsAuthenticated_ShouldAddBearerToken()
    {
        var session = new AuthSession();
        session.SetSession("test-token", DateTime.UtcNow.AddMinutes(10));

        var handler = new AuthTokenHandler(session)
        {
            InnerHandler = new FakeHttpMessageHandler()
        };

        var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/test");

        var request = FakeHttpMessageHandler.LastRequest!;

        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", request.Headers.Authorization.Parameter);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public static HttpRequestMessage? LastRequest;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}