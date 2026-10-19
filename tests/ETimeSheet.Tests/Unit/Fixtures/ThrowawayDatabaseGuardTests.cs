using ETimeSheet.Tests.Fixtures;

namespace ETimeSheet.Tests.Unit.Fixtures;

/// <summary>
/// The guard that keeps the integration suite off every real database.
/// <para>
/// A unit test on purpose: it needs no Docker, so it runs on every machine and
/// in every unit run - including the ones where the integration suite itself
/// cannot start, which is exactly when nobody would notice the guard had been
/// broken.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class ThrowawayDatabaseGuardTests
{
    /// <summary>The shape SqlServerFixture builds from a Testcontainers SQL Server.</summary>
    private const string Container =
        "Data Source=127.0.0.1,49153;Initial Catalog=ETimeSheetIntegrationTests;User ID=sa;" +
        "Password=generated-Pa55word;Connect Timeout=60;Trust Server Certificate=True";

    /// <summary>The live server's shape, as appsettings.json names it - with a made-up password.</summary>
    private const string Live =
        "Server=103.235.104.111,1433;Database=PPMUAT;User Id=solanki;Password=not-the-real-one;" +
        "TrustServerCertificate=True;MultipleActiveResultSets=True";

    [Fact]
    public void AcceptsTheContainersOwnConnection()
    {
        ThrowawayDatabaseGuard.Verify(Container, Container);
    }

    /// <summary>Keyword spelling and order are not a difference; the values are.</summary>
    [Fact]
    public void AcceptsTheSameConnectionWrittenWithOtherKeywords()
    {
        const string sameConnection =
            "Server=127.0.0.1,49153;Database=ETimeSheetIntegrationTests;User Id=sa;" +
            "Password=generated-Pa55word;Connection Timeout=60;TrustServerCertificate=True";

        ThrowawayDatabaseGuard.Verify(Container, sameConnection);
    }

    /// <summary>The case that matters most: appsettings.json winning over the test configuration.</summary>
    [Fact]
    public void RefusesTheLiveServer()
    {
        Assert.Throws<InvalidOperationException>(() => ThrowawayDatabaseGuard.Verify(Container, Live));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RefusesAMissingConnection(string? actual)
    {
        Assert.Throws<InvalidOperationException>(() => ThrowawayDatabaseGuard.Verify(Container, actual));
    }

    [Fact]
    public void RefusesAConnectionThatCannotBeParsed()
    {
        Assert.Throws<InvalidOperationException>(
            () => ThrowawayDatabaseGuard.Verify(Container, "this is not a connection string"));
    }

    /// <summary>Same container, another database - a developer's, say - is still refused.</summary>
    [Fact]
    public void RefusesAnotherDatabaseOnTheContainersServer()
    {
        var otherDatabase = Container.Replace("ETimeSheetIntegrationTests", "PPMUAT", StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() => ThrowawayDatabaseGuard.Verify(Container, otherDatabase));
    }

    /// <summary>
    /// Even when both sides agree, a server that is not on loopback is refused:
    /// were the fixture ever handed a real server's address, matching it would
    /// prove nothing.
    /// </summary>
    [Fact]
    public void RefusesARemoteServerEvenWhenBothSidesAgree()
    {
        var remote = Container.Replace("127.0.0.1,49153", "103.235.104.111,1433", StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() => ThrowawayDatabaseGuard.Verify(remote, remote));
    }

    /// <summary>And a real database name on loopback is refused for the same reason.</summary>
    [Fact]
    public void RefusesARealDatabaseNameEvenWhenBothSidesAgree()
    {
        var localPpmuat = Container.Replace("ETimeSheetIntegrationTests", "PPMUAT", StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(() => ThrowawayDatabaseGuard.Verify(localPpmuat, localPpmuat));
    }

    [Theory]
    [InlineData("localhost,49153")]
    [InlineData("tcp:127.0.0.1,49153")]
    [InlineData("LOCALHOST")]
    public void AcceptsEveryLoopbackSpelling(string dataSource)
    {
        var connection = Container.Replace("127.0.0.1,49153", dataSource, StringComparison.Ordinal);

        Assert.Null(ThrowawayDatabaseGuard.FindProblem(connection, connection));
    }

    /// <summary>The live connection string carries a password; the refusal must never repeat it.</summary>
    [Fact]
    public void NeverPutsAConnectionStringInTheMessage()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ThrowawayDatabaseGuard.Verify(Container, Live));

        Assert.DoesNotContain("not-the-real-one", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("generated-Pa55word", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("103.235.104.111", exception.Message, StringComparison.Ordinal);
    }
}
