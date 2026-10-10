using ETimeSheet.Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ETimeSheet.Infrastructure.Jobs;

/// <summary>
/// Schedules every registered background job from <c>dbo.SchedulerConfiguration</c>
/// when the application starts. A row changed afterwards takes effect at the
/// next restart.
/// <para>
/// Holds no logic of its own: what a row means is decided by
/// <see cref="ISchedulerService.StartScheduledJobsAsync"/>. It only exists
/// because something has to call that at startup, outside any request.
/// </para>
/// </summary>
public class ScheduledJobsHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledJobsHostedService> _logger;

    public ScheduledJobsHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledJobsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A hosted service is a singleton and ISchedulerService is scoped, so
            // it gets a scope - and a Context - of its own.
            using var scope = _scopeFactory.CreateScope();

            await scope.ServiceProvider
                .GetRequiredService<ISchedulerService>()
                .StartScheduledJobsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The database being unreachable at startup must not stop the API
            // serving requests; the jobs simply do not run until a restart.
            _logger.LogError(exception, "Could not read the scheduler configuration; no background job was scheduled.");
        }
    }

    // Nothing to stop: Quartz's own hosted service shuts the scheduler down.
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
