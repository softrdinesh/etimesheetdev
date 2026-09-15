using Microsoft.OpenApi.Models;

namespace ETimeSheet.Api.Extensions;

public static class SwaggerExtensions
{
    private const string SecuritySchemeId = "Bearer";

    public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ETimeSheet API",
                Version = "v1",
                Description =
                    "Time tracking API. Authenticate with a bearer token that carries the " +
                    "user and role claims."
            });

            // Declares the bearer scheme so Swagger UI shows an Authorize button.
            options.AddSecurityDefinition(SecuritySchemeId, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Paste the raw JWT only - Swagger adds the \"Bearer \" prefix itself."
            });

            // Applies the scheme to every operation, so authenticated endpoints
            // are callable straight from Swagger UI once Authorize is used.
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = SecuritySchemeId
                    }
                }] = Array.Empty<string>()
            });

            options.SupportNonNullableReferenceTypes();
        });

        return services;
    }

    /// <summary>
    /// Serves Swagger UI. Exposed in development only by default: an OpenAPI
    /// document is a map of the attack surface, so publishing it in production
    /// should be a deliberate decision.
    /// </summary>
    public static WebApplication UseSwaggerUi(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ETimeSheet API v1");
            options.DocumentTitle = "ETimeSheet API";
        });

        return app;
    }
}
