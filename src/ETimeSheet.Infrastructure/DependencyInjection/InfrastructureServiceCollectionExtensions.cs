using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Infrastructure.Data;
using ETimeSheet.Infrastructure.Data.Interceptors;
using ETimeSheet.Infrastructure.Jobs;
using ETimeSheet.Infrastructure.Repositories;
using ETimeSheet.Infrastructure.Services;
using ETimeSheet.Shared.Configuration;
using ETimeSheet.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace ETimeSheet.Infrastructure.DependencyInjection;

/// <summary>
/// Composition root for the Infrastructure layer.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF Core context, repositories and the technical services
    /// behind the Application layer's abstractions.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddPersistence();
        services.AddRepositories();
        services.AddPlatformServices();

        return services;
    }

    /// <summary>
    /// Registers <see cref="IMemoryCache"/> and the cache abstraction.
    /// <para>
    /// <c>MemoryCacheService</c> is a singleton because it owns the key index
    /// that makes prefix invalidation work; that index must outlive a request.
    /// </para>
    /// </summary>
    public static IServiceCollection AddCachingServices(this IServiceCollection services)
    {
        services.AddMemoryCache();

        // The size limit is policy, so it is taken from CacheSettings rather
        // than hard-coded. Every entry written by MemoryCacheService declares a
        // size of 1, which is what makes this limit meaningful.
        services.AddOptions<MemoryCacheOptions>()
            .Configure<IOptions<CacheSettings>>((options, cacheSettings) =>
                options.SizeLimit = cacheSettings.Value.SizeLimit);

        services.AddSingleton<ICacheService, MemoryCacheService>();

        return services;
    }

    /// <summary>
    /// Registers Quartz, every background job, the
    /// <see cref="IBackgroundJobService"/> the Application layer schedules them
    /// through, and the hosted service that schedules them at startup.
    /// <para>
    /// A job is registered here with <b>no schedule</b> - stored durably so it
    /// can exist without one - under its <c>SchedulerConfigurationID</c> from
    /// <see cref="SchedulerJobs"/>. Its schedule is that row of
    /// <c>dbo.SchedulerConfiguration</c>, applied by
    /// <see cref="ScheduledJobsHostedService"/>. A new job is one <c>AddJob</c>
    /// line here, its id in <see cref="SchedulerJobs.Registered"/>, and a row.
    /// </para>
    /// <para>
    /// The store is in memory: a restart forgets every schedule, and startup
    /// reads them all again.
    /// </para>
    /// </summary>
    public static IServiceCollection AddSchedulerServices(this IServiceCollection services)
    {
        services.AddQuartz(quartz =>
        {
            quartz.AddJob<SendEmailJob>(job => job
                .WithIdentity(QuartzBackgroundJobService.JobKeyFor(SchedulerJobs.SendEmail))
                .StoreDurably());

            quartz.AddJob<TimeLogReminderEmailQueueJob>(job => job
                .WithIdentity(QuartzBackgroundJobService.JobKeyFor(SchedulerJobs.TimeLogReminderEmailQueue))
                .StoreDurably());

            quartz.AddJob<TimesheetReminderEmailQueueJob>(job => job
                .WithIdentity(QuartzBackgroundJobService.JobKeyFor(SchedulerJobs.TimesheetReminderEmailQueue))
                .StoreDurably());
        });

        // On shutdown, let a running job finish rather than cut it off halfway.
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        services.AddSingleton<IBackgroundJobService, QuartzBackgroundJobService>();

        // The SendEmail job's two collaborators: the external email service,
        // as a typed HttpClient so handlers are pooled, and the HTML templates.
        services.AddHttpClient<IEmailSender, HttpEmailSender>((provider, client) =>
            client.Timeout = TimeSpan.FromSeconds(
                provider.GetRequiredService<IOptions<EmailServiceSettings>>().Value.TimeoutSeconds));

        services.AddSingleton<IEmailTemplateStore, FileEmailTemplateStore>();

        // Registered after Quartz's own hosted service, so it starts after it.
        services.AddHostedService<ScheduledJobsHostedService>();

        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<Context>((serviceProvider, options) =>
        {
            var settings = serviceProvider
                .GetRequiredService<IOptions<DatabaseSettings>>()
                .Value;

            options.UseSqlServer(settings.ConnectionString, sqlServer =>
            {
                // Retries cover the transient failures that are routine against
                // Azure SQL and during failover.
                sqlServer.EnableRetryOnFailure(settings.MaxRetryCount);
                sqlServer.CommandTimeout(settings.CommandTimeoutSeconds);

                // No MigrationsHistoryTable, and no migrations to put in one:
                // the schema is database-first and changed by hand. Nothing in
                // this application creates, alters or drops it.
            });

            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditableEntityInterceptor>());

            if (settings.EnableSensitiveDataLogging)
            {
                // Guarded by configuration and intended for local debugging only:
                // this writes parameter values into the log.
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // One registration per feature repository. Scoped, to share the
        // request's DbContext and its change tracker.
        services.AddScoped<ITimeLogRepository, TimeLogRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<ISheetSubmissionRepository, SheetSubmissionRepository>();
        services.AddScoped<ISchedulerRepository, SchedulerRepository>();

        // Not a feature repository: dbo.EmailQueue is written by the scheduler
        // (queueing) and by AdminService (closing a deleted setup's emails).
        services.AddScoped<IEmailQueueRepository, EmailQueueRepository>();

        // Not a feature repository: dbo.Country is a read-only lookup, and
        // AdminService reads it to resolve a setup's time zone.
        services.AddScoped<ICountryRepository, CountryRepository>();

        return services;
    }

    private static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }
}
