namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One block of time a user logged against one project, mapped onto
/// <c>dbo.TimesheetProjectTimeLog</c>. This is a persistence/domain model and
/// is never returned from a controller.
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ (an
/// <c>int</c> <c>IsDelete</c>, no updated-by convention) and the table is
/// written only by this module.
/// </para>
/// </summary>
public class TimesheetProjectTimeLog
{
    /// <summary>The <c>TimesheetProjectTimeLogID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetProjectTimeLogId { get; set; }

    /// <summary>A <c>dbo.Signup.UserID</c>. No foreign key protects it.</summary>
    public int UserId { get; set; }

    /// <summary>A <c>dbo.TimesheetProject.ProjectID</c>. No foreign key protects it.</summary>
    public int ProjectId { get; set; }

    /// <summary>
    /// The sheet this entry belongs to - <c>NOT NULL</c>, so an entry carries
    /// it from the moment it is created, before anything is submitted. The
    /// only link to <c>dbo.TimesheetProjectSubmission</c>; neither table holds
    /// the other's key.
    /// </summary>
    public string SheetSubmissionCode { get; set; } = string.Empty;

    /// <summary>The day the work was done. A <c>date</c> column - one day, not a range.</summary>
    public DateTime LogDate { get; set; }

    /// <summary>Optional. Present together with <see cref="EndTime"/>, or not at all.</summary>
    public TimeSpan? StartTime { get; set; }

    /// <summary>Optional. Present together with <see cref="StartTime"/>, or not at all.</summary>
    public TimeSpan? EndTime { get; set; }

    /// <summary>
    /// How long the block ran. The figure every total is built from, so an
    /// entry that crosses midnight is counted once and in full.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>What was worked on. There is no task, so this is the only record of it.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The <c>Status</c> column - <c>1</c> = Save, <c>2</c> = Draft, as
    /// <c>Constants.TimeLog.Status</c>. <c>NOT NULL</c> with no default: every
    /// insert sets it.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Soft-delete marker - an <c>int</c>, as on
    /// <c>dbo.Timesheet_Users_Project_setup</c>. <c>1</c> is deleted, <c>0</c>
    /// is live. <c>NOT NULL</c> with no default: every insert sets it to 0.
    /// </summary>
    public int IsDelete { get; set; }

    public DateTime CreateDate { get; set; }

    public int CreatedBy { get; set; }

    public DateTime? UpdateDate { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime? DeletedDate { get; set; }

    public int? DeletedBy { get; set; }
}
