namespace ETimeSheet.Application.DTOs.Admins;

/// <summary>
/// Body of the timesheet setup save request - <b>one payload for both insert and
/// update</b>.
/// <para>
/// There is deliberately no setup id here. A user holds exactly one setup, so
/// <see cref="UserId"/> already identifies the row: the service looks the user
/// up and updates their setup if they have one, revives and overwrites it if
/// theirs was deleted, and inserts only when they have neither. The client never
/// has to know which of the three happened, and cannot create a second row for a
/// user by sending the wrong id.
/// </para>
/// <para>
/// Every field except <see cref="UserId"/> and <see cref="CreatedBy"/> is
/// optional, mirroring the table: every column on
/// <c>dbo.TimesheetMasterSetup</c> other than the key is nullable.
/// </para>
/// </summary>
public class AdminSaveRequest
{
    /// <summary>The user these settings belong to. Required - it is what identifies the row to save.</summary>
    public int UserId { get; set; }

    /// <summary>
    /// Maximum time loggable, as a duration rather than a number: 8 hours is
    /// <c>"08:00:00"</c>. The column is <c>time(7)</c>, so the value must be
    /// inside a single day.
    /// </summary>
    public TimeSpan? MaxTimeInHrs { get; set; }

    /// <summary>Companion to <see cref="MaxTimeInHrs"/>, also a <c>time(7)</c>.</summary>
    public TimeSpan? MaxTimInMins { get; set; }

    public int? OrganizationId { get; set; }

    public int? ContractType { get; set; }

    /// <summary>
    /// First day of the timesheet week - a <c>dbo.DayMaster.DayID</c>: 1 =
    /// Monday, 2 = Tuesday ... 7 = Sunday. Send 1, not <c>"MO"</c>; the column
    /// stopped being a two-letter code on 2026-09-17.
    /// </summary>
    public int? StartDay { get; set; }

    /// <summary>Last day of the timesheet week. A day id, as <see cref="StartDay"/>. Send it with <see cref="StartDay"/> or not at all.</summary>
    public int? EndDay { get; set; }

    /// <summary>
    /// A day worked in addition to the normal week - also a
    /// <c>dbo.DayMaster.DayID</c>. Send 7 for Sunday, not <c>"SUN"</c>.
    /// </summary>
    public int? ExceptionDay { get; set; }

    public int? CountryId { get; set; }

    /// <summary>Time of day after which entry is locked, for example <c>"18:00:00"</c>.</summary>
    public TimeSpan? TimeEntryLockAt { get; set; }

    // CanUserLoggedPreDayTime is deliberately absent: it is not a column on
    // dbo.TimesheetMasterSetup, it is derived by
    // spc_GetTimesheetMasterSetupByUserID. There is nothing here to store.

    /// <summary>
    /// The user performing the save.
    /// <para>
    /// Written to the row's <c>CreatedBy</c> when the save inserts, and to its
    /// <c>UpdatedBy</c> when the save updates or revives - the payload carries
    /// one "who is doing this", and which audit column it lands in follows from
    /// what the save turned out to be.
    /// </para>
    /// <para>
    /// <b>Temporary.</b> This belongs in the token, not in the payload - a
    /// caller can currently claim to be anyone. It moves to the authenticated
    /// principal the moment JWT is switched back on, and this property is then
    /// deleted from the contract.
    /// </para>
    /// </summary>
    public int CreatedBy { get; set; }
}
