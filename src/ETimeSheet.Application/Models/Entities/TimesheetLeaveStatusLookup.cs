namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One leave status, mapped onto the existing
/// <c>dbo.TimesheetLeaveStatus_Lookup</c> table - Submitted, Approved,
/// Rejected. Fixed reference data, inserted with the schema.
/// <para>
/// READ-ONLY TO THE API. The statuses belong to the database: the API looks
/// a status up by its <see cref="Text"/> and never hard-codes the ids.
/// </para>
/// </summary>
public class TimesheetLeaveStatusLookup
{
    /// <summary>The <c>ID</c> column.</summary>
    public int Id { get; set; }

    /// <summary>The <c>Value</c> column - what <c>dbo.Timesheet_Leave_User_apply.LeaveStatus</c> stores.</summary>
    public int? Value { get; set; }

    /// <summary>The <c>Text</c> column - the status's name.</summary>
    public string? Text { get; set; }
}
