namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A holiday of an organisation, optionally narrowed to a country, client or
/// project, mapped onto the existing <c>dbo.TimesheetHolidaysetup</c> table.
/// This is a persistence/domain model and is never returned from a controller.
/// <para>
/// Mapped for the soft delete only: the list and the add/edit go through
/// <c>spc_GetTimesheetHolidaySetup</c> and
/// <c>spc_AddEditTimesheetHolidaySetup</c>.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>CreateDate</c>, a nullable <c>bit</c> <c>IsDelete</c>, and no
/// update or delete stamps).
/// </para>
/// </summary>
public class TimesheetHolidaySetup
{
    /// <summary>The <c>TimesheetHolidaysetupID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetHolidaySetupId { get; set; }

    /// <summary><c>date</c>, nullable.</summary>
    public DateTime? HolidayDate { get; set; }

    /// <summary><c>nvarchar(100)</c>, nullable - the holiday's name.</summary>
    public string? Description { get; set; }

    public int? OrganizationId { get; set; }

    public int? CountryId { get; set; }

    public int? ClientId { get; set; }

    public int? ProjectId { get; set; }

    public DateTime? CreateDate { get; set; }

    public int? CreatedBy { get; set; }

    /// <summary>Soft-delete flag. Nullable: only <see langword="true"/> means deleted.</summary>
    public bool? IsDelete { get; set; }
}
