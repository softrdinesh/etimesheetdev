using System.ComponentModel.DataAnnotations;

namespace ETimeSheet.Shared.Configuration;

/// <summary>
/// When the <c>ProjectTimesheetReminderEmailQueue</c> job reminds a project
/// user that a project timesheet period is about to end.
/// <para>
/// Separate from the regular timesheet reminder on purpose: that one is driven
/// by each user's <c>dbo.TimesheetMasterSetup</c> (<c>NeedToSendReminder</c>,
/// <c>TimeEntryLockAt</c>, <c>ReminderTimeBeforeCutoff</c>), and none of those
/// columns is read here. The setup supplies only the user's time zone.
/// </para>
/// </summary>
public class ProjectTimesheetReminderSettings
{
    public const string SectionName = "ProjectTimesheetReminder";

    /// <summary>
    /// The time of day the reminder goes out, on <b>the user's own clock</b> -
    /// <c>"09:00:00"</c> is nine in the morning where the user is. From this
    /// time on the reminder day the user is due one; before it they are not.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "23:59:59")]
    public TimeSpan SendAt { get; set; } = new(9, 0, 0);

    /// <summary>
    /// How many days before a period's last day the reminder goes out:
    /// <c>1</c> is the day before. A period may be submitted from its last day,
    /// so this is advance notice, sent once per period.
    /// </summary>
    [Range(1, 14)]
    public int DaysBeforePeriodEnd { get; set; } = 1;
}
