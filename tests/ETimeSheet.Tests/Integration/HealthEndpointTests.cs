using System.Net;
using ETimeSheet.Tests.Fixtures;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// The health endpoints must stay reachable without authentication, which is
/// what makes them usable by a load balancer or orchestrator.
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class HealthEndpointTests : IntegrationTestBase
{
    public HealthEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_AreAnonymousAndHealthy(string path)
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Health_ReportsTheIndividualChecks()
    {
        var client = Factory.CreateClient();

        var body = await client.GetStringAsync("/health");

        Assert.Contains("\"self\"", body, StringComparison.Ordinal);
        Assert.Contains("\"database\"", body, StringComparison.Ordinal);
    }
}
