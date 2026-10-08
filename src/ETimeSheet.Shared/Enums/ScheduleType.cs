namespace ETimeSheet.Shared.Enums;

/// <summary>
/// Values of the <c>ScheduleTypeID</c> column of <c>dbo.SchedulerConfiguration</c>
/// - how a background job's runs are spaced. <c>tinyint</c> in the table, so
/// <see cref="byte"/> here. The values are persisted, so they must stay stable.
/// </summary>
public enum ScheduleType : byte
{
    /// <summary>Runs once, at <c>ScheduleDateTime</c>.</summary>
    Once = 1,

    /// <summary>
    /// Runs every <c>RepeatInterval</c> <c>RepeatIntervalType</c> units, from
    /// <c>ScheduleDateTime</c> - or from startup, when that is null.
    /// </summary>
    Repeated = 2,

    /// <summary>Runs every day at the time of day of <c>ScheduleDateTime</c>.</summary>
    Daily = 3,

    /// <summary>Runs every week on the weekday, and at the time, of <c>ScheduleDateTime</c>.</summary>
    Weekly = 4,

    /// <summary>Runs every month on the day of month, and at the time, of <c>ScheduleDateTime</c>.</summary>
    Monthly = 5
}
