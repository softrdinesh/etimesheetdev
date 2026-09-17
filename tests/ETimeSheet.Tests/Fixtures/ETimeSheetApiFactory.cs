using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Infrastructure.Data;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ETimeSheet.Shared.Configuration;

namespace ETimeSheet.Tests.Fixtures;

/// <summary>
/// Boots the real API pipeline - middleware, authentication, validation,
/// controllers, EF Core - against the throwaway SQL Server container owned by
/// <see cref="SqlServerFixture"/>.
/// <para>
/// The connection string is injected through configuration rather than by
/// swapping the provider, so the production registration path in
/// <c>AddInfrastructureServices</c> runs unchanged: same SQL Server provider,
/// same retry policy, same audit interceptor. The only substitution is the
/// clock, which tests must be able to freeze.
/// </para>
/// </summary>
public class ETimeSheetApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Signing key used only by the test host. Long enough to satisfy the HMAC-SHA256 minimum.</summary>
    public const string TestSigningKey = "integration-test-signing-key-do-not-use-anywhere-else";

    public const string TestIssuer = "ETimeSheet.Tests";
    public const string TestAudience = "ETimeSheet.Tests.Client";

    private readonly string _connectionString;

    public ETimeSheetApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>The clock the host runs on, frozen so business rules are deterministic.</summary>
    public FixedDateTimeProvider Clock { get; } = new(TimeLogTestData.Now);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development keeps HTTPS redirection out of the pipeline, which the
        // in-memory test client cannot follow.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = TestSigningKey,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:ExpiryMinutes"] = "60",
                ["Jwt:ClockSkewSeconds"] = "0",

                // Points at the disposable container, never at a developer's or
                // a deployed database.
                ["Database:ConnectionString"] = _connectionString,
                ["Database:MaxRetryCount"] = "3",
                ["Database:CommandTimeoutSeconds"] = "60",
                // The fixture migrates once, up front. Letting the host do it
                // would repeat the work for every factory that is built.

                ["Cache:Enabled"] = "true",
                ["Cache:DefaultExpirationSeconds"] = "60",

                ["TimeLog:MinEntryMinutes"] = "1",
                ["TimeLog:MaxEntryMinutes"] = "720",
                ["TimeLog:MaxMinutesPerDay"] = "1440",
                ["TimeLog:EditableWindowDays"] = "30",

                ["Cors:AllowedOrigins:0"] = "https://app.test",

                // EF Core command logging is noisy enough to bury assertion
                // failures in the test output.
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // A frozen clock makes "not in the future" and "within the editable
            // window" mean the same thing in every run.
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(Clock);
        });
    }

    /// <summary>
    /// Returns a client that presents a valid bearer token for the given user
    /// and role. Tokens are minted with the same key the host validates against.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(int userId, int roleId)
    {
        var client = CreateClient();

        client.DefaultRequestHeaders.Authorization = TestTokenFactory.BearerHeader(userId, roleId);

        return client;
    }

    /// <summary>
    /// Creates the schema by executing the checked-in <c>Schema/Schema.sql</c>.
    /// <para>
    /// This project is database-first, so there are no migrations to apply. The
    /// script is the repository's copy of the real schema, which means the suite
    /// still runs against genuine SQL Server DDL - real column types, real
    /// lengths, real constraints - rather than a model-shaped approximation
    /// produced by <c>EnsureCreated</c>.
    /// </para>
    /// </summary>
    public async Task CreateSchemaAsync()
    {
        await using var scope = Services.CreateAsyncScope();

        AssertUsingThrowawayContainer(scope.ServiceProvider);

        var dbContext = scope.ServiceProvider.GetRequiredService<Context>();

        // Creates the empty database only - no tables. EnsureCreated would
        // build them from the EF model instead of from the script, which is
        // precisely what this approach is avoiding.
        var creator = dbContext.GetService<IRelationalDatabaseCreator>();

        if (!await creator.ExistsAsync())
        {
            await creator.CreateAsync();
        }

        foreach (var batch in ReadSchemaBatches())
        {
            await dbContext.Database.ExecuteSqlRawAsync(batch);
        }
    }

    /// <summary>
    /// Reads the recorded schema from <c>docs/database</c> and splits each file
    /// on its <c>GO</c> separators.
    /// <para>
    /// Tables first, then procedures, because a procedure will not compile
    /// against a table that does not exist yet. <c>GO</c> is a batch separator
    /// understood by sqlcmd and SSMS, not a T-SQL statement: sending it to the
    /// server as part of a command is a syntax error, so each file has to be
    /// executed one batch at a time.
    /// </para>
    /// </summary>
    private static IEnumerable<string> ReadSchemaBatches()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Database");

        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException(
                $"The recorded database schema was not found at '{root}'. It is copied from " +
                "docs/database by the <None Include=\"..\\..\\docs\\database\\**\\*.sql\" ... /> " +
                "item in ETimeSheet.Tests.csproj.");
        }

        var files = OrderedSqlFiles(root, "schema")
            .Concat(OrderedSqlFiles(root, "procedures"))
            .ToArray();

        if (files.Length == 0)
        {
            throw new InvalidOperationException(
                $"No .sql files were found under '{root}'.");
        }

        return files
            .SelectMany(file => File.ReadAllText(file).Split("\nGO", StringSplitOptions.RemoveEmptyEntries))
            .Select(batch => batch.Trim())
            .Where(batch => batch.Length > 0)
            .ToArray();
    }

    /// <summary>Sorted so the order a container is built in is reproducible.</summary>
    private static IEnumerable<string> OrderedSqlFiles(string root, string folder)
    {
        var path = Path.Combine(root, folder);

        return Directory.Exists(path)
            ? Directory.GetFiles(path, "*.sql").OrderBy(file => file, StringComparer.Ordinal)
            : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Refuses to run if the host resolved any connection string other than the
    /// throwaway container's.
    /// <para>
    /// The API project's <c>appsettings.json</c> is on the configuration chain
    /// and names a developer's local SQL Server. The
    /// in-memory source added above outranks it, but "outranks it" is an
    /// ordering detail that a future change could quietly invert - and the
    /// failure mode would be a test suite deleting rows from someone's real
    /// database. This turns that silent risk into a loud, immediate stop.
    /// </para>
    /// </summary>
    private void AssertUsingThrowawayContainer(IServiceProvider serviceProvider)
    {
        var resolved = serviceProvider
            .GetRequiredService<IOptions<DatabaseSettings>>()
            .Value
            .ConnectionString;

        if (string.Equals(resolved, _connectionString, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            "Refusing to run the integration tests: the host resolved a connection string " +
            "that is not the disposable test container's. Integration tests must never run " +
            "against a developer, shared or deployed database. Check that " +
            "ETimeSheetApiFactory still injects Database:ConnectionString and that nothing " +
            "later in the configuration chain overrides it.");
    }

    /// <summary>
    /// Empties every mapped table and resets identity, so each test starts from
    /// a known state without paying to recreate the database.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Context>();

        await dbContext.Database.ExecuteSqlRawAsync(BuildResetScript(dbContext));
    }

    /// <summary>Inserts entries directly, bypassing the API, to arrange a test.</summary>
    public async Task<IReadOnlyList<TimeLog>> SeedAsync(params TimeLog[] timeLogs)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Context>();

        foreach (var timeLog in timeLogs)
        {
            // Let the database assign identity so arranged ids cannot collide.
            timeLog.SheetId = 0;
            timeLog.CreateDate = Clock.UtcNow;
        }

        dbContext.TimeLog.AddRange(timeLogs);
        await dbContext.SaveChangesAsync();

        return timeLogs;
    }

    /// <summary>
    /// Marks an entry deleted directly in the database.
    /// <para>
    /// The API is read-only, so there is no endpoint that can arrange this. The
    /// row still has to be reachable in order to prove the global query filter
    /// hides it from the read path.
    /// </para>
    /// </summary>
    public async Task SoftDeleteAsync(int timeLogId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Context>();

        var timeLog = await dbContext.TimeLog
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(entry => entry.SheetId == timeLogId)
            ?? throw new InvalidOperationException($"Time log {timeLogId} was not seeded.");

        // IsDeleted is what the global query filter reads; DeleteDate is the
        // accompanying audit detail.
        timeLog.IsDeleted = 1;
        timeLog.DeleteDate = Clock.UtcNow;

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Lookup tables, whose rows are fixed reference data inserted by the schema
    /// script rather than arranged by a test.
    /// <para>
    /// They are excluded from the reset below on purpose: emptying one would
    /// delete seed data that nothing puts back, so the first test to run would
    /// pass and every test after it would face an empty lookup.
    /// </para>
    /// </summary>
    private static readonly IReadOnlySet<string> LookupTables =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DayMaster" };

    /// <summary>
    /// Builds the clean-up script from the EF model, so a new entity is covered
    /// the moment it is mapped - nobody has to remember to update this, except
    /// to add a new lookup table to <see cref="LookupTables"/>.
    /// <para>
    /// Constraints are disabled around the deletes so that tables can be emptied
    /// without working out a topological order once foreign keys appear.
    /// </para>
    /// </summary>
    private static string BuildResetScript(Context dbContext)
    {
        // Each part is bracketed separately: "[dbo].[TimeLogs]" is a two-part
        // name, while "[dbo.TimeLogs]" would be one identifier containing a dot.
        var tables = dbContext.Model
            .GetEntityTypes()
            .Where(entityType => entityType.GetTableName() is not null)
            .Where(entityType => !LookupTables.Contains(entityType.GetTableName()!))
            .Select(entityType => $"[{entityType.GetSchema() ?? "dbo"}].[{entityType.GetTableName()}]")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (tables.Length == 0)
        {
            return "SELECT 1;";
        }

        var script = new System.Text.StringBuilder();

        foreach (var table in tables)
        {
            script.AppendLine($"ALTER TABLE {table} NOCHECK CONSTRAINT ALL;");
        }

        foreach (var table in tables)
        {
            script.AppendLine($"DELETE FROM {table};");

            // DBCC CHECKIDENT fails on a table without an identity column, so
            // only reseed the ones that have one.
            script.AppendLine(
                $"IF OBJECTPROPERTY(OBJECT_ID('{table}'), 'TableHasIdentity') = 1 " +
                $"DBCC CHECKIDENT('{table}', RESEED, 0);");
        }

        foreach (var table in tables)
        {
            script.AppendLine($"ALTER TABLE {table} WITH CHECK CHECK CONSTRAINT ALL;");
        }

        return script.ToString();
    }
}
