namespace BusinessJournal.Domain.Common;

public static class TextNormalizer
{
    public static string NormalizeRequired(string value, string paramName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, paramName);
        }

        return value.Trim();
    }

    public static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    public static string NormalizeRequiredEmail(
        string value,
        string? paramName = null,
        string message = "Email is required.")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, paramName ?? nameof(value));
        }

        return value.Trim().ToLowerInvariant();
    }

    public static string? NormalizeOptionalEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}












