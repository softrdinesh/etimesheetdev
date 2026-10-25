namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One leave application of a user, mapped onto the existing
/// <c>dbo.Timesheet_Leave_User_apply</c> table. This is a persistence/domain
/// model and is never returned from a controller.
/// <para>
/// Mapped for the approval check on edit and for the remove, which deletes
/// the row and may give its days back to the user's balance: the list and the
/// add/edit go through <c>spc_GetTimesheetUserLeaveApply</c> and
/// <c>spc_AddEditTimesheetUserLeaveApply</c>.
/// </para>
/// <para>
/// No soft delete: the table has no <c>IsDelete</c> column, so a removed
/// application is physically deleted.
/// </para>
/// </summary>
public class TimesheetLeaveUserApply
{
    /// <summary>The <c>TimesheetLeaveUserApplyID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetLeaveUserApplyId { get; set; }

    /// <summary>The <c>LeaveID</c> column - the running leave number, assigned by the add procedure.</summary>
    public int LeaveId { get; set; }

    public int? UserId { get; set; }

    /// <summary>The <c>OrgID</c> column - the organisation the application belongs to.</summary>
    public int? OrganizationId { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>The <c>NoofDays</c> column - <c>decimal(4,2)</c>.</summary>
    public decimal? NoOfDays { get; set; }

    /// <summary>The <c>LeavetypeID</c> column.</summary>
    public int? LeaveTypeId { get; set; }

    public DateTime? CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public int? ApprovedBy { get; set; }

    public DateTime? RejectedDate { get; set; }

    public int? RejectedBy { get; set; }

    public string? UserComments { get; set; }

    public string? ApproverComments { get; set; }

    /// <summary>The <c>LeaveStatus</c> column - a <c>dbo.TimesheetLeaveStatus_Lookup.Value</c>.</summary>
    public int? LeaveStatus { get; set; }
}
