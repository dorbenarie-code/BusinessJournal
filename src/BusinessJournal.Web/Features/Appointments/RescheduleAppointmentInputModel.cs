using System.ComponentModel.DataAnnotations;

namespace BusinessJournal.Web.Features.Appointments;

public sealed class RescheduleAppointmentInputModel : IValidatableObject
{
    [Required]
    public string StartsAt { get; set; } = string.Empty;

    [Required]
    public string EndsAt { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!TryParseLocalDateTime(StartsAt, out var startsAt))
        {
            yield return new ValidationResult(
                "Start time is required.",
                new[] { nameof(StartsAt) });
        }

        if (!TryParseLocalDateTime(EndsAt, out var endsAt))
        {
            yield return new ValidationResult(
                "End time is required.",
                new[] { nameof(EndsAt) });
        }

        if (TryParseLocalDateTime(StartsAt, out startsAt)
            && TryParseLocalDateTime(EndsAt, out endsAt)
            && endsAt <= startsAt)
        {
            yield return new ValidationResult(
                "End time must be later than start time.",
                new[] { nameof(StartsAt), nameof(EndsAt) });
        }
    }

    public bool TryGetStartsAt(out DateTime startsAt)
    {
        return TryParseLocalDateTime(StartsAt, out startsAt);
    }

    public bool TryGetEndsAt(out DateTime endsAt)
    {
        return TryParseLocalDateTime(EndsAt, out endsAt);
    }

    private static bool TryParseLocalDateTime(string? value, out DateTime result)
    {
        return DateTime.TryParse(value, out result);
    }
}