using ETimeSheet.Shared.Enums;

namespace ETimeSheet.Application.Models.Results;

/// <summary>
/// One row of the result set returned by <c>dbo.spc_GetTimeLoggedDetailsForTask</c>.
/// <para>
/// This is a keyless type: it is not a table, it has no identity and it is never
/// tracked or written. It exists solely to give the procedure's SELECT list a
/// shape EF Core can materialise, which is why it carries exactly the eight
/// columns the procedure returns - no more.
/// </para>
/// </summary>
public class TimeLoggedDetail
{
    public int SheetId { get; set; }

    public string? SheetCode { get; set; }

    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }

    public TimeSpan? StartTime { get; set; }

    public DateTime? EndDate { get; set; }

    public TimeSpan? EndTime { get; set; }

    /// <summary>
    /// The <c>Status</c> column as returned by the procedure.
    /// <para><b>1 = Save, 2 = Draft only</b> - see
    /// <see cref="ETimeSheet.Shared.Utilities.Constants.TimeLog.Status"/>.</para>
    /// </summary>
    public TimeLogStatus? Status { get; set; }

    /// <summary>
    /// Duration of this entry in hours, computed by the procedure and already
    /// rounded to two decimal places. <c>DATEDIFF(...)/60.0</c> yields a SQL
    /// <c>numeric</c>, hence <c>decimal</c> rather than <c>double</c>.
    /// </summary>
    public decimal? TotalWorkingHours { get; set; }

    /// <summary>
    /// Duration of this entry in whole minutes, computed by the procedure.
    /// <c>DATEDIFF</c> returns an <c>int</c>, and this is the exact value the
    /// summary totals are built from.
    /// </summary>
    public int? TotalWorkingMinutes { get; set; }
}
