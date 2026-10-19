using Microsoft.Data.SqlClient;

namespace ETimeSheet.Tests.Fixtures;

/// <summary>
/// The one check that stands between the integration suite and a real
/// database.
/// <para>
/// The API project's <c>appsettings.json</c> names the live PPMUAT server, and
/// the test host loads it. The factory's in-memory connection string outranks
/// it, but that is a configuration ordering detail a future change could
/// quietly invert - and the reset before every test deletes every row of every
/// table. So every database touch in the suite is checked against this first,
/// and it fails closed.
/// </para>
/// <para>
/// A connection is accepted only when all three hold:
/// <list type="number">
/// <item>it is the very connection string the Testcontainers fixture produced;</item>
/// <item>its server is on loopback - the container is published on
///   <c>localhost</c>, a real server never is;</item>
/// <item>its database is <see cref="TestDatabaseName"/>, the one the fixture
///   creates and destroys.</item>
/// </list>
/// Any one alone could be fooled; all three together cannot be met by a real
/// database.
/// </para>
/// </summary>
public static class ThrowawayDatabaseGuard
{
    /// <summary>The only database name the integration suite may ever touch.</summary>
    public const string TestDatabaseName = "ETimeSheetIntegrationTests";

    private static readonly string[] LoopbackHosts = { "localhost", "127.0.0.1", "::1", "[::1]", "(local)", "." };

    /// <summary>
    /// Throws unless <paramref name="actual"/> - the connection the host is
    /// really about to use - is the throwaway container's.
    /// </summary>
    /// <param name="expected">The connection string the fixture built from the container.</param>
    /// <param name="actual">The connection string resolved inside the host.</param>
    public static void Verify(string expected, string? actual)
    {
        var problem = FindProblem(expected, actual);

        if (problem is null)
        {
            return;
        }

        // The connection strings are never put in the message: the live one
        // carries a password (CLAUDE.md §9).
        throw new InvalidOperationException(
            "Refusing to touch the database: " + problem + " Integration tests must only ever " +
            "run against the disposable SQL Server container that SqlServerFixture starts - " +
            "never a live, shared or developer database. Check that ETimeSheetApiFactory still " +
            "injects Database:ConnectionString and that nothing later in the configuration " +
            "chain overrides it.");
    }

    /// <summary>The reason a connection is refused, or <see langword="null"/> when it is safe.</summary>
    public static string? FindProblem(string expected, string? actual)
    {
        if (string.IsNullOrWhiteSpace(actual))
        {
            return "the host resolved no connection string at all.";
        }

        SqlConnectionStringBuilder expectedBuilder;
        SqlConnectionStringBuilder actualBuilder;

        try
        {
            expectedBuilder = new SqlConnectionStringBuilder(expected);
            actualBuilder = new SqlConnectionStringBuilder(actual);
        }
        catch (ArgumentException)
        {
            return "a connection string could not be parsed.";
        }

        // Compared as parsed keys rather than raw text, so the provider
        // re-ordering or re-casing keywords cannot cause a false alarm - but
        // every key has to match, password included.
        if (!string.Equals(expectedBuilder.ConnectionString, actualBuilder.ConnectionString, StringComparison.Ordinal))
        {
            return "the host resolved a connection string that is not the test container's.";
        }

        if (!IsLoopback(actualBuilder.DataSource))
        {
            return "the server is not on this machine's loopback address.";
        }

        if (!string.Equals(actualBuilder.InitialCatalog, TestDatabaseName, StringComparison.Ordinal))
        {
            return $"the database is not '{TestDatabaseName}'.";
        }

        return null;
    }

    /// <summary>
    /// True for <c>localhost,49153</c>, <c>tcp:127.0.0.1,49153</c> and the like:
    /// the host part only, with any protocol prefix, port or instance removed.
    /// </summary>
    private static bool IsLoopback(string dataSource)
    {
        var host = dataSource.Trim();

        var protocol = host.IndexOf(':');
        if (protocol > 0 && host[..protocol] is "tcp" or "np" or "lpc")
        {
            host = host[(protocol + 1)..];
        }

        host = host.Split(',')[0].Split('\\')[0].Trim();

        return LoopbackHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
    }
}
