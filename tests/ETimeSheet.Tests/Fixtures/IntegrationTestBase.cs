using ETimeSheet.Shared.Enums;
using ETimeSheet.Tests.TestData;

namespace ETimeSheet.Tests.Fixtures;

/// <summary>
/// Base class for integration tests: exposes the shared host and gives every
/// test a clean database.
/// <para>
/// xUnit creates a new instance per test, so <see cref="InitializeAsync"/> runs
/// before each one - which is what makes the shared container safe to reuse.
/// </para>
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected IntegrationTestBase(SqlServerFixture fixture)
    {
        Factory = fixture.Factory;
    }

    protected ETimeSheetApiFactory Factory { get; }

    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>A client authenticated as a plain employee, holding no permissions.</summary>
    protected HttpClient EmployeeClient() => Factory.CreateAuthenticatedClient(
        TimeLogTestData.EmployeeUserId,
        (int)RoleType.Employee);

    /// <summary>A client authenticated as a manager, who may view all entries and review them.</summary>
    protected HttpClient ManagerClient() => Factory.CreateAuthenticatedClient(
        TimeLogTestData.ManagerUserId,
        (int)RoleType.Manager);
}
