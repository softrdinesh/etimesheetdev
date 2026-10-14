namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A project status defined for an organisation, mapped onto the existing
/// <c>dbo.TimesheetProjectStatus</c> table. This is a persistence/domain model
/// and is never returned from a controller.
/// <para>
/// Mapped for the soft delete only: the list and the add/edit go through
/// <c>spc_GetTimesheetProjectStatus</c> and
/// <c>spc_AddEditTimesheetProjectStatus</c>.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>CreateDate</c>, <c>Deletedby</c>, <c>Updatedby</c>), and there is no
/// delete flag - a row is deleted when <c>DeletedDate</c> is set.
/// </para>
/// </summary>
public class TimesheetProjectStatus
{
    /// <summary>The <c>StatusID</c> column - <c>IDENTITY(1,1)</c>.</summary>
    public int StatusId { get; set; }

    /// <summary><c>nvarchar(20)</c>, not null.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The <c>Colorcode</c> column. <c>nvarchar(25)</c>, nullable.</summary>
    public string? ColorCode { get; set; }

    public DateTime? CreateDate { get; set; }

    public int? CreatedBy { get; set; }

    /// <summary>Set when the status is soft-deleted; null while it is live.</summary>
    public DateTime? DeletedDate { get; set; }

    /// <summary>The <c>Deletedby</c> column.</summary>
    public int? DeletedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    /// <summary>The <c>Updatedby</c> column.</summary>
    public int? UpdatedBy { get; set; }

    /// <summary>The <c>OrgID</c> column. Nullable on the table.</summary>
    public int? OrganizationId { get; set; }
}
