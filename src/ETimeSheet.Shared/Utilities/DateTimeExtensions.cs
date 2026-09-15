namespace ETimeSheet.Shared.Utilities;

/// <summary>
/// Small, well-defined date helpers. Everything the application persists is UTC.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Strips the time component and marks the result as UTC. Used to normalise
    /// the date filters of a query so that a time-of-day in the request cannot
    /// silently exclude entries on the boundary days.
    /// </summary>
    public static DateTime ToUtcDate(this DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
}
