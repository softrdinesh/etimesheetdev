using ETimeSheet.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ETimeSheet.Infrastructure.Jobs;

/// <summary>
/// Quartz entry point for the <c>SendEmail</c> job -
/// <c>SchedulerJobs.SendEmail</c>. It holds no logic: it hands straight to
/// <see cref="ISchedulerService.RunSendEmailAsync"/>, so the rules stay in the
/// Application layer. When it runs is decided by its row in
/// <c>dbo.SchedulerConfiguration</c>.
/// <para>
/// Quartz creates a DI scope per execution, so the scoped service, repository
/// and <c>Context</c> it resolves are this run's alone. Concurrent runs are
/// disallowed: a run that outlasts the interval makes the next one wait.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
public class SendEmailJob : IJob
{
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<SendEmailJob> _logger;

    public SendEmailJob(
        ISchedulerService schedulerService,
        ILogger<SendEmailJob> logger)
    {
        _schedulerService = schedulerService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _schedulerService.RunSendEmailAsync(context.CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Nothing is waiting on a background job's result, so the log is the
            // only place a failure shows up. Not refired: each email's outcome
            // is saved as soon as it is known, so the next scheduled run picks
            // up exactly the emails that are still due.
            _logger.LogError(exception, "Job {JobKey} failed.", context.JobDetail.Key);
            throw new JobExecutionException(exception, refireImmediately: false);
        }
    }
}
