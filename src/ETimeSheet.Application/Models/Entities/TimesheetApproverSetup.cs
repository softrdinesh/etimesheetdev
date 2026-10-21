namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// The timesheet approver of one project, mapped onto the
/// existing <c>dbo.Timesheet_Approver_setup</c> table. This is a
/// persistence/domain model and is never returned from a controller.
/// <para>
/// Mapped for the soft delete and the one-approver-per-project check
/// only: the list and the add/edit go through
/// <c>spc_GetTimesheetApproverSetup</c> and
/// <c>spc_AddEditTimesheetApproverSetup</c>.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>CreateDate</c>, no updated columns, and a nullable <c>bit</c>
/// <c>IsDelete</c> rather than the base class's <c>int IsDeleted</c>).
/// </para>
/// </summary>
public class TimesheetApproverSetup
{
    /// <summary>The <c>TimesheetApproverSetupID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetApproverSetupId { get; set; }

    /// <summary>The <c>ApproverID</c> column - the approving user's <c>dbo.Signup.UserID</c>.</summary>
    public int? ApproverId { get; set; }

    public int? ProjectId { get; set; }

    public DateTime? CreateDate { get; set; }

    public int? CreatedBy { get; set; }

    /// <summary>Soft-delete flag. Nullable: only <see langword="true"/> means deleted.</summary>
    public bool? IsDelete { get; set; }

    public DateTime? DeletedDate { get; set; }

    public int? DeletedBy { get; set; }

    public bool? IsTimesheetApprover { get; set; }

    /// <summary>The <c>OrgID</c> column.</summary>
    public int? OrganizationId { get; set; }
}
