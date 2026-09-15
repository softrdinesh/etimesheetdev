using System.Reflection;
using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Services.Implementations;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Infrastructure.Data;
using ETimeSheet.Infrastructure.Repositories;

namespace ETimeSheet.Tests.Unit.Architecture;

/// <summary>
/// Executable versions of the architectural rules in CLAUDE.md.
/// <para>
/// A convention that is only written down drifts. These tests fail the build
/// the moment a service or repository grows a public method that is not on its
/// interface, or a layer reaches somewhere it should not.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class ArchitectureRuleTests
{
    [Theory]
    [InlineData(typeof(TimeLogService), typeof(ITimeLogService))]
    [InlineData(typeof(AuthorizationService), typeof(IAuthorizationService))]
    [InlineData(typeof(TimeLogRepository), typeof(ITimeLogRepository))]
    public void EveryPublicMethodIsDeclaredOnTheInterface(
        Type implementationType,
        Type interfaceType)
    {
        var declaredOnInterface = interfaceType
            .GetMethods()
            .Select(Signature)
            .ToHashSet(StringComparer.Ordinal);

        var publicMethods = implementationType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .ToArray();

        var undeclared = publicMethods
            .Select(Signature)
            .Where(signature => !declaredOnInterface.Contains(signature))
            .ToArray();

        Assert.True(
            undeclared.Length == 0,
            $"{implementationType.Name} exposes public members that are not on " +
            $"{interfaceType.Name}: {string.Join(", ", undeclared)}. Either add them to the " +
            "interface or make them private.");
    }

    [Fact]
    public void TheApplicationLayerCannotSeeEntityFrameworkCore()
    {
        var referenced = typeof(ITimeLogService).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        // This is what makes "services never touch DbContext" a compile-time
        // guarantee rather than a code-review promise.
        Assert.DoesNotContain(
            referenced,
            name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void TheApplicationLayerDoesNotDependOnInfrastructure()
    {
        var referenced = typeof(ITimeLogService).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("ETimeSheet.Infrastructure", referenced);
    }

    [Fact]
    public void TheSharedLayerDependsOnNothingInThisSolution()
    {
        var referenced = typeof(Shared.Responses.ApiResponse<object>).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            referenced,
            name => name.StartsWith("ETimeSheet.", StringComparison.Ordinal));
    }

    [Fact]
    public void OnlyRepositoriesTakeADependencyOnTheDbContext()
    {
        var offenders = typeof(Context).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(TakesDbContextDependency)
            .Where(type => !IsPermittedDbContextConsumer(type))
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Only repositories, the seeder and the context itself may depend on " +
            $"Context. Offenders: {string.Join(", ", offenders)}.");
    }

    [Fact]
    public void ControllersDependOnlyOnServiceInterfaces()
    {
        var controllerTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(controllerTypes);

        foreach (var controllerType in controllerTypes)
        {
            var parameterTypes = controllerType
                .GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            foreach (var parameterType in parameterTypes)
            {
                Assert.True(
                    parameterType.IsInterface,
                    $"{controllerType.Name} injects the concrete type {parameterType.Name}. " +
                    "Controllers must depend on interfaces.");

                Assert.False(
                    IsRepositoryAbstraction(parameterType),
                    $"{controllerType.Name} injects {parameterType.Name}. Controllers must go " +
                    "through a service, never straight to a repository.");

                Assert.False(
                    typeof(Context).IsAssignableFrom(parameterType),
                    $"{controllerType.Name} injects the DbContext.");
            }
        }
    }

    private static bool TakesDbContextDependency(Type type) =>
        type.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => typeof(Context).IsAssignableFrom(parameter.ParameterType));

    private static bool IsPermittedDbContextConsumer(Type type) =>
        type.Name.EndsWith("Repository", StringComparison.Ordinal)
        || type.Name.EndsWith("Seeder", StringComparison.Ordinal);

    private static bool IsRepositoryAbstraction(Type type) =>
        type.Name.EndsWith("Repository", StringComparison.Ordinal);

    /// <summary>
    /// Builds a comparable signature. Interface and implementation members match
    /// exactly for an implicit implementation, so name plus parameter types is
    /// enough to detect a public method that was never added to the contract.
    /// </summary>
    private static string Signature(MethodInfo method) =>
        $"{method.Name}({string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name))})";
}
