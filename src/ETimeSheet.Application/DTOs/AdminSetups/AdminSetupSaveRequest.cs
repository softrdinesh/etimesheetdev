namespace ETimeSheet.Application.DTOs.AdminSetups;

/// <summary>
/// Body of the timesheet setup save request - <b>one payload for both insert and
/// update</b>.
/// <para>
/// <see cref="SetupId"/> is what decides which one happens: supply it and the
/// existing row is updated, leave it out (or send null) and a new row is
/// inserted. There is deliberately no separate "create" and "update" endpoint,
/// because the client should not have to know which it is doing.
/// </para>
/// <para>
/// Every field except <see cref="UserId"/> and <see cref="PerformedBy"/> is
/// optional, mirroring the table: every column on
/// <c>dbo.TimesheetMasterSetup</c> other than the key is nullable.
/// </para>
/// </summary>
public class AdminSetupSaveRequest
{
    /// <summary>
    /// The row to update. <b>Null (or absent) means insert.</b> When supplied it
    /// must identify a live, non-deleted row, or the call is a 404.
    /// </summary>
    public int? SetupId { get; set; }

    /// <summary>The user these settings belong to. Required - a setup with no owner cannot be applied to anyone.</summary>
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
    /// First day of the timesheet week - a two-character code such as
    /// <c>"MO"</c>. Stored in a <c>char(2)</c>, so exactly two letters.
    /// </summary>
    public string? StartDay { get; set; }

    /// <summary>Last day of the timesheet week. Two letters, as <see cref="StartDay"/>.</summary>
    public string? EndDay { get; set; }

    /// <summary>
    /// A day treated as an exception to the normal week - a three-character code
    /// such as <c>"SUN"</c>. Stored in a <c>char(3)</c>.
    /// </summary>
    public string? ExceptionDay { get; set; }

    public int? CountryId { get; set; }

    /// <summary>Time of day after which entry is locked, for example <c>"18:00:00"</c>.</summary>
    public TimeSpan? TimeEntryLockAt { get; set; }

    // CanUserLoggedPreDayTime is deliberately absent: it is not a column on
    // dbo.TimesheetMasterSetup, it is derived by
    // spc_GetTimesheetMasterSetupByUserID. There is nothing here to store.

    /// <summary>
    /// The user performing the save. Written to <c>CreatedBy</c> on an insert
    /// and to <c>UpdatedBy</c> on an update.
    /// <para>
    /// <b>Temporary.</b> This belongs in the token, not in the payload - a
    /// caller can currently claim to be anyone. It moves to the authenticated
    /// principal the moment JWT is switched back on, and this property is then
    /// deleted from the contract.
    /// </para>
    /// </summary>
    public int PerformedBy { get; set; }
}
