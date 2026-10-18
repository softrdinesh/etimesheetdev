namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A user's assignment to a project, mapped onto the existing
/// <c>dbo.Timesheet_Users_Project_setup</c> table. This is a
/// persistence/domain model and is never returned from a controller.
/// <para>
/// Added by <c>spc_Signup_timesheet_user</c>; mapped for the edit and the
/// soft delete.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>Createdby</c>, an <c>int</c> <c>IsDelete</c>), and there are no
/// updated-by columns.
/// </para>
/// </summary>
public class TimesheetUserProjectSetup
{
    /// <summary>The <c>TimesheetUserProjectSetupID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetUserProjectSetupId { get; set; }

    /// <summary>A <c>dbo.Signup.UserID</c>. Not null.</summary>
    public int UserId { get; set; }

    /// <summary>Presumably a <c>dbo.TimesheetUserRole.RoleID</c>.</summary>
    public int? ProjectRoleId { get; set; }

    /// <summary>The <c>Cost_rate_per_hour</c> column. <c>decimal(18,2)</c>.</summary>
    public decimal? CostRate { get; set; }

    /// <summary>The <c>Billable_rate_per_hour</c> column. <c>decimal(18,2)</c>.</summary>
    public decimal? BillRate { get; set; }

    public DateTime? CreateDate { get; set; }

    /// <summary>The <c>Createdby</c> column.</summary>
    public int? CreatedBy { get; set; }

    /// <summary>
    /// Soft-delete marker - an <c>int</c>, nullable. <c>1</c> is deleted;
    /// <c>0</c> or NULL is live.
    /// </summary>
    public int? IsDelete { get; set; }

    public DateTime? DeletedDate { get; set; }

    public int? DeletedBy { get; set; }

    /// <summary>The <c>Jobtype</c> column.</summary>
    public int? JobType { get; set; }

    /// <summary>The <c>WorklocationRange</c> column.</summary>
    public int? WorkLocationRange { get; set; }

    /// <summary>Presumably a <c>dbo.TimesheetProject.ProjectID</c>.</summary>
    public int? ProjectId { get; set; }

    /// <summary>The <c>ProjectlocLatitude</c> column. <c>nvarchar(25)</c> - text.</summary>
    public string? ProjectLocationLatitude { get; set; }

    /// <summary>The <c>ProjectlocLongtitude</c> column - the database's spelling. <c>nvarchar(25)</c> - text.</summary>
    public string? ProjectLocationLongitude { get; set; }

    /// <summary>
    /// The <c>Max_log_hour_day</c> column - the most hours a day this
    /// assignment may log. Null means this assignment sets no cap.
    /// <para>
    /// Stored and returned only. The daily cap the time log enforces is
    /// <c>dbo.TimesheetMasterSetup.MaxTimeInHrs</c>, per user; nothing
    /// reconciles the two.
    /// </para>
    /// </summary>
    public int? MaxLogHoursPerDay { get; set; }
}
