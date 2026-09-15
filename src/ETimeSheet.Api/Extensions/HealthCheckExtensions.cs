using System.Text.Json;
using ETimeSheet.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ETimeSheet.Api.Extensions;

public static class HealthCheckExtensions
{
    /// <summary>Tag for checks that prove the process is alive but say nothing about its dependencies.</summary>
    private const string LivenessTag = "live";

    /// <summary>Tag for checks that prove the API can actually serve traffic.</summary>
    private const string ReadinessTag = "ready";

    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy("The API is running."),
                tags: new[] { LivenessTag })
            // Verifies the configured connection string really reaches SQL Server
            // and that the model can be used against it.
            .AddDbContextCheck<Context>(
                "database",
                tags: new[] { ReadinessTag });

        return services;
    }

    /// <summary>
    /// Maps the health endpoints.
    /// <para>
    /// <c>/health</c> is the overall answer, and the split liveness/readiness
    /// endpoints exist because an orchestrator must be able to tell "restart me"
    /// apart from "do not send me traffic yet".
    /// </para>
    /// </summary>
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        var options = new HealthCheckOptions { ResponseWriter = WriteHealthResponseAsync };

        app.MapHealthChecks("/health", options).AllowAnonymous();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LivenessTag),
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadinessTag),
            ResponseWriter = WriteHealthResponseAsync
        }).AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Writes a small JSON body. Check descriptions are included, but exception
    /// details never are - health endpoints are usually unauthenticated.
    /// </summary>
    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
