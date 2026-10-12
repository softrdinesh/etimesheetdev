namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A project category defined for an organisation, mapped onto the existing
/// <c>dbo.TimesheetProjectCategory</c> table. This is a persistence/domain
/// model and is never returned from a controller.
/// <para>
/// Mapped for the soft delete only: the list and the add/edit go through
/// <c>spc_GetTimesheetProjectCategory</c> and
/// <c>spc_AddEditTimesheetProjectCategory</c>.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>CreateDate</c>, <c>Deletedby</c>, and a nullable <c>bit</c>
/// <c>IsDelete</c> rather than the base class's <c>int IsDeleted</c>).
/// </para>
/// </summary>
public class TimesheetProjectCategory
{
    /// <summary>The <c>ID</c> column - <c>IDENTITY(1,1)</c>.</summary>
    public int CategoryId { get; set; }

    /// <summary>The <c>Categoryname</c> column. <c>nvarchar(50)</c>, nullable.</summary>
    public string? CategoryName { get; set; }

    public int OrganizationId { get; set; }

    /// <summary>Soft-delete flag. Nullable: only <see langword="true"/> means deleted.</summary>
    public bool? IsDelete { get; set; }

    public DateTime? CreateDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? DeletedDate { get; set; }

    /// <summary>The <c>Deletedby</c> column.</summary>
    public int? DeletedBy { get; set; }
}
