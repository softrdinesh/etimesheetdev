namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// The seven days of the week, mapped onto the existing <c>dbo.DayMaster</c>
/// lookup table. This is a persistence/domain model and is never returned from a
/// controller.
/// <para>
/// <b>Read-only reference data.</b> The rows are fixed and are inserted with the
/// schema; nothing in the API adds, edits or deletes one. There is no repository
/// for this table for exactly that reason - when something needs the list, it
/// reads it, and that is all.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the table has no audit or
/// soft-delete columns at all, so there is nothing to inherit and no query
/// filter to apply.
/// </para>
/// </summary>
public class DayMaster
{
    /// <summary>
    /// Primary key, and <b>not</b> generated: the value is supplied by the seed
    /// data and carries meaning - 1 = Monday through 7 = Sunday, the ISO-8601
    /// numbering.
    /// <para>
    /// This is deliberately not <see cref="System.DayOfWeek"/>, which numbers
    /// Sunday 0 and Saturday 6. Never cast one to the other; translate through
    /// <c>Constants.DayMaster.DayId</c>.
    /// </para>
    /// </summary>
    public int DayId { get; set; }

    /// <summary>
    /// The day's full English name - "Monday" ... "Sunday". <c>varchar(20)</c>,
    /// NOT NULL and UNIQUE.
    /// </summary>
    public string Day { get; set; } = string.Empty;

    /// <summary>
    /// The day's two-letter code - "MO" ... "SU". <c>varchar(2)</c>, NOT NULL
    /// and UNIQUE.
    /// <para>
    /// Variable length, so these come back unpadded, unlike the
    /// <c>char(2)</c>/<c>char(3)</c> day columns on
    /// <c>dbo.TimesheetMasterSetup</c>. Trim and compare case-insensitively
    /// before matching a value from that table against this one.
    /// </para>
    /// </summary>
    public string DayCode { get; set; } = string.Empty;
}
