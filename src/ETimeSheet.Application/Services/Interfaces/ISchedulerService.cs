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
    /// The <c>ApproverReminderEmailQueue</c> job's work, run once a day at
    /// 7 AM: queues one approval reminder in <c>dbo.EmailQueue</c> per project
    /// whose submission period ended yesterday - weekly, every 15 days or
    /// monthly, counted from the project's start date - and that has at least
    /// one sheet for that period still Submitted, addressed to the project's
    /// timesheet approver and naming the project, unless one was already
    /// queued for that approver and project today. An approver of three such
    /// projects gets three. Returns how many were queued. Run by the job,
    /// never by a request.
    /// </summary>
    Task<int> RunApproverReminderEmailQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The <c>AutoApproveProjectSheet</c> job's work, run once a day:
    /// approves every live project sheet that is still <c>Submitted</c> and
    /// whose project has an <c>AutoApproveInDays</c>, counting from the
    /// sheet's <c>SubmittedDate</c> - a project set to 3 days approves a sheet
    /// three days after it was submitted, whether or not its approver ever
    /// looked at it. Each sheet is moved to <c>Approved</c> with
    /// <c>ApprovedBy</c> set to the configured
    /// <c>AutoApprove:ApprovedByUserId</c> and an audit row recording the
    /// change, all in one transaction. A sheet with no <c>SubmittedDate</c> is
    /// left alone - there is no day to count from. Returns how many were
    /// approved. Run by the job, never by a request.
    /// </summary>
    Task<int> RunAutoApproveProjectSheetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The <c>SendEmail</c> job's work: sends, one by one, every queued email
    /// - time log, timesheet and approver reminders alike -
    /// that is <c>Pending</c> - or failed with attempts left - and records each
    /// outcome on its row. Returns how many were sent. Run by the job, never
    /// by a request.
    /// </summary>
    Task<int> RunSendEmailAsync(CancellationToken cancellationToken = default);
}
