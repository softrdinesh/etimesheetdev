using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Infrastructure.Data;
using ETimeSheet.Infrastructure.Data.Interceptors;
using ETimeSheet.Infrastructure.Repositories;
using ETimeSheet.Infrastructure.Services;
using ETimeSheet.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
