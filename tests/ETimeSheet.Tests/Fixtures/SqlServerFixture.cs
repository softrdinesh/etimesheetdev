using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace ETimeSheet.Tests.Fixtures;

/// <summary>
/// Owns the disposable SQL Server used by the whole integration suite.
/// <para>
/// Testcontainers starts a real SQL Server in Docker on a random free port with
/// a generated password, applies the EF Core migrations to a database created
/// for this run, and destroys the container when the run ends. No test ever
/// touches a developer's local database or any deployed environment, and there
/// is nothing to clean up by hand.
/// </para>
/// <para>
/// The container is shared across every integration test class through
/// <see cref="IntegrationTestCollection"/>, because starting SQL Server is by
/// far the most expensive thing in the suite. Tests get isolation from
/// <see cref="ETimeSheetApiFactory.ResetDatabaseAsync"/> instead, which empties
/// the tables between tests.
/// </para>
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>
    /// Pinned image tag. A floating tag would let an image change turn a green
    /// suite red without a single line of code changing.
    /// <para>
    /// Override with <c>ETIMESHEET_TEST_SQL_IMAGE</c> - useful on Apple Silicon,
    /// where this amd64 image needs Rosetta emulation and some people prefer a
    /// natively built alternative.
    /// </para>
    /// </summary>
    private const string DefaultImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private const string ImageEnvironmentVariable = "ETIMESHEET_TEST_SQL_IMAGE";

    /// <summary>Database created inside the container. Never the server's <c>master</c>.</summary>
    private const string TestDatabaseName = "ETimeSheetIntegrationTests";

    private MsSqlContainer? _container;

    private ETimeSheetApiFactory? _factory;

    /// <summary>The API host wired to the container. Available once the fixture has initialised.</summary>
    public ETimeSheetApiFactory Factory =>
        _factory ?? throw new InvalidOperationException(
            "The SQL Server fixture has not finished starting.");

    public async Task InitializeAsync()
    {
        try
        {
            // Built here rather than in the constructor: Testcontainers resolves
            // the Docker endpoint while building, so a missing daemon would
            // otherwise throw before this guard could explain why.
            _container = new MsSqlBuilder()
                .WithImage(ResolveImage())
                // Removed as soon as the run finishes, including after a crash.
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync();
        }
        catch (Exception exception)
        {
            // The overwhelmingly common cause is "Docker is not running", and
            // the raw Testcontainers error does not say so.
            throw new InvalidOperationException(
                "Could not start the SQL Server test container.\n" +
                "Docker must be installed AND running before the integration tests can execute.\n" +
                "Run `./scripts/run-integration-tests.sh` instead - it starts Docker, pulls the " +
                "image and then runs this suite.\n" +
                "On Apple Silicon, also enable Docker Desktop > Settings > General > " +
                "\"Use Rosetta for x86/amd64 emulation\", because the SQL Server image is " +
                "amd64-only.\n" +
                $"Image: {ResolveImage()}",
                exception);
        }

        _factory = new ETimeSheetApiFactory(BuildConnectionString());

        await _factory.CreateSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private static string ResolveImage()
    {
        var configured = Environment.GetEnvironmentVariable(ImageEnvironmentVariable);

        return string.IsNullOrWhiteSpace(configured) ? DefaultImage : configured;
    }

    /// <summary>
    /// Retargets the container's connection string at a dedicated database.
    /// The container hands back a <c>master</c> connection; the migrations
    /// create the test database on first use.
    /// </summary>
    private string BuildConnectionString() =>
        new SqlConnectionStringBuilder(_container!.GetConnectionString())
        {
            InitialCatalog = TestDatabaseName,
            TrustServerCertificate = true,
            ConnectTimeout = 60
        }.ConnectionString;
}
