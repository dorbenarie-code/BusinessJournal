using System.Net;
using System.Net.Http.Json;

namespace BusinessJournal.Web.Features.Customers;

public sealed class CustomersApiClient
{
    private readonly HttpClient _httpClient;

    public CustomersApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<CustomerResponseModel>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            "/api/customers",
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

        var response = await _httpClient.PostAsJsonAsync(
            "/api/customers",
            input,
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