using ETimeSheet.Api.Middleware;

namespace ETimeSheet.Api.Extensions;

public static class MiddlewareExtensions
{
    /// <summary>
    /// Registers the global exception handler. Must be the outermost middleware
    /// so that it can catch failures thrown by everything after it.
    /// </summary>
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
