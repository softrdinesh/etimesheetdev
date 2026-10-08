using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Interfaces.Services;

/// <summary>
/// Schedules registered background jobs.
/// <para>
/// The abstraction keeps Quartz out of the Application layer, the same way
/// <see cref="IDateTimeProvider"/> keeps the system clock out: the service says
/// <i>which</i> job and <i>when</i>, the Infrastructure implementation decides
/// <i>how</i>.
/// </para>
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>
    /// Puts the job registered under <paramref name="jobId"/> - one of
    /// <c>ETimeSheet.Shared.Constants.SchedulerJobs</c> - on
    /// <paramref name="schedule"/>, replacing any schedule it already has.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No job is registered under <paramref name="jobId"/>.
    /// </exception>
    Task ScheduleAsync(
        int jobId,
        JobSchedule schedule,
        CancellationToken cancellationToken = default);
}
