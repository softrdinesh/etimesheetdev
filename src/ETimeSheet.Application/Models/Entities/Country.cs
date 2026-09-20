namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A country, mapped onto the existing <c>dbo.Country</c> lookup table. This is
/// a persistence/domain model and is never returned from a controller.
/// <para>
/// <b>Read-only reference data.</b> The rows - and in particular
/// <see cref="TimeZone"/> - are maintained by hand in SQL Server, outside this
/// repository; nothing in the API adds, edits or deletes one. See
/// <c>docs/database/data/dbo.Country_UpdateTimeZone.sql</c> for the script that
/// populated the time zones.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the table has no soft-delete
/// column and carries only a <c>CreateDate</c>, so there is nothing to inherit
/// and no query filter to apply.
/// </para>
/// </summary>
public class Country
{
    /// <summary>Primary key - the <c>ID</c> column, and the value held in <c>dbo.TimesheetMasterSetup.CountryID</c>.</summary>
    public int Id { get; set; }

    /// <summary>The country's name. <c>nvarchar(80)</c>, nullable on the table.</summary>
    public string? Name { get; set; }

    /// <summary>The ISO 3166-1 alpha-2 code - "US", "GB". <c>nvarchar(6)</c>, nullable on the table.</summary>
    public string? Code { get; set; }

    /// <summary>
    /// The country's IANA time zone ids - <b>one value, or several separated by
    /// commas</b> when the country spans more than one zone:
    /// <c>"Europe/London"</c> for the United Kingdom,
    /// <c>"America/New_York,America/Chicago,..."</c> for the United States. The
    /// first id is the country's primary zone.
    /// <para>
    /// A single <c>nvarchar(1000)</c> column rather than a child table, because
    /// that is how the lookup is maintained. <c>CountryTimeZones</c> is the only
    /// thing that reads it apart: never split this string at a call site.
    /// </para>
    /// <para>
    /// Note the contrast with <c>dbo.TimesheetMasterSetup.TimeZone</c>, which
    /// holds exactly <b>one</b> of these ids - the zone chosen for that setup.
    /// </para>
    /// </summary>
    public string? TimeZone { get; set; }

    /// <summary>
    /// When the row was added. <c>datetimeoffset(7)</c> on this table, unlike
    /// the plain <c>datetime</c> columns elsewhere in the schema, so it is a
    /// <see cref="DateTimeOffset"/> and carries its own offset.
    /// </summary>
    public DateTimeOffset? CreateDate { get; set; }
}
