using ETimeSheet.Shared.Configuration;

namespace ETimeSheet.Api.Configuration;

/// <summary>
/// Binds every configuration section to its settings class.
/// <para>
/// Each binding is validated with data annotations and <c>ValidateOnStart</c>,
/// so a deployment with a missing connection string or a weak JWT secret fails
/// at startup instead of failing on the first request that needs it.
/// </para>
/// </summary>
public static class OptionsRegistrationExtensions
{
    public static IServiceCollection AddApplicationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddValidatedOptions<JwtSettings>(configuration, JwtSettings.SectionName);
        services.AddValidatedOptions<DatabaseSettings>(configuration, DatabaseSettings.SectionName);
        services.AddValidatedOptions<CacheSettings>(configuration, CacheSettings.SectionName);

        // CORS carries no [Required] members: an empty origin list is a valid
        // configuration meaning "no browser client", so it is bound unvalidated.
        services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));

        return services;
    }

    private static IServiceCollection AddValidatedOptions<TSettings>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TSettings : class
    {
        services
            .AddOptions<TSettings>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
