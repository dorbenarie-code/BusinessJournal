namespace BusinessJournal.Web.Features.Customers;

public sealed class CreateCustomerResult
{
    private CreateCustomerResult(
        bool isSuccess,
        CustomerResponseModel? customer,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        Customer = customer;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public CustomerResponseModel? Customer { get; }
    public string? ErrorMessage { get; }

    public static CreateCustomerResult Success(CustomerResponseModel customer) =>
        new(true, customer, null);

    public static CreateCustomerResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}