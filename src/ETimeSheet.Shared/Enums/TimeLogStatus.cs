// Aliased because the class ETimeSheet.Shared.Utilities.Constants collides with
// the sibling namespace ETimeSheet.Shared.Constants: an unqualified "Constants"
// inside the ETimeSheet.Shared tree binds to the namespace, not the class.
using TimeLogStatusValues = ETimeSheet.Shared.Utilities.Constants.TimeLog.Status;

namespace ETimeSheet.Shared.Enums;

/// <summary>
/// Values of the <c>Status</c> column of <c>dbo.TimeLog</c>, and of that table
/// only - other tables have their own <c>Status</c> column with its own
/// meaning, and each gets its own type rather than sharing this one.
/// <para>
/// <b>1 = Save, 2 = Draft only.</b> No other value is in use.
/// </para>
/// <para>
/// The members take their numbers from
/// <see cref="ETimeSheet.Shared.Utilities.Constants.TimeLog.Status"/> rather
/// than repeating the literals, so the enum and the constants cannot disagree.
/// The values are persisted, so they must stay stable.
/// </para>
/// </summary>
public enum TimeLogStatus
{
    /// <summary>A saved entry. Stored as <c>1</c>.</summary>
    Save = TimeLogStatusValues.Save,

    /// <summary>A draft entry. Stored as <c>2</c>.</summary>
    Draft = TimeLogStatusValues.Draft
}
