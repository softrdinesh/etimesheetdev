namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One time entry on a project timesheet, mapped onto the
/// <c>dbo.TimesheetProjectTimeLog</c> table. This is a persistence/domain
/// model and is never returned from a controller.
/// <para>
/// The table has no <c>UserID</c> or <c>ProjectID</c>: both are the
/// <see cref="TimesheetProjectSubmission"/>'s the entry belongs to.
/// </para>
/// </summary>
public class TimesheetProjectTimeLog
{
    /// <summary>The <c>TimesheetProjectTimeLogID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetProjectTimeLogId { get; set; }

    /// <summary>The sheet the entry belongs to. No foreign key.</summary>
    public int TimesheetProjectSubmissionId { get; set; }

    /// <summary>That sheet's <c>SheetSubmissionCode</c>, repeated. <c>nvarchar(20)</c>, NOT NULL.</summary>
    public string SheetSubmissionCode { get; set; } = string.Empty;

    /// <summary>The <c>LogDate</c> column - a <c>date</c>, no time part.</summary>
    public DateTime LogDate { get; set; }

    public TimeSpan? StartTime { get; set; }

    public TimeSpan? EndTime { get; set; }

    public int DurationMinutes { get; set; }

    /// <summary><c>nvarchar(max)</c>.</summary>
    public string? Description { get; set; }

    /// <summary>See <c>Constants.TimesheetProjectTimeLog.Status</c>.</summary>
    public int Status { get; set; }

    /// <summary>Soft-delete flag. 1 = deleted, 0 = live. NOT NULL.</summary>
    public int IsDelete { get; set; }

    public DateTime CreateDate { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? UpdateDate { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime? DeletedDate { get; set; }

    public int? DeletedBy { get; set; }
}
