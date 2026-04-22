using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BusinessJournal.Web.Features.Customers;
using Xunit;

namespace BusinessJournal.Tests.Web;

public sealed class CustomersApiClientTests
{
    [Fact]
    public async Task GetAllAsync_WhenSuccess_ShouldReturnCustomers()
    {
        var customers = new[]
        {
            new CustomerResponseModel
            {
                Id = Guid.NewGuid(),
                FullName = "Test User",
                PhoneNumber = "0500000000",
                Email = "test@test.com"
            }
        };

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, customers);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var apiClient = new CustomersApiClient(client);

        var result = await apiClient.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("Test User", result[0].FullName);
    }

    [Fact]
    public async Task GetAllAsync_WhenUnauthorized_ShouldThrow()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, null);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var apiClient = new CustomersApiClient(client);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            apiClient.GetAllAsync());
    }

    [Fact]
    public async Task CreateAsync_WhenSuccess_ShouldReturnSuccess()
    {
        var customer = new CustomerResponseModel
        {
            Id = Guid.NewGuid(),
            FullName = "New User",
            PhoneNumber = "0500000000",
            Email = "new@test.com"
        };

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, customer);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var apiClient = new CustomersApiClient(client);

        var result = await apiClient.CreateAsync(new CreateCustomerInputModel
        {
            FullName = "New User",
            PhoneNumber = "0500000000",
            Email = "new@test.com"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Customer);
    }

    [Fact]
    public async Task CreateAsync_WhenBadRequest_ShouldReturnFailure()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, null);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var apiClient = new CustomersApiClient(client);

        var result = await apiClient.CreateAsync(new CreateCustomerInputModel());

        Assert.False(result.IsSuccess);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object? _response;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, object? response)
        {
            _statusCode = statusCode;
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var message = new HttpResponseMessage(_statusCode);

            if (_response is not null)
            {
                var json = JsonSerializer.Serialize(_response);
                message.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return Task.FromResult(message);
        }
    }
}