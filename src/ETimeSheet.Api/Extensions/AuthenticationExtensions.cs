using System.Text;
using ETimeSheet.Shared.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ETimeSheet.Api.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Configures JWT bearer authentication with every validation switched on:
    /// issuer, audience, signing key and token lifetime.
    /// <para>
    /// The settings are resolved from <see cref="IOptions{TOptions}"/> when the
    /// bearer options are first built, rather than read from
    /// <c>IConfiguration</c> during registration. That keeps a single source of
    /// truth - the validated <see cref="JwtSettings"/> - and means any
    /// configuration source added later in the host's lifetime still applies.
    /// </para>
    /// </summary>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>(ConfigureBearer);

        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core authorization with <b>no fallback policy</b>, so
    /// an endpoint is anonymous unless it carries <c>[Authorize]</c>.
    /// <para>
    /// The deny-by-default fallback that used to live here challenged every
    /// request that reached the authorization middleware - including paths that
    /// match no endpoint at all - which turned a mistyped URL into a 401 bearer
    /// challenge instead of a 404 and made the whole host look locked down. That
    /// is the wrong trade while security is off and the API is deliberately
    /// unauthenticated.
    /// </para>
    /// <para>
    /// When JWT is switched back on, restore <c>options.FallbackPolicy =
    /// options.DefaultPolicy</c> here and take <c>[AllowAnonymous]</c> off the
    /// controllers - the two changes belong in the same commit.
    /// </para>
    /// </summary>
    public static IServiceCollection AddAuthorizationServices(this IServiceCollection services)
    {
        services.AddAuthorization();

        return services;
    }

    private static void ConfigureBearer(JwtBearerOptions options, IOptions<JwtSettings> jwtOptions)
    {
        // Reading .Value here triggers the data-annotation validation on
        // JwtSettings, so a missing or too-short secret fails loudly.
        var jwtSettings = jwtOptions.Value;

        options.RequireHttpsMetadata = true;

        // The raw token is not kept on the authentication properties: nothing in
        // the application needs it after validation, and not storing it keeps it
        // out of anything that might serialise the auth ticket.
        options.SaveToken = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),

            ValidateLifetime = true,
            RequireExpirationTime = true,

            // Default skew is five minutes, which silently extends every token's
            // life. Zero by default here, tunable per environment.
            ClockSkew = TimeSpan.FromSeconds(jwtSettings.ClockSkewSeconds)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("ETimeSheet.Authentication");

                // The reason is logged; the token itself never is.
                logger.LogWarning(
                    "Bearer token rejected for {Path}: {Reason}",
                    context.HttpContext.Request.Path,
                    context.Exception.GetType().Name);

                return Task.CompletedTask;
            }
        };
    }
}
