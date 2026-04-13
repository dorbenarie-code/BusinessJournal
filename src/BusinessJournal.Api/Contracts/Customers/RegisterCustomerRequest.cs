using System.ComponentModel.DataAnnotations;

namespace BusinessJournal.Api.Contracts.Customers;

public sealed class RegisterCustomerRequest
{
    [Required]
    [StringLength(200)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string PhoneNumber { get; init; } = string.Empty;

    [EmailAddress]
    [StringLength(320)]
    public string? Email { get; init; }
}