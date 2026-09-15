using System.Net;
using System.Text.Json;
using ETimeSheet.Shared.Exceptions;
using ETimeSheet.Shared.Responses;

namespace ETimeSheet.Api.Middleware;

/// <summary>
/// Converts any unhandled exception into the standard <see cref="ApiResponse{T}"/>
/// envelope, so a caller never sees a raw framework error page.
/// <para>
/// Expected failures derive from <see cref="AppException"/> and carry their own
/// status code. Anything else is a genuine defect: it is logged in full and
/// reported to the caller as a bare 500 with a correlation id, never as a stack trace.
/// </para>
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client hung up. Nothing went wrong and there is nobody left to
            // answer, so this is logged as information rather than as an error.
            _logger.LogInformation(
                "Request {Method} {Path} was cancelled by the client.",
                context.Request.Method,
                context.Request.Path);

            if (!context.Response.HasStarted)
            {
                // 499, the conventional code for "client closed request".
                context.Response.StatusCode = 499;
            }
        }
        catch (AppException exception)
        {
            LogExpectedFailure(context, exception);
            await WriteAsync(context, exception.StatusCode, exception.Message, exception.Errors);
        }
        catch (Exception exception)
        {
            var correlationId = context.TraceIdentifier;

            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. CorrelationId {CorrelationId}.",
                context.Request.Method,
                context.Request.Path,
                correlationId);

            // Development gets the exception message to speed up debugging.
            // Production gets nothing that could describe the internals.
            var message = _environment.IsDevelopment()
                ? exception.Message
                : "An unexpected error occurred. Please try again later.";

            await WriteAsync(
                context,
                HttpStatusCode.InternalServerError,
                message,
                new[] { $"CorrelationId: {correlationId}" });
        }
    }

    private void LogExpectedFailure(HttpContext context, AppException exception)
    {
        // Authentication and authorisation failures are security-relevant and
        // worth a warning; ordinary rule violations are just information.
        var level = exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? LogLevel.Warning
            : LogLevel.Information;

        _logger.Log(
            level,
            "Request {Method} {Path} rejected with {StatusCode}: {Reason}",
            context.Request.Method,
            context.Request.Path,
            (int)exception.StatusCode,
            exception.Message);
    }

    private static async Task WriteAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message,
        IReadOnlyCollection<string> errors)
    {
        if (context.Response.HasStarted)
        {
            // Headers are already on the wire; rewriting the status now would
            // throw and mask the original failure.
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var payload = ApiResponse<object?>.Fail(message, errors);

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, SerializerOptions),
            context.RequestAborted);
    }
}
