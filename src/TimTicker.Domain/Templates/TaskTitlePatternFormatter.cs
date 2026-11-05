using System.Globalization;

namespace TimTicker.Domain.Templates;

public static class TaskTitlePatternFormatter
{
    public static string Format(string pattern, DateOnly date, int sequenceNumber)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (date == default)
        {
            throw new ArgumentException("The generation date must not be empty.", nameof(date));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequenceNumber);

        return pattern
            .Replace("%y", date.Year.ToString("0000", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("%m", date.Month.ToString("00", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("%d", date.Day.ToString("00", CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("%n", sequenceNumber.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
