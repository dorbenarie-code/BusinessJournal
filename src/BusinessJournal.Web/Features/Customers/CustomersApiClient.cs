using System.Net;
using System.Net.Http.Json;
using BusinessJournal.Web.Features.Auth;

namespace BusinessJournal.Web.Features.Customers;

public sealed class CustomersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthSession _authSession;

    public CustomersApiClient(HttpClient httpClient, AuthSession authSession)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(authSession);

        _httpClient = httpClient;
        _authSession = authSession;
    }

    public async Task<IReadOnlyList<CustomerResponseModel>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = AuthorizedRequest.Create(
            _authSession,
            HttpMethod.Get,
            "/api/customers");

        var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("The current user is not authenticated.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Unexpected server error. Status code: {(int)response.StatusCode}");
        }

        var customers = await response.Content.ReadFromJsonAsync<List<CustomerResponseModel>>(
            cancellationToken: cancellationToken);

        return customers ?? [];
    }

    public async Task<CreateCustomerResult> CreateAsync(
        CreateCustomerInputModel input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var request = AuthorizedRequest.Create(
            _authSession,
            HttpMethod.Post,
            "/api/customers",
            JsonContent.Create(input));

        var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return CreateCustomerResult.Failure("You must sign in to create a customer.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return CreateCustomerResult.Failure(
                "The submitted form is invalid. Please check the fields and try again.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return CreateCustomerResult.Failure(
                $"Unexpected server error. Status code: {(int)response.StatusCode}");
        }

        var payload = await response.Content.ReadFromJsonAsync<CustomerResponseModel>(
            cancellationToken: cancellationToken);

        if (payload is null)
        {
            return CreateCustomerResult.Failure("The server returned an invalid response.");
        }

        return CreateCustomerResult.Success(payload);
    }
}