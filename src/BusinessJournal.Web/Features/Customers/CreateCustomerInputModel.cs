using System.ComponentModel.DataAnnotations;

namespace BusinessJournal.Web.Features.Customers;

public sealed class CreateCustomerInputModel
{
    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(320)]
    public string? Email { get; set; }
}