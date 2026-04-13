using System.ComponentModel.DataAnnotations;

namespace BusinessJournal.Api.Contracts.Appointments;

public sealed class RescheduleAppointmentRequest : IValidatableObject
{
    public DateTime StartsAt { get; init; }

    public DateTime EndsAt { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
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