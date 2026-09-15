using System.Text.Json.Serialization;
using ETimeSheet.Shared.Responses;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;

namespace ETimeSheet.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers controllers, request validation and JSON behaviour.
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                // A missing or unparsable body is a client error, so surface it
                // as a validation failure rather than a null model.
                options.SuppressAsyncSuffixInActionNames = false;
            })
            .AddJsonOptions(options =>
            {
                // Enums travel as their names, which keeps the contract readable
                // and stable when new members are inserted.
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition =
                    JsonIgnoreCondition.WhenWritingNull;
            });

        // Runs the FluentValidation validators registered by the Application
        // layer as part of model binding, before the action is entered.
        services.AddFluentValidationAutoValidation(options =>
        {
            options.DisableDataAnnotationsValidation = true;
        });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            // Replaces the default ProblemDetails body so that validation
            // failures use the same envelope as every other response.
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .SelectMany(entry => entry.Value!.Errors.Select(error =>
                        string.IsNullOrWhiteSpace(entry.Key)
                            ? error.ErrorMessage
                            : $"{entry.Key}: {error.ErrorMessage}"))
                    .ToArray();

                return new BadRequestObjectResult(
                    ApiResponse.Fail("One or more validation errors occurred.", errors));
            };
        });

        return services;
    }
}
