namespace ETimeSheet.Application.DTOs.TimeLogs;

/// <summary>
/// Totals for the requested period. <b>All three are in hours</b>, decimal, so
/// seven and a half hours is <c>7.5</c> rather than <c>07:30:00</c>.
/// </summary>
public class TimeLoggedSummaryResponse
{
    /// <summary>Sum of the returned entries' durations.</summary>
    public decimal TotalWorkInHours { get; init; }

    /// <summary>
    /// What the user was expected to log over the period, from their
    /// <c>TimesheetMasterSetup</c> row. Zero when they have no setup.
    /// </summary>
    public decimal TotalExpected { get; init; }

    /// <summary>
    /// <see cref="TotalExpected"/> minus <see cref="TotalWorkInHours"/>.
    /// Negative when the user logged more than expected, which is information
    /// rather than an error, so it is not clamped to zero.
    /// </summary>
    public decimal TotalRemaining { get; init; }
}
