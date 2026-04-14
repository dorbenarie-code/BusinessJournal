using System.ComponentModel.DataAnnotations;

namespace BusinessJournal.Api.Contracts.Appointments;

public sealed class RescheduleAppointmentRequest : IValidatableObject
{
    public DateTime StartsAt { get; init; }

    public DateTime EndsAt { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
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