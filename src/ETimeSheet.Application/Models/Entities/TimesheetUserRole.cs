namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A role defined for an organisation, mapped onto the existing
/// <c>dbo.TimesheetUserRole</c> table. This is a persistence/domain model and is
/// never returned from a controller.
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>Createdby</c>, <c>Deleteddate</c>, and a nullable <c>bit</c>
/// <c>IsDelete</c> rather than the base class's <c>int IsDeleted</c>).
/// </para>
/// </summary>
public class TimesheetUserRole
{
    /// <summary>The <c>RoleID</c> column - <c>IDENTITY(1,1)</c>.</summary>
    public int RoleId { get; set; }

    /// <summary>The <c>Rolename</c> column. <c>nvarchar(50)</c>, nullable.</summary>
    public string? RoleName { get; set; }

    public int? OrganizationId { get; set; }

    public DateTime? CreateDate { get; set; }

    /// <summary>The <c>Createdby</c> column.</summary>
    public int? CreatedBy { get; set; }

    /// <summary>Soft-delete flag. Nullable: only <see langword="true"/> means deleted.</summary>
    public bool? IsDelete { get; set; }

    /// <summary>The <c>Deleteddate</c> column.</summary>
    public DateTime? DeletedDate { get; set; }

    /// <summary>The <c>Deletedby</c> column.</summary>
    public int? DeletedBy { get; set; }
}
