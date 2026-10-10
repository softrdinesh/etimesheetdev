using ETimeSheet.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ETimeSheet.Infrastructure.Jobs;

/// <summary>
/// Quartz entry point for the <c>TimesheetReminderEmailQueue</c> job -
/// <c>SchedulerJobs.TimesheetReminderEmailQueue</c>. It holds no logic: it hands straight
/// to <see cref="ISchedulerService.RunTimesheetReminderEmailQueueAsync"/>, so the rules stay in the
/// Application layer. When it runs is decided by its row in
/// <c>dbo.SchedulerConfiguration</c>.
/// <para>
/// Quartz creates a DI scope per execution, so the scoped service, repository
/// and <c>Context</c> it resolves are this run's alone. Concurrent runs are
/// disallowed: a run that outlasts the interval makes the next one wait.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
public class TimesheetReminderEmailQueueJob : IJob
{
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<TimesheetReminderEmailQueueJob> _logger;

    public TimesheetReminderEmailQueueJob(
        ISchedulerService schedulerService,
        ILogger<TimesheetReminderEmailQueueJob> logger)
    {
        _schedulerService = schedulerService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _schedulerService.RunTimesheetReminderEmailQueueAsync(context.CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Nothing is waiting on a background job's result, so the log is the
            // only place a failure shows up. Not refired: the insert is one
            // transaction, so a failed run queued nothing, and the next
            // scheduled run tries again.
            _logger.LogError(exception, "Job {JobKey} failed.", context.JobDetail.Key);
            throw new JobExecutionException(exception, refireImmediately: false);
        }
    }
}
