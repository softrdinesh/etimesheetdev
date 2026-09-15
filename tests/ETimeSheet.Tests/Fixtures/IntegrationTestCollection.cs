namespace ETimeSheet.Tests.Fixtures;

/// <summary>
/// Groups every integration test class into one xUnit collection.
/// <para>
/// Two things follow from this. The SQL Server container is started once for the
/// whole run instead of once per test class, and the classes run sequentially
/// rather than in parallel - which they must, because they share one database
/// and reset it between tests.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server integration tests";
}
