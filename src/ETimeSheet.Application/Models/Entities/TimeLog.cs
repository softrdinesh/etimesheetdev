using ETimeSheet.Application.Models.Common;
using ETimeSheet.Shared.Enums;

namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A single block of recorded work, mapped onto the existing <c>TimeLog</c>
/// table. This is a persistence/domain model and is never returned from a
/// controller - see the DTOs under <c>ETimeSheet.Application.DTOs.TimeLogs</c>.
/// <para>
/// Almost every column is nullable in the database, and that is reproduced
/// faithfully here rather than papered over: pretending a nullable column is
/// required would throw at materialisation time on perfectly valid existing rows.
/// </para>
/// </summary>
public class TimeLog : AuditableEntity
{
    /// <summary>Primary key. Maps to the <c>SheetID</c> column.</summary>
    public int SheetId { get; set; }

    /// <summary>Human-readable reference. <c>varchar(15)</c>, so non-Unicode.</summary>
    public string? SheetCode { get; set; }

    public int? TaskId { get; set; }

    /// <summary><c>nvarchar(max)</c>.</summary>
    public string? Description { get; set; }

    /// <summary>Clock time of day, not an instant. The column is <c>time(7)</c>.</summary>
    public TimeSpan? StartTime { get; set; }

    public TimeSpan? EndTime { get; set; }

    /// <summary>Calendar day the work starts on. The column is <c>date</c>, so the time part is always midnight.</summary>
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>Owner of the entry. Maps to the <c>UserID</c> column.</summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Stored as a plain <c>int</c>, and nullable, exactly as the column is.
    /// <para><b>1 = Save, 2 = Draft only</b> - see
    /// <see cref="ETimeSheet.Shared.Utilities.Constants.TimeLog.Status"/>.</para>
    /// </summary>
    public TimeLogStatus? Status { get; set; }

    /// <summary>
    /// Whether <see cref="TaskId"/> is a project task (<c>dbo.TaskMaster</c>)
    /// rather than a sprint task (<c>dbo.SprintTaskManagement</c>). The column
    /// is a nullable <c>bit</c>, added 2026-09-24; rows written before then
    /// hold null.
    /// </summary>
    public bool? IsProjectTask { get; set; }
}
