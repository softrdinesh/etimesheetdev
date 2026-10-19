namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One status change on a project sheet - who moved it to which status, and
/// when - mapped onto the <c>dbo.TimesheetProjectSubmissionAuditLog</c> table.
/// Written on every submit, approve and reject; never updated or deleted.
/// This is a persistence/domain model and is never returned from a controller.
/// </summary>
public class TimesheetProjectSubmissionAuditLog
{
    /// <summary>The <c>TimesheetProjectSubmissionAuditLogID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetProjectSubmissionAuditLogId { get; set; }

    /// <summary>The sheet that changed. No foreign key.</summary>
    public int TimesheetProjectSubmissionId { get; set; }

    /// <summary>The status the sheet moved to - see <c>Constants.TimesheetProjectSubmission.Status</c>.</summary>
    public int StatusId { get; set; }

    public int CreatedBy { get; set; }

    public DateTime CreateDate { get; set; }
}
