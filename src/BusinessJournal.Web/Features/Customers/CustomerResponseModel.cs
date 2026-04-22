namespace BusinessJournal.Web.Features.Customers;

public sealed class CustomerResponseModel
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Email { get; init; }
}