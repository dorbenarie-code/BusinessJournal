using System.ComponentModel.DataAnnotations;

namespace BusinessJournal.Web.Features.Auth;

public sealed class LoginInputModel
{
    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Password { get; set; } = string.Empty;
}