namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// The timesheet limits and working-week settings held for a user, mapped onto
/// the existing <c>dbo.TimesheetMasterSetup</c> table. This is a
/// persistence/domain model and is never returned from a controller.
/// <para>
/// Every column except <c>SetupID</c> is nullable in the database, and that is
/// reproduced faithfully here: pretending a nullable column is required would
/// throw at materialisation time on perfectly valid existing rows.
/// </para>
/// <para>
/// <b>It deliberately does not derive from <c>AuditableEntity</c>.</b> This table
/// spells its soft-delete column <c>IsDelete</c> and types it <c>bit</c>, where
/// <c>dbo.TimeLog</c> uses <c>IsDeleted</c> typed <c>int</c>. The two audit
/// conventions genuinely differ, so the columns are declared here rather than
/// inherited from a base class that does not fit.
/// </para>
/// </summary>
public class TimesheetMasterSetup
{
    /// <summary>Primary key. The only non-nullable column on the table.</summary>
    public int SetupId { get; set; }

    /// <summary>
    /// Maximum time loggable, as a <c>time(7)</c> rather than a number: "8 hours"
    /// is stored as <c>08:00:00</c>.
    /// </summary>
    public TimeSpan? MaxTimeInHrs { get; set; }

    /// <summary>Companion to <see cref="MaxTimeInHrs"/>, also a <c>time(7)</c>.</summary>
    public TimeSpan? MaxTimInMins { get; set; }

    public int? UserId { get; set; }

    public int? OrganizationId { get; set; }

    public int? ContractType { get; set; }

    /// <summary>
    /// First day of the timesheet week - a <c>dbo.DayMaster.DayID</c>, so 1 is
    /// Monday and 7 is Sunday.
    /// <para>
    /// <b>Was a <c>char(2)</c> code such as "MO" until 2026-09-17</b>, and is an
    /// <c>int</c> now. It is NOT a <see cref="System.DayOfWeek"/>, which numbers
    /// Sunday 0 - translate through <c>TimesheetWeek</c> rather than casting.
    /// </para>
    /// </summary>
    public int? StartDay { get; set; }

    /// <summary>Last day of the timesheet week. A <c>DayMaster.DayID</c>, as <see cref="StartDay"/>.</summary>
    public int? EndDay { get; set; }

    /// <summary>
    /// A day treated as an exception to the normal week - also a
    /// <c>DayMaster.DayID</c>. Was a <c>char(3)</c> code such as "SUN" until
    /// 2026-09-17.
    /// </summary>
    public int? ExceptionDay { get; set; }

    public int? CountryId { get; set; }

    /// <summary>Time of day after which entry is locked. <c>time(7)</c>.</summary>
    public TimeSpan? TimeEntryLockAt { get; set; }

    // There is deliberately NO CanUserLoggedPreDayTime here. The API returns
    // that value, but it is NOT a column on this table: mapping it produced
    // "Invalid column name 'CanUserLoggedPreDayTime'" on the first real SELECT
    // against dbo.TimesheetMasterSetup. It is derived inside
    // spc_GetTimesheetMasterSetupByUserID, so it belongs on that procedure's
    // keyless result type (TimesheetMasterSetupDetail) and nowhere else.

    // ---- audit / soft delete, as this table spells them ----

    /// <summary>
    /// Soft-delete marker. A <c>bit</c> and nullable here, unlike
    /// <c>dbo.TimeLog.IsDeleted</c> which is a non-null <c>int</c>. Null and
    /// false both mean "live".
    /// </summary>
    public bool? IsDelete { get; set; }

    public DateTime? CreateDate { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdateDate { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime? DeleteDate { get; set; }

    /// <summary>Maps the <c>Deletedby</c> column - note the database's lower-case "b".</summary>
    public int? DeletedBy { get; set; }
}
