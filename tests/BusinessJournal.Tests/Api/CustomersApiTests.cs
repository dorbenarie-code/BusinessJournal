using System.Net;
using System.Net.Http.Json;
using BusinessJournal.Api.Contracts.Customers;
using Xunit;

namespace BusinessJournal.Tests.Api;

[Collection("API integration tests")]
public sealed class CustomersApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public CustomersApiTests(ApiWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        ApiTestDatabase.Cleanup();
        _factory = factory;
    }

    [Fact]
    public async Task CreateCustomer_WithValidRequest_ShouldReturnCreatedCustomer()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var request = new RegisterCustomerRequest
        {
            FullName = $"Rachel Cohen {Guid.NewGuid():N}",
            PhoneNumber = "0501234567",
            Email = "Rachel@Gmail.com"
        };

        var response = await client.PostAsJsonAsync("/api/Customers", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>();

        Assert.NotNull(customer);
        Assert.NotEqual(Guid.Empty, customer!.Id);
        Assert.Equal(request.FullName, customer.FullName);
        Assert.Equal("0501234567", customer.PhoneNumber);
        Assert.Equal("rachel@gmail.com", customer.Email);

        Assert.NotNull(response.Headers.Location);
        Assert.Contains($"/api/Customers/{customer.Id}", response.Headers.Location!.ToString());
    }
    [Fact]
public async Task CreateCustomer_WithFullNameLongerThan200Characters_ShouldReturnBadRequest()
{
    using var client = await CreateAuthenticatedClientAsync();

    var request = new RegisterCustomerRequest
    {
        FullName = new string('A', 201),
        PhoneNumber = "0501234567",
        Email = "rachel@gmail.com"
    };

    var response = await client.PostAsJsonAsync("/api/Customers", request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}

    [Fact]
    public async Task GetCustomerById_WhenCustomerExists_ShouldReturnOk()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var createRequest = new RegisterCustomerRequest
        {
            FullName = $"Michal Levi {Guid.NewGuid():N}",
            PhoneNumber = "0521234567",
            Email = "Michal@Gmail.com"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Customers", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdCustomer = await createResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(createdCustomer);

        var response = await client.GetAsync($"/api/Customers/{createdCustomer!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>();

        Assert.NotNull(customer);
        Assert.Equal(createdCustomer.Id, customer!.Id);
        Assert.Equal(createRequest.FullName, customer.FullName);
        Assert.Equal("0521234567", customer.PhoneNumber);
        Assert.Equal("michal@gmail.com", customer.Email);
    }

    [Fact]
    public async Task GetCustomerById_WhenCustomerDoesNotExist_ShouldReturnNotFound()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/Customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAllCustomers_WhenCustomersExist_ShouldReturnOkAndContainCreatedCustomers()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var firstRequest = new RegisterCustomerRequest
        {
            FullName = $"Dana Levi {Guid.NewGuid():N}",
            PhoneNumber = "0501111111",
            Email = "Dana@Gmail.com"
        };

        var secondRequest = new RegisterCustomerRequest
        {
            FullName = $"Shani Cohen {Guid.NewGuid():N}",
            PhoneNumber = "0502222222",
            Email = "Shani@Gmail.com"
        };

        var firstCreateResponse = await client.PostAsJsonAsync("/api/Customers", firstRequest);
        var secondCreateResponse = await client.PostAsJsonAsync("/api/Customers", secondRequest);

        Assert.Equal(HttpStatusCode.Created, firstCreateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondCreateResponse.StatusCode);

        var firstCustomer = await firstCreateResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        var secondCustomer = await secondCreateResponse.Content.ReadFromJsonAsync<CustomerResponse>();

        Assert.NotNull(firstCustomer);
        Assert.NotNull(secondCustomer);

        var response = await client.GetAsync("/api/Customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var customers = await response.Content.ReadFromJsonAsync<List<CustomerResponse>>();

        Assert.NotNull(customers);
        Assert.Contains(customers, customer => customer.Id == firstCustomer!.Id);
        Assert.Contains(customers, customer => customer.Id == secondCustomer!.Id);
    }

    private Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        return ApiAuthenticationHelper.CreateAuthenticatedClientAsync(_factory);
    }
}