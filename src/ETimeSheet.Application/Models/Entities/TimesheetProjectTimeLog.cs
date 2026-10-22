namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A user's time on one project for one calendar day, mapped onto the
/// <c>dbo.TimesheetProjectTimeLog</c> table. This is a persistence/domain
/// model and is never returned from a controller.
/// <para>
/// One live row per <see cref="UserId"/>, <see cref="ProjectId"/> and
/// <see cref="LogDate"/>: logging more time on the same day adds to
/// <see cref="HoursLogged"/> rather than adding a row. Nothing in the database
/// enforces that - <c>TimesheetProjectService</c> does.
/// </para>
/// </summary>
public class TimesheetProjectTimeLog
{
    /// <summary>The <c>TimesheetProjectTimeLogID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetProjectTimeLogId { get; set; }

    /// <summary>The <c>UserID</c> column - <c>dbo.Signup.UserID</c>. No foreign key.</summary>
    public int UserId { get; set; }

    /// <summary>The <c>ProjectID</c> column - <c>dbo.TimesheetProject.ProjectID</c>. No foreign key.</summary>
    public int ProjectId { get; set; }

    /// <summary>The <c>OrganizationID</c> column - the project's organisation. No foreign key.</summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// <c>nvarchar(20)</c>, NOT NULL. The <c>PSHT</c> code of the submission
    /// period the day is in, shared by every day of this user and project in
    /// that period. No <c>dbo.TimesheetProjectSubmission</c> row backs it.
    /// </summary>
    public string SheetSubmissionCode { get; set; } = string.Empty;

    /// <summary>The <c>LogDate</c> column - a <c>date</c>, no time part.</summary>
    public DateTime LogDate { get; set; }

    /// <summary>The <c>HoursLogged</c> column - whole hours, the day's total on the project.</summary>
    public int HoursLogged { get; set; }

    /// <summary><c>nvarchar(max)</c>.</summary>
    public string? Description { get; set; }

    /// <summary>Soft-delete flag. 1 = deleted, 0 = live. NOT NULL.</summary>
    public int IsDelete { get; set; }

    public DateTime CreateDate { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? UpdateDate { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime? DeletedDate { get; set; }

    public int? DeletedBy { get; set; }
}
