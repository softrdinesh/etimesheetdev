namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A leave type assigned to one user, with the balance left, mapped onto the
/// existing <c>dbo.Timesheet_Leave_assignment_user</c> table. This is a
/// persistence/domain model and is never returned from a controller.
/// <para>
/// Mapped for the soft delete and the one-assignment-per-user-and-leave-type
/// check only: the list and the add/edit go through
/// <c>spc_GetTimesheetUserLeaveAssignment</c> and
/// <c>spc_AddEditTimesheetUserLeaveAssignment</c>.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>CreatedDate</c>, and a nullable <c>bit</c> <c>IsDelete</c> rather
/// than the base class's <c>int IsDeleted</c>).
/// </para>
/// </summary>
public class TimesheetLeaveAssignmentUser
{
    /// <summary>The <c>TimesheetLeaveAssignmentUserID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetLeaveAssignmentUserId { get; set; }

    /// <summary>The <c>UserID</c> column - the user's <c>dbo.Signup.UserID</c>.</summary>
    public int? UserId { get; set; }

    /// <summary>The <c>LeavetypeID</c> column - a <c>dbo.Timesheet_Leavetype_Setup.LeaveTypeID</c>.</summary>
    public int? LeaveTypeId { get; set; }

    /// <summary>Soft-delete flag. Nullable: only <see langword="true"/> means deleted.</summary>
    public bool? IsDelete { get; set; }

    public DateTime? CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime? DeletedDate { get; set; }

    public int? DeletedBy { get; set; }

    /// <summary>The <c>BalanceLeave</c> column - <c>decimal(4,2)</c>.</summary>
    public decimal? BalanceLeave { get; set; }
}
