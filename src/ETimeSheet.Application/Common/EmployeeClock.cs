namespace ETimeSheet.Application.Common;

/// <summary>
/// The wall clock an employee is actually looking at - "now" in the time zone
/// their timesheet setup names, rather than "now" on the server.
/// <para>
/// <b>Every rule about a time of day has to be judged on this clock, not on
/// UTC.</b> A cut-off of 19:00 means seven in the evening where the employee is
/// sitting: for a team in Kolkata that moment is 13:30 UTC, and for one in
/// Los Angeles it is 02:00 the following day. Comparing either against the
/// server's UTC clock locks one team out five and a half hours early and gives
/// the other until the middle of the next morning.
/// </para>
/// <para>
/// It carries <see cref="ZoneName"/> beside the time because an employee told
/// "the 19:00 cut-off has passed" at what their watch says is 13:45 needs to
/// know which 19:00 was meant. The two travel together so a message cannot
/// quote one clock and name another.
/// </para>
/// </summary>
/// <param name="Now">
/// The current local time. <see cref="DateTimeKind.Unspecified"/>, as
/// <see cref="TimeZoneInfo.ConvertTimeFromUtc"/> returns - it is a wall-clock
/// reading in a known zone, and tagging it Local would claim it belongs to the
/// server's zone, which is exactly the confusion this type exists to remove.
/// </param>
/// <param name="ZoneName">
/// The zone the reading is in - an IANA id such as <c>"Asia/Kolkata"</c>, or
/// <c>"UTC"</c> when the setup named no zone or named one this system does not
/// recognise.
/// </param>
internal readonly record struct EmployeeClock(DateTime Now, string ZoneName)
{
    /// <summary>The name used when no usable zone could be had.</summary>
    internal const string FallbackZoneName = "UTC";

    /// <summary>The employee's local date - their "today".</summary>
    internal DateTime Today => Now.Date;

    /// <summary>The employee's local time of day, for comparing against a <c>time(7)</c> column.</summary>
    internal TimeSpan TimeOfDay => Now.TimeOfDay;

    /// <summary>
    /// Looks up a zone by its id, or returns <see langword="null"/> when the id
    /// is absent or names nothing this system knows.
    /// <para>
    /// Null for an unrecognised id rather than an exception: the id comes from a
    /// column an administrator fills in, and an employee should not be unable to
    /// log time because somebody mistyped a zone. The caller decides what to do
    /// about it - <c>TimeLogService</c> logs it and falls back to UTC.
    /// </para>
    /// <para>
    /// IANA ids work on every platform from .NET 6 onwards, Windows included,
    /// so <c>"Asia/Kolkata"</c> does not have to be translated first.
    /// </para>
    /// </summary>
    internal static TimeZoneInfo? FindZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId!.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            // The id names no zone on this machine.
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            // The id names a zone whose rules are corrupt - rare, and just as
            // unusable as one that does not exist.
            return null;
        }
    }

    /// <summary>
    /// Reads <paramref name="utcNow"/> on the clock of <paramref name="zone"/>.
    /// <para>
    /// A null zone falls back to UTC unchanged, which is what this code did for
    /// every user before time zones existed. It is a fallback and not a default:
    /// it keeps an employee working when their setup is incomplete, and the
    /// caller is expected to say so in the log.
    /// </para>
    /// </summary>
    internal static EmployeeClock At(DateTime utcNow, TimeZoneInfo? zone)
    {
        // SpecifyKind before converting, not for tidiness: ConvertTimeFromUtc
        // throws on a DateTime marked Local, and the clock behind this is an
        // interface anyone can implement.
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        return zone is null
            ? new EmployeeClock(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified), FallbackZoneName)
            : new EmployeeClock(TimeZoneInfo.ConvertTimeFromUtc(utc, zone), zone.Id);
    }
}
