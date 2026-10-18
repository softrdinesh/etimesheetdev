namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// The weekend days of one project, mapped onto the existing
/// <c>dbo.TimeSheet_Weekend_setup</c> table. This is a persistence/domain model
/// and is never returned from a controller.
/// <para>
/// Mapped for the soft delete and the one-setup-per-project check only: the
/// list and the add/edit go through <c>spc_GetTimesheetWeekendSetup</c> and
/// <c>spc_AddEditTimesheetWeekendSetup</c>.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>CreateDate</c>, and a nullable <c>bit</c> <c>IsDelete</c> rather than
/// the base class's <c>int IsDeleted</c>).
/// </para>
/// </summary>
public class TimesheetWeekendSetup
{
    /// <summary>The <c>TimeSheetWeekendSetupID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimeSheetWeekendSetupId { get; set; }

    /// <summary>The <c>OrgID</c> column. Not null.</summary>
    public int OrganizationId { get; set; }

    public int? ClientId { get; set; }

    public int? ProjectId { get; set; }

    public bool? IsMondayWeekend { get; set; }

    public bool? IsTuesdayWeekend { get; set; }

    public bool? IsWednesdayWeekend { get; set; }

    public bool? IsThursdayWeekend { get; set; }

    public bool? IsFridayWeekend { get; set; }

    public bool? IsSaturdayWeekend { get; set; }

    public bool? IsSundayWeekend { get; set; }

    public DateTime? CreateDate { get; set; }

    public int? CreatedBy { get; set; }

    /// <summary>Soft-delete flag. Nullable: only <see langword="true"/> means deleted.</summary>
    public bool? IsDelete { get; set; }

    public DateTime? DeletedDate { get; set; }

    public int? DeletedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }
}
