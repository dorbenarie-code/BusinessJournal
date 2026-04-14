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

    public string? Notes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CustomerId == Guid.Empty)
        {
            yield return new ValidationResult(
                "Customer id is required.",
                new[] { nameof(CustomerId) });
        }

        foreach (var validationResult in AppointmentTimeValidation.Validate(
                     StartsAt,
                     EndsAt,
                     nameof(StartsAt),
                     nameof(EndsAt)))
        {
            yield return validationResult;
        }
    }
}