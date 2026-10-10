using System.Globalization;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models;
using ETimeSheet.Shared.Enums;
using ETimeSheet.Shared.Exceptions;
using Quartz;

namespace ETimeSheet.Infrastructure.Services;

/// <summary>
/// <see cref="IBackgroundJobService"/> over Quartz, on the jobs registered in
/// <c>AddSchedulerServices</c>.
/// <para>
/// Each job has <b>one</b> trigger, under a fixed key, so scheduling it again
/// replaces that trigger instead of stacking a second one beside it.
/// </para>
/// </summary>
public class QuartzBackgroundJobService : IBackgroundJobService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public QuartzBackgroundJobService(
        ISchedulerFactory schedulerFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _schedulerFactory = schedulerFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// The Quartz key a job is registered under: its
    /// <c>SchedulerConfigurationID</c>, as text.
    /// </summary>
    public static JobKey JobKeyFor(int jobId) => new(jobId.ToString(CultureInfo.InvariantCulture));

    public async Task ScheduleAsync(
        int jobId,
        JobSchedule schedule,
        CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = JobKeyFor(jobId);

        // Checked first so an unknown id is a clear NotFoundException, not a
        // JobPersistenceException.
        if (!await scheduler.CheckExists(jobKey, cancellationToken))
        {
            throw new NotFoundException($"No job is registered for scheduler configuration '{jobId}'.");
        }

        var triggerKey = new TriggerKey($"{jobKey.Name}-trigger");
        var trigger = BuildTrigger(schedule, triggerKey, jobKey, _dateTimeProvider.UtcNow);

        if (await scheduler.CheckExists(triggerKey, cancellationToken))
        {
            await scheduler.RescheduleJob(triggerKey, trigger, cancellationToken);
        }
        else
        {
            await scheduler.ScheduleJob(trigger, cancellationToken);
        }
    }

    private static ITrigger BuildTrigger(JobSchedule schedule, TriggerKey triggerKey, JobKey jobKey, DateTime utcNow)
    {
        var builder = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey);

        // SchedulerService guarantees StartsAt for every type but Repeated, and
        // Interval for Repeated.
        switch (schedule.Type)
        {
            case ScheduleType.Once:
                return builder
                    .StartAt(InstantOf(schedule.StartsAt!.Value))
                    .WithSimpleSchedule(simple => simple.WithMisfireHandlingInstructionFireNow())
                    .Build();

            case ScheduleType.Repeated:
                var interval = schedule.Interval!.Value;

                builder = schedule.StartsAt is { } startsAt
                    ? builder.StartAt(NextSlot(InstantOf(startsAt), interval, utcNow))
                    : builder.StartNow();

                return builder
                    .WithSimpleSchedule(simple => simple
                        .WithInterval(interval)
                        .RepeatForever()
                        // A run that overruns its slot skips the missed slots
                        // rather than firing a burst to catch up.
                        .WithMisfireHandlingInstructionNextWithRemainingCount())
                    .Build();

            default:
                // Daily, weekly, monthly: a cron at the wall-clock time of
                // StartsAt, in UTC, never before StartsAt itself.
                return builder
                    .StartAt(InstantOf(schedule.StartsAt!.Value))
                    .WithCronSchedule(
                        CronFor(schedule.Type, schedule.StartsAt.Value),
                        cron => cron.InTimeZone(TimeZoneInfo.Utc).WithMisfireHandlingInstructionDoNothing())
                    .Build();
        }
    }

    /// <summary>
    /// Quartz cron: seconds, minutes, hours, day of month, month, day of week.
    /// <para>
    /// A monthly job on the 31st runs on the last day of every month (<c>L</c>);
    /// one on the 29th or 30th skips the months that do not have that day.
    /// </para>
    /// </summary>
    private static string CronFor(ScheduleType type, DateTime at)
    {
        var time = $"{at.Second} {at.Minute} {at.Hour}";

        return type switch
        {
            ScheduleType.Daily => $"{time} ? * *",
            ScheduleType.Weekly => $"{time} ? * {at.DayOfWeek.ToString()[..3].ToUpperInvariant()}",
            ScheduleType.Monthly => $"{time} {(at.Day == 31 ? "L" : at.Day.ToString(CultureInfo.InvariantCulture))} * ?",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a calendar schedule type.")
        };
    }

    /// <summary>
    /// The first slot of <c>start + n * interval</c> that is not in the past,
    /// so runs stay on the configured grid - a 15-minute job started at 00:09:02
    /// runs at :09:02, :24:02, :39:02 and :54:02, whenever the API comes up.
    /// <para>
    /// Computed here rather than left to Quartz: given a past start, Quartz's
    /// misfire handling counts the interval from startup instead, so the slots
    /// drift with every restart.
    /// </para>
    /// </summary>
    private static DateTimeOffset NextSlot(DateTimeOffset start, TimeSpan interval, DateTime utcNow)
    {
        var now = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

        if (start >= now)
        {
            return start;
        }

        var slotsPassed = (now - start).Ticks / interval.Ticks + 1;

        return start + TimeSpan.FromTicks(slotsPassed * interval.Ticks);
    }

    /// <summary><c>ScheduleDateTime</c> is UTC.</summary>
    private static DateTimeOffset InstantOf(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
}
