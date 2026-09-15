using ETimeSheet.Shared.Configuration;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace ETimeSheet.Api.Extensions;

public static class CorsExtensions
{
    /// <summary>
    /// Builds the named CORS policy from <see cref="CorsSettings"/>.
    /// <para>
    /// There is deliberately no <c>AllowAnyOrigin</c> path: an empty allow-list
    /// produces a policy that permits nothing, which fails visibly in the
    /// browser rather than quietly opening the API to every site.
    /// </para>
    /// </summary>
    public static IServiceCollection AddCorsServices(this IServiceCollection services)
    {
        services.AddCors();

        // Configured through IOptions so the policy reflects the settings the
        // host actually resolved, not a snapshot taken during registration.
        services
            .AddOptions<CorsOptions>()
            .Configure<IOptions<CorsSettings>>((options, corsOptions) =>
            {
                var settings = corsOptions.Value;

                options.AddPolicy(CorsSettings.PolicyName, policy =>
                {
                    if (settings.AllowedOrigins.Length == 0)
                    {
                        return;
                    }

                    policy
                        .WithOrigins(settings.AllowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();

                    if (settings.AllowCredentials)
                    {
                        policy.AllowCredentials();
                    }
                });
            });

        return services;
    }
}
