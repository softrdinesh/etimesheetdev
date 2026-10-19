namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One status change on a project sheet, mapped onto
/// <c>dbo.TimesheetProjectSubmissionAuditLog</c>. This is a
/// persistence/domain model and is never returned from a controller.
/// <para>
/// Append-only: a row is written at submit and at every approve or reject,
/// and nothing ever updates or deletes one. That is why the table has no
/// soft-delete or updated-by columns.
/// </para>
/// </summary>
public class TimesheetProjectSubmissionAuditLog
{
    /// <summary><c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetProjectSubmissionAuditLogId { get; set; }

    /// <summary>
    /// The sheet's <b>IDENTITY</b>, not its code - so the trail still points
    /// at exactly one row even if two sheets ever share a code.
    /// </summary>
    public int TimesheetProjectSubmissionId { get; set; }

    /// <summary>
    /// The status the sheet moved <b>to</b> - <c>1</c> Submitted, <c>2</c>
    /// Approved, <c>3</c> Rejected.
    /// </summary>
    public int StatusId { get; set; }

    /// <summary>
    /// Why. A rejection's reason, kept here after the sheet itself is approved
    /// or resubmitted and <c>RejectionReason</c> is cleared.
    /// </summary>
    public string? Remark { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreateDate { get; set; }
}
