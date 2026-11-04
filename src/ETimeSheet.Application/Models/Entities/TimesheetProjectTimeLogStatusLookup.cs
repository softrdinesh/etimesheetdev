namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One status a time log entry may carry, mapped onto the
/// <c>dbo.TimesheetProjectTimeLogStatus_Lookup</c> table - the statuses of
/// <c>dbo.TimesheetProjectTimeLog.StatusID</c>. Reference data, owned by the
/// database.
/// <para>
/// The table is <b>empty today</b> on purpose: the statuses have not been
/// agreed. Until rows exist, every entry's status reads back with a null
/// name.
/// </para>
/// <para>
/// READ-ONLY TO THE API. Nothing writes it, and nothing hard-codes an id:
/// the time log endpoints join it for the name only.
/// </para>
/// </summary>
public class TimesheetProjectTimeLogStatusLookup
{
    /// <summary>The <c>StatusID</c> column - what <c>dbo.TimesheetProjectTimeLog.StatusID</c> stores. Not generated.</summary>
    public int StatusId { get; set; }

    /// <summary>The <c>StatusName</c> column - the status's name. <c>nvarchar(50)</c>, NOT NULL, unique.</summary>
    public string StatusName { get; set; } = string.Empty;
}
