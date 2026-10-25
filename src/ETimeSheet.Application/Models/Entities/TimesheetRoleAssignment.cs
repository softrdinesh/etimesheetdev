namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// The modules one user may use, mapped onto the existing
/// <c>dbo.Timesheet_role_assignment</c> table. This is a persistence/domain
/// model and is never returned from a controller.
/// <para>
/// Mapped for the one-assignment-per-user check and the delete only: the list
/// and the add/edit go through <c>spc_GetUserAccessSetup</c> and
/// <c>spc_AddEditUserAccessSetup</c>.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>CreateDate</c>, <c>Updatedby</c>), and there is no soft-delete column -
/// a removed assignment is physically deleted.
/// </para>
/// </summary>
public class TimesheetRoleAssignment
{
    /// <summary>The <c>AssignmentID</c> column - <c>IDENTITY(1,1)</c>.</summary>
    public int AssignmentId { get; set; }

    public int? UserId { get; set; }

    /// <summary>The <c>Mod_IsclientSetup</c> column.</summary>
    public bool? IsClientSetup { get; set; }

    /// <summary>The <c>Mod_Isproject</c> column.</summary>
    public bool? IsProject { get; set; }

    /// <summary>The <c>Mod_IsProjectbudget</c> column.</summary>
    public bool? IsProjectBudget { get; set; }

    /// <summary>The <c>Mod_IsProjectCategory</c> column.</summary>
    public bool? IsProjectCategory { get; set; }

    /// <summary>The <c>Mod_IsStatusSetup</c> column.</summary>
    public bool? IsStatusSetup { get; set; }

    /// <summary>The <c>Mod_IsRoleSetup</c> column.</summary>
    public bool? IsRoleSetup { get; set; }

    /// <summary>The <c>Mod_IsUsersetup</c> column.</summary>
    public bool? IsUserSetup { get; set; }

    /// <summary>The <c>Mod_IsTimeLog</c> column.</summary>
    public bool? IsTimeLog { get; set; }

    /// <summary>The <c>Mod_IsViewTimesheet</c> column.</summary>
    public bool? IsViewTimesheet { get; set; }

    /// <summary>The <c>Mod_IsUserLeaveApply</c> column.</summary>
    public bool? IsUserLeaveApply { get; set; }

    /// <summary>The <c>Mod_IsLeaveSetup</c> column.</summary>
    public bool? IsLeaveSetup { get; set; }

    /// <summary>The <c>Mod_IsLeaveAssignmentUser</c> column.</summary>
    public bool? IsLeaveAssignmentUser { get; set; }

    /// <summary>The <c>Mod_IsLeaveApproveReject</c> column.</summary>
    public bool? IsLeaveApproveReject { get; set; }

    /// <summary>The <c>Mod_IsReportView</c> column.</summary>
    public bool? IsReportView { get; set; }

    public DateTime? CreateDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    /// <summary>The <c>Updatedby</c> column.</summary>
    public int? UpdatedBy { get; set; }
}
