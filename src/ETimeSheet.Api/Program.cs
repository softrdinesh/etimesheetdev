using ETimeSheet.Api.Configuration;
using ETimeSheet.Api.Extensions;
using ETimeSheet.Application.Common.DependencyInjection;
using ETimeSheet.Infrastructure.DependencyInjection;
using ETimeSheet.Shared.Configuration;

var builder = WebApplication.CreateBuilder(args);

// --- Composition -----------------------------------------------------------
// Every registration lives behind one of these extension methods, so this file
// stays a readable description of what the application is made of.
builder.Services
    .AddApplicationOptions(builder.Configuration)
    .AddApplicationServices()
    .AddInfrastructureServices()
    .AddCachingServices()
    .AddAuthenticationServices()
    .AddAuthorizationServices()
    .AddCorsServices()
    .AddHealthCheckServices()
    .AddApiServices()
    .AddSwaggerServices();

var app = builder.Build();

// --- Pipeline --------------------------------------------------------------
// Order matters. The exception handler is outermost so that it can catch
// failures from everything below it, including authentication.
app.UseExceptionHandling();


app.UseSwaggerUi();


app.UseCors(CorsSettings.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthEndpoints();

await app.RunAsync();

/// <summary>
/// Named partial so that <c>WebApplicationFactory&lt;Program&gt;</c> in the test
/// project can boot this exact pipeline. Top-level statements otherwise generate
/// an internal class that tests cannot reference.
/// </summary>
public partial class Program
{
}
