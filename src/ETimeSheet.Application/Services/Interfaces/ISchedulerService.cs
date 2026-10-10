namespace ETimeSheet.Application.Services.Interfaces;

/// <summary>
/// Application contract for the Scheduler module - background jobs. Every
/// public method of <c>SchedulerService</c> is declared here; its private
/// helpers are not.
/// <para>
/// There is no controller: every registered job is scheduled at startup from
/// its <c>dbo.SchedulerConfiguration</c> row, through
/// <see cref="StartScheduledJobsAsync"/>, and each job has one method that does
/// its work, which the job calls each time it fires.
/// </para>
/// </summary>
public interface ISchedulerService
{
    /// <summary>
    /// Reads the configuration row of every registered job, by id, and
    /// schedules each one that is enabled and usable. A job with no row, a
    /// disabled row, or a row missing what its schedule type needs is logged
    /// and not scheduled. Called once, at application startup.
    /// </summary>
    Task StartScheduledJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The <c>TimeLogReminderEmailQueue</c> job's work: queues a time log reminder in
    /// <c>dbo.EmailQueue</c> for every user due one right now - reminders on,
    /// a working day, past their reminder time and short of their daily time,
    /// all on their own clock, and not already reminded today - and returns how
    /// many were queued. Run by the job, never by a request.
    /// </summary>
    Task<int> RunTimeLogReminderEmailQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The <c>TimesheetReminderEmailQueue</c> job's work: queues a timesheet
    /// submission reminder in <c>dbo.EmailQueue</c> for every user due one
    /// right now - reminders on, the last day of their week, past their
    /// reminder time, all on their own clock; this week's timesheet not yet
    /// submitted; and not already reminded today - and returns how many were
    /// queued. Run by the job, never by a request.
    /// </summary>
    Task<int> RunTimesheetReminderEmailQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The <c>SendEmail</c> job's work: sends, one by one, every queued email
    /// that is <c>Pending</c> - or failed with attempts left - and records each
    /// outcome on its row. Returns how many were sent. Run by the job, never
    /// by a request.
    /// </summary>
    Task<int> RunSendEmailAsync(CancellationToken cancellationToken = default);
}
