using ETimeSheet.Shared.Enums;

namespace ETimeSheet.Application.DTOs.TimeLogs;

/// <summary>
/// One row of <c>dbo.TimeLog</c> as the API returns it - the entry exactly as it
/// was stored, so a caller that has just logged time can show it back without a
/// second request.
/// <para>
/// Distinct from <see cref="TimeLoggedDetailResponse"/>, which is the shape
/// <c>spc_GetTimeLoggedDetailsForTask</c> returns. They overlap but are not the
/// same thing, and tying the write response to the procedure's shape would make
/// the procedure impossible to change.
/// </para>
/// </summary>
public class TimeLogResponse
{
    /// <summary>The generated key. This is the one field the caller could not have known before the call.</summary>
    public int SheetId { get; set; }

    public string? SheetCode { get; set; }

    public int? TaskId { get; set; }

    public string? Description { get; set; }

    public int? UserId { get; set; }

    public DateTime? StartDate { get; set; }

    public TimeSpan? StartTime { get; set; }

    public DateTime? EndDate { get; set; }

    public TimeSpan? EndTime { get; set; }

    /// <summary>1 = Save, 2 = Draft.</summary>
    public TimeLogStatus? Status { get; set; }

    /// <summary>The status spelled out, so a client does not have to carry the numbers.</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>
    /// How long the entry covers, in hours to two places. Computed from both
    /// ends including their dates, so an overnight entry measures correctly
    /// rather than coming out negative.
    /// </summary>
    public decimal TotalWorkingHours { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CreateDate { get; set; }
}
