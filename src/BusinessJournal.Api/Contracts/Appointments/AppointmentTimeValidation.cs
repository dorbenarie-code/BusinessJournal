using System.ComponentModel.DataAnnotations;

namespace BusinessJournal.Api.Contracts.Appointments;

internal static class AppointmentTimeValidation
{
    public static IEnumerable<ValidationResult> Validate(
        DateTime startsAt,
        DateTime endsAt,
        string startsAtMemberName = "StartsAt",
        string endsAtMemberName = "EndsAt")
    {
        if (startsAt == default)
        {
            yield return new ValidationResult(
                "Start time is required.",
                new[] { startsAtMemberName });
        }

        if (endsAt == default)
        {
            yield return new ValidationResult(
                "End time is required.",
                new[] { endsAtMemberName });
        }

        if (startsAt != default && endsAt != default && endsAt <= startsAt)
        {
            yield return new ValidationResult(
                "End time must be later than start time.",
                new[] { startsAtMemberName, endsAtMemberName });
        }
    }
}