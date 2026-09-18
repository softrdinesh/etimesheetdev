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

                // EVERY property is written, every time - a null comes back as
                // null rather than vanishing.
                //
                // This was WhenWritingNull, and that made the response shape
                // depend on the data: two calls to the same endpoint returned
                // different sets of keys, so an employee with no timesheet setup
                // simply had no setupId, expectedHoursPerWeek or contractType at
                // all. A client then cannot tell "the field is absent because
                // this row has no value" from "the field is absent because I
                // called the wrong version, or misspelled it, or it was
                // removed" - and every consumer ends up writing defensive
                // lookups for fields the contract says are always there.
                //
                // Never is the default, so this line only has to say so out
                // loud. It is written explicitly to stop the old behaviour being
                // reintroduced as a payload-size optimisation: the bytes saved
                // are not worth an unstable contract.
                options.JsonSerializerOptions.DefaultIgnoreCondition =
                    JsonIgnoreCondition.Never;
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
