namespace ETimeSheet.Application.Common;

/// <summary>
/// Reads the day codes held in <c>dbo.TimesheetMasterSetup</c> - the
/// <c>char(2)</c> <c>StartDay</c>/<c>EndDay</c> and the <c>char(3)</c>
/// <c>Exceptionday</c> - and turns a pair of them into the set of days a week
/// covers.
/// <para>
/// Kept out of the service because it is pure code-to-calendar translation with
/// no rule in it: the service decides what to do about a non-working day, this
/// only says which days the week contains.
/// </para>
/// </summary>
internal static class TimesheetWeek
{
    /// <summary>
    /// Both widths in one table, because the two columns use different ones and
    /// a caller should not have to know which it is holding. Case-insensitive
    /// because the columns are, and blank-padded values are trimmed by the
    /// parser rather than by every call site.
    /// <para>
    /// Confirmed against live rows: <c>MO</c>/<c>FR</c> and <c>SU</c>/<c>TH</c>
    /// appear as week bounds, <c>SUN</c>/<c>FRI</c> as exception days.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, DayOfWeek> Codes =
        new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
        {
            ["MO"] = DayOfWeek.Monday,
            ["MON"] = DayOfWeek.Monday,
            ["TU"] = DayOfWeek.Tuesday,
            ["TUE"] = DayOfWeek.Tuesday,
            ["WE"] = DayOfWeek.Wednesday,
            ["WED"] = DayOfWeek.Wednesday,
            ["TH"] = DayOfWeek.Thursday,
            ["THU"] = DayOfWeek.Thursday,
            ["FR"] = DayOfWeek.Friday,
            ["FRI"] = DayOfWeek.Friday,
            ["SA"] = DayOfWeek.Saturday,
            ["SAT"] = DayOfWeek.Saturday,
            ["SU"] = DayOfWeek.Sunday,
            ["SUN"] = DayOfWeek.Sunday
        };

    /// <summary>
    /// The day a code names, or null when the code is absent or is not one this
    /// table knows.
    /// <para>
    /// Null rather than an exception: an unrecognised code is data the
    /// application cannot interpret, and refusing to log time because of it
    /// would punish the employee for a setup someone else got wrong. Callers
    /// treat null as "no constraint expressed".
    /// </para>
    /// </summary>
    internal static DayOfWeek? Parse(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return Codes.TryGetValue(code.Trim(), out var day) ? day : null;
    }

    /// <summary>
    /// Every day from <paramref name="start"/> to <paramref name="end"/>
    /// inclusive, walking forward through the week and wrapping past Saturday.
    /// <para>
    /// The wrap is the point: a week configured <c>SU</c> to <c>TH</c> runs
    /// Sunday, Monday, Tuesday, Wednesday, Thursday, and a naive numeric
    /// comparison would call that an empty range. A start equal to the end is a
    /// single-day week, not a seven-day one.
    /// </para>
    /// </summary>
    internal static IReadOnlyCollection<DayOfWeek> Span(DayOfWeek start, DayOfWeek end)
    {
        var days = new List<DayOfWeek>();
        var current = start;

        // Bounded by seven, so a malformed pair can never loop forever.
        for (var step = 0; step < 7; step++)
        {
            days.Add(current);

            if (current == end)
            {
                break;
            }

            current = (DayOfWeek)(((int)current + 1) % 7);
        }

        return days;
    }
}
