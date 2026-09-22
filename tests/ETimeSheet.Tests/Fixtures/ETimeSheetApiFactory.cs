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
    /// Builds the container's schema by replaying the recorded SQL in
    /// <c>docs/database</c>.
    /// <para>
    /// This project is database-first: the real schema is changed by hand, by
    /// its owner, and this repository only records it. Nothing in the
    /// application - and nothing else in this test suite - creates, alters or
    /// drops schema anywhere. <b>This method is the sole exception</b>, and it
    /// is fenced in three ways: it runs only against the throwaway container
    /// (<see cref="AssertUsingThrowawayContainer"/>), the container is
    /// destroyed when the run ends, and
    /// <c>ArchitectureRuleTests.NothingButTheTestContainerFixtureExecutesSchema</c>
    /// fails the build if DDL appears anywhere else.
    /// </para>
    /// <para>
    /// Replaying the record rather than calling <c>EnsureCreated</c> is the
    /// point: the suite runs against the same DDL production runs - real column
    /// types, real lengths, real constraints - so a drift between the record
    /// and the EF mappings fails a test here instead of returning
    /// <c>Invalid column name</c> at runtime. <c>EnsureCreated</c> would build
    /// the container from the EF model, which can only ever agree with itself.
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
            await ExecuteScriptAsync(dbContext, batch);
        }
    }

    /// <summary>
    /// Sends one batch to SQL Server exactly as the file writes it.
    /// <para>
    /// Deliberately not <c>ExecuteSqlRawAsync</c>. "Raw" there means "not a
    /// LINQ query", not "not a format string": EF Core puts the text through
    /// <c>string.Format</c> before sending it, and it does so even when no
    /// parameters are supplied. One brace anywhere in the script is therefore
    /// enough to fail the entire run with <c>System.FormatException: Input
    /// string was not in a correct format.</c> before a byte reaches the
    /// server - and the recorded schema is hand-written SQL whose header
    /// comments document routes such as
    /// <c>get-all-employees-by-orgid/{orgID}</c>.
    /// </para>
    /// <para>
    /// These batches are DDL with nothing in them to parameterise; what they
    /// need is to be executed verbatim. A plain
    /// <see cref="System.Data.Common.DbCommand"/> does that, and
    /// <see cref="CurrentWeekStartAsync"/> already takes the same route.
    /// </para>
    /// </summary>
    private static async Task ExecuteScriptAsync(Context dbContext, string sql)
    {
        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();

        command.CommandText = sql;

        // Honours Database:CommandTimeoutSeconds, which the EF path would have
        // applied for us.
        if (dbContext.Database.GetCommandTimeout() is { } timeoutSeconds)
        {
            command.CommandTimeout = timeoutSeconds;
        }

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Reads the recorded schema from <c>docs/database</c> and splits each file
    /// on its <c>GO</c> separators.
    /// <para>
    /// <c>schema</c> and <c>procedures</c> only. <c>docs/database/data</c> is
    /// deliberately left out: those are one-off data scripts the database owner
    /// runs by hand against a real database, not part of an empty container's
    /// schema.
    /// </para>
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
    /// Inserts <c>dbo.Signup</c> rows directly, to arrange the people an
    /// organisation contains.
    /// <para>
    /// Raw SQL rather than EF Core, because <c>dbo.Signup</c> deliberately has
    /// no entity: the API never reads or writes it, and it is reached only
    /// through <c>dbo.spc_GetEmployeeListByPOrgID</c>. Adding an entity purely
    /// so a test could seed one would put a table in the application model that
    /// the application does not use.
    /// </para>
    /// <para>
    /// <c>IDENTITY_INSERT</c> is on because the test picks the user ids: they
    /// are the join key to <c>dbo.TimesheetMasterSetup</c> and <c>dbo.TimeLog</c>,
    /// so a generated one would leave the arranged rows pointing at nobody.
    /// </para>
    /// </summary>
    public async Task SeedEmployeesAsync(params EmployeeListTestData.SignupRow[] employees)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Context>();

        foreach (var employee in employees)
        {
            // isdelete is written explicitly rather than left to the column's
            // default. spc_GetEmployeeListByPOrgID filters on "isdelete = 0", so
            // a row that got the wrong value here would simply not come back and
            // the test would read as though the endpoint had lost it.
            await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                SET IDENTITY_INSERT dbo.Signup ON;
                INSERT INTO dbo.Signup (UserID, Name, Email, RoleID, OrganizationID, isdelete)
                VALUES ({employee.UserId}, {employee.Name}, {employee.Email},
                        {employee.RoleId}, {employee.OrganizationId}, {employee.IsDelete});
                SET IDENTITY_INSERT dbo.Signup OFF;");
        }
    }

    /// <summary>
    /// Monday of the current week <b>as SQL Server sees it</b>, worked out the
    /// way <c>spc_GetEmployeeListByPOrgID</c> does.
    /// <para>
    /// Asked of the database rather than computed from <c>DateTime.Today</c> on
    /// purpose. The procedure reads <c>GETDATE()</c>, which no substituted
    /// <see cref="IDateTimeProvider"/> can reach, and the container's timezone
    /// need not match the test machine's - so a host-side calculation would
    /// disagree with the procedure by a day near midnight, and by a whole week
    /// when that midnight is Sunday's. Arranging rows against the server's own
    /// answer removes both.
    /// </para>
    /// </summary>
    public async Task<DateTime> CurrentWeekStartAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Context>();

        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();

        // The procedure's own expression, character for character, so the two
        // cannot drift: '19000101' is a Monday, which is what makes the modulo
        // land on Monday.
        command.CommandText = @"
            DECLARE @Today DATE = CAST(GETDATE() AS DATE);
            SELECT DATEADD(DAY, -(DATEDIFF(DAY, '19000101', @Today) % 7), @Today);";

        return (DateTime)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Inserts timesheet setups directly, bypassing the API, to arrange a test.</summary>
    public async Task<IReadOnlyList<TimesheetMasterSetup>> SeedSetupsAsync(
        params TimesheetMasterSetup[] setups)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Context>();

        foreach (var setup in setups)
        {
            // Let the database assign identity so arranged ids cannot collide.
            setup.SetupId = 0;
            setup.CreateDate ??= Clock.UtcNow;
        }

        dbContext.TimesheetMasterSetup.AddRange(setups);
        await dbContext.SaveChangesAsync();

        return setups;
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
    /// Tables a test can seed that are <b>not</b> in the EF model, and so cannot
    /// be discovered from it.
    /// <para>
    /// <c>dbo.Signup</c> is here because the API reaches it only through
    /// <c>dbo.spc_GetEmployeeListByPOrgID</c> and has no entity for it - see
    /// <see cref="SeedEmployeesAsync"/>. Without this entry its rows would
    /// survive <see cref="ResetDatabaseAsync"/> and leak into the next test.
    /// </para>
    /// <para>
    /// This list is the exception, not the pattern: anything with an entity is
    /// still discovered from the model, so a new mapped table needs no change
    /// here. Add to it only when a test seeds a table the application does not
    /// model.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyList<string> TablesOutsideTheModel =
        new[] { "[dbo].[Signup]" };

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
            .Concat(TablesOutsideTheModel)
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
