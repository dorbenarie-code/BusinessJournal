namespace BusinessJournal.Api.Contracts.Customers;

public sealed class CustomerResponse
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Email { get; init; }
}