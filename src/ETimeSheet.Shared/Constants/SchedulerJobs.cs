namespace ETimeSheet.Shared.Constants;

/// <summary>
/// The background jobs, by the <c>SchedulerConfigurationID</c> of their row in
/// <c>dbo.SchedulerConfiguration</c>. At startup each registered job's schedule
/// is read from the row with its id. Never write one of these ids inline.
/// </summary>
public static class SchedulerJobs
{
    /// <summary>
    /// Sends every queued email that is still due an attempt, one by one,
    /// through the external email service.
    /// </summary>
    public const int SendEmail = 1;

    /// <summary>
    /// Queues a time log reminder in <c>dbo.EmailQueue</c> for every user who
    /// is due one - see <c>SchedulerService.RunTimeLogReminderEmailQueueAsync</c>.
    /// </summary>
    public const int TimeLogReminderEmailQueue = 2;

    /// <summary>
    /// Queues a timesheet submission reminder in <c>dbo.EmailQueue</c> for
    /// every user who is due one - see
    /// <c>SchedulerService.RunTimesheetReminderEmailQueueAsync</c>.
    /// </summary>
    public const int TimesheetReminderEmailQueue = 3;

    /// <summary>
    /// Queues one approval reminder in <c>dbo.EmailQueue</c> per project whose
    /// submission period ended yesterday with sheets still Submitted, to its
    /// timesheet approver -
    /// see <c>SchedulerService.RunApproverReminderEmailQueueAsync</c>. Meant to
    /// run once a day, at 7 AM (a Daily row).
    /// </summary>
    public const int ApproverReminderEmailQueue = 4;

    /// <summary>
    /// The ids that have a job registered with Quartz - the only ones whose
    /// configuration is read and scheduled.
    /// </summary>
    public static readonly IReadOnlyList<int> Registered = new[]
    {
        SendEmail, TimeLogReminderEmailQueue, TimesheetReminderEmailQueue, ApproverReminderEmailQueue
    };
}
