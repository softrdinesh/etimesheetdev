using ETimeSheet.Shared.Utilities;

namespace ETimeSheet.Application.Common;

/// <summary>
/// Reads the day ids held in <c>dbo.TimesheetMasterSetup</c> - <c>StartDay</c>,
/// <c>EndDay</c> and <c>Exceptionday</c>, all <c>int</c> columns holding
/// <c>dbo.DayMaster.DayID</c> - and turns a pair of them into the set of days a
/// week covers.
/// <para>
/// Kept out of the service because it is pure code-to-calendar translation with
/// no rule in it: the service decides what to do about a non-working day, this
/// only says which days the week contains.
/// </para>
/// <para>
/// <b>Until 2026-09-17 those three columns held letter codes</b> - "MO", "SUN" -
/// and this class parsed strings. They are day ids now, which removes the
/// guesswork: the vocabulary is whatever <c>dbo.DayMaster</c> contains, and that
/// is fixed at seven rows.
/// </para>
/// </summary>
internal static class TimesheetWeek
{
    /// <summary>
    /// <c>dbo.DayMaster.DayID</c> to <see cref="DayOfWeek"/>.
    /// <para>
    /// A table rather than arithmetic, because the two numberings disagree in a
    /// way that is easy to get subtly wrong: DayMaster is ISO-8601, Monday 1 to
    /// Sunday 7, while <see cref="DayOfWeek"/> runs Sunday 0 to Saturday 6. The
    /// obvious <c>(DayOfWeek)dayId</c> is wrong for all seven days, and
    /// <c>dayId % 7</c> is right only by coincidence of ordering - so neither is
    /// used.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<int, DayOfWeek> Days =
        new Dictionary<int, DayOfWeek>
        {
            [Constants.DayMaster.DayId.Monday] = DayOfWeek.Monday,
            [Constants.DayMaster.DayId.Tuesday] = DayOfWeek.Tuesday,
            [Constants.DayMaster.DayId.Wednesday] = DayOfWeek.Wednesday,
            [Constants.DayMaster.DayId.Thursday] = DayOfWeek.Thursday,
            [Constants.DayMaster.DayId.Friday] = DayOfWeek.Friday,
            [Constants.DayMaster.DayId.Saturday] = DayOfWeek.Saturday,
            [Constants.DayMaster.DayId.Sunday] = DayOfWeek.Sunday
        };

    /// <summary>
    /// The day a <c>DayMaster.DayID</c> names, or null when the id is absent or
    /// is not one of the seven.
    /// <para>
    /// Null rather than an exception: no foreign key ties these columns to
    /// <c>dbo.DayMaster</c>, so a row can hold an id that is not in the lookup,
    /// and refusing to log time because of it would punish the employee for a
    /// setup someone else got wrong. Callers treat null as "no constraint
    /// expressed".
    /// </para>
    /// </summary>
    internal static DayOfWeek? Parse(int? dayId)
    {
        if (dayId is not { } id)
        {
            return null;
        }

        return Days.TryGetValue(id, out var day) ? day : null;
    }

    /// <summary>
    /// The name of a <c>DayMaster.DayID</c> - "Monday" ... "Sunday" - or the
    /// number itself when it is not one of the seven, so that a message about a
    /// bad value can still show what the value was.
    /// </summary>
    internal static string Describe(int? dayId) =>
        Parse(dayId) is { } day ? day.ToString() : dayId?.ToString() ?? "not set";

    /// <summary>
    /// Every day from <paramref name="start"/> to <paramref name="end"/>
    /// inclusive, walking forward through the week and wrapping past Saturday.
    /// <para>
    /// The wrap is the point: a week configured Sunday to Thursday runs Sunday,
    /// Monday, Tuesday, Wednesday, Thursday, and a naive numeric comparison
    /// would call that an empty range. A start equal to the end is a single-day
    /// week, not a seven-day one.
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
