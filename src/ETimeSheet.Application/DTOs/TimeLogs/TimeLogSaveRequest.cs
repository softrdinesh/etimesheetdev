using ETimeSheet.Shared.Enums;

namespace ETimeSheet.Application.DTOs.TimeLogs;

/// <summary>
/// Body of the "log my time" request - one block of work an employee recorded
/// against one task.
/// <para>
/// This is an <b>insert only</b>. There is no sheet id, because the caller is
/// logging new time rather than correcting an entry; editing an existing one is
/// a separate operation and is not built yet.
/// </para>
/// <para>
/// <c>UserId</c> is supplied by the caller and would normally come from the
/// authenticated principal. Authentication is switched off for this project for
/// now, so it travels in the payload - deliberately temporary, exactly as on the
/// read endpoints.
/// </para>
/// </summary>
public class TimeLogSaveRequest
{
    /// <summary>The employee the time belongs to. Required - it is also what selects the timesheet setup the entry is validated against.</summary>
    public int UserId { get; set; }

    /// <summary>The task the work was done on. Required.</summary>
    public int TaskId { get; set; }

    /// <summary>What was worked on. Optional, and unbounded - the column is <c>nvarchar(max)</c>.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional caller-supplied reference, stored in <c>varchar(15)</c>. Nothing
    /// generates one: left out, the column stays null, exactly as it is on the
    /// rows already in the table.
    /// </summary>
    public string? SheetCode { get; set; }

    /// <summary>
    /// The day the work started. A calendar date - the column is <c>date</c>, so
    /// any time part is ignored.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// The day the work ended. Optional: left out it is the same day as
    /// <see cref="StartDate"/>, which is the ordinary case. It exists for a
    /// shift that runs past midnight, which is the only reason the table carries
    /// two dates.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Clock time the work started, for example <c>"09:00:00"</c>. Required.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>Clock time the work ended. Required, and must be after the start once both dates are taken into account.</summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// <b>1 = Save, 2 = Draft.</b> Required - an entry with no status is neither
    /// saved nor drafted, and the reads would not know what to do with it.
    /// </summary>
    public TimeLogStatus Status { get; set; }

    /// <summary>
    /// The user recording the entry. Usually the same person as
    /// <see cref="UserId"/>, but not necessarily - a manager may log on someone's
    /// behalf, and the row should say who actually did it.
    /// <para>
    /// <b>Temporary</b>, as on <c>AdminSaveRequest.CreatedBy</c>: this
    /// belongs in the token, and it moves there the moment JWT is switched back
    /// on.
    /// </para>
    /// </summary>
    public int CreatedBy { get; set; }
}
