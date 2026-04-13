using System.ComponentModel.DataAnnotations;

namespace BusinessJournal.Api.Contracts.Appointments;

public sealed class ScheduleAppointmentRequest : IValidatableObject
{
    [Required]
    public Guid CustomerId { get; init; }

    [Required]
    [StringLength(200)]
    public string Title { get; init; } = string.Empty;

    public DateTime StartsAt { get; init; }

    public DateTime EndsAt { get; init; }

    [StringLength(1000)]
    public string? Notes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CustomerId == Guid.Empty)
        {
            yield return new ValidationResult(
                "Customer id is required.",
                new[] { nameof(CustomerId) });
        }

        if (StartsAt == default)
        {
            yield return new ValidationResult(
                "Start time is required.",
                new[] { nameof(StartsAt) });
        }

        if (EndsAt == default)
        {
            yield return new ValidationResult(
                "End time is required.",
                new[] { nameof(EndsAt) });
        }

        if (StartsAt != default && EndsAt != default && EndsAt <= StartsAt)
        {
            yield return new ValidationResult(
                "End time must be later than start time.",
                new[] { nameof(StartsAt), nameof(EndsAt) });
        }
    }
}