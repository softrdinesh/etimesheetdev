using ETimeSheet.Application.Services.Implementations;
using ETimeSheet.Application.Services.Interfaces;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ETimeSheet.Application.Common.DependencyInjection;

/// <summary>
/// Composition root for the Application layer, so that <c>Program.cs</c> never
/// has to know the individual service types.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers business services and request validators.
    /// <para>
    /// Services are scoped: they depend on the current request's identity and on
    /// the request-scoped repository/DbContext.
    /// </para>
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<ITimeLogService, TimeLogService>();
        services.AddScoped<IAdminService, AdminService>();

        // Picks up every AbstractValidator in this assembly, so a new validator
        // is wired up by the act of creating it.
        services.AddValidatorsFromAssemblyContaining<ITimeLogService>(ServiceLifetime.Scoped);

        return services;
    }
}
