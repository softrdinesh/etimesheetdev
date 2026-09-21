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
    [InlineData(typeof(AdminService), typeof(IAdminService))]
    [InlineData(typeof(AdminRepository), typeof(IAdminRepository))]
    [InlineData(typeof(CountryRepository), typeof(ICountryRepository))]
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

    /// <summary>
    /// The database is database-first: its schema is changed by hand, by its
    /// owner, and this repository only records what they changed in
    /// <c>docs/database</c>. Nothing here may create, alter or drop anything.
    /// <para>
    /// The sole exception is
    /// <see cref="Fixtures.ETimeSheetApiFactory.CreateSchemaAsync"/>, which
    /// replays the recorded files into the throwaway Testcontainers SQL Server
    /// and is itself fenced by <c>AssertUsingThrowawayContainer</c>. This test
    /// is what stops that exception from widening: the day a second file starts
    /// issuing DDL, the build fails here rather than the day someone runs it
    /// against PPMUAT.
    /// </para>
    /// <para>
    /// A source scan, not reflection, because what matters is the SQL a file
    /// contains - which no amount of type inspection can see.
    /// </para>
    /// </summary>
    [Fact]
    public void NothingButTheTestContainerFixtureExecutesSchema()
    {
        var repositoryRoot = FindRepositoryRoot();

        var offenders = new List<string>();

        foreach (var file in CSharpSourceFiles(repositoryRoot))
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, file);

            if (MayDescribeSchemaOperations.Contains(relativePath))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            var found = SchemaOperations
                .Where(operation => text.Contains(operation, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (found.Length > 0)
            {
                offenders.Add($"{relativePath} ({string.Join(", ", found)})");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Schema is changed by hand in SQL Server and only recorded in docs/database - see " +
            "CLAUDE.md section 8. These files create, alter or drop it: " +
            $"{string.Join("; ", offenders)}. The only code permitted to do that is " +
            "ETimeSheetApiFactory.CreateSchemaAsync, against the throwaway test container.");
    }

    /// <summary>What "executes schema" looks like in C#, whether in EF Core or in SQL.</summary>
    private static readonly IReadOnlyList<string> SchemaOperations = new[]
    {
        "CREATE TABLE",
        "ALTER TABLE",
        "DROP TABLE",
        "CREATE OR ALTER",
        "EnsureCreated",
        "EnsureDeleted",
        ".Migrate(",
        "MigrateAsync",
        "IRelationalDatabaseCreator"
    };

    /// <summary>
    /// The two files that may contain those strings: the fixture, which is the
    /// documented exception, and this one, which cannot forbid a string without
    /// naming it.
    /// </summary>
    private static readonly IReadOnlySet<string> MayDescribeSchemaOperations =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine("tests", "ETimeSheet.Tests", "Fixtures", "ETimeSheetApiFactory.cs"),
            Path.Combine("tests", "ETimeSheet.Tests", "Unit", "Architecture", "ArchitectureRuleTests.cs")
        };

    /// <summary>
    /// Every hand-written <c>.cs</c> file in the solution. Generated output is
    /// skipped: <c>obj</c> holds assembly attributes nobody wrote, and <c>bin</c>
    /// holds copies that would be reported twice.
    /// </summary>
    private static IEnumerable<string> CSharpSourceFiles(string repositoryRoot) =>
        new[] { "src", "tests" }
            .Select(folder => Path.Combine(repositoryRoot, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                          StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                          StringComparison.Ordinal));

    /// <summary>
    /// Walks up from the test binaries to the folder holding the solution file.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ETimeSheet.sln")))
        {
            directory = directory.Parent;
        }

        Assert.True(
            directory is not null,
            $"Could not find ETimeSheet.sln above '{AppContext.BaseDirectory}'. This test reads the " +
            "solution's source files, so it has to run from inside the repository.");

        return directory!.FullName;
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
