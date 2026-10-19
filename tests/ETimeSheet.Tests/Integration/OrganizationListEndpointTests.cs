using System.Net;
using ETimeSheet.Application.Models;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// <c>GET /api/v1/MasterAdmin/get-all-organization</c>, end to end against a
/// real SQL Server: the recorded <c>dbo.spc_GetOrganizationList</c>, its keyless
/// result mapping, the real middleware.
/// <para>
/// The other Master Admin endpoints are covered by
/// <see cref="MasterAdminEndpointTests"/>.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class OrganizationListEndpointTests : IntegrationTestBase
{
    private const string Url = "/api/v1/MasterAdmin/get-all-organization";

    public OrganizationListEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // [AllowAnonymous] while authentication is off.
    private HttpClient Client() => Factory.CreateClient();

    private async Task<IReadOnlyList<OrganizationResponse>> ListAsync()
    {
        var response = await Client().GetAsync(Url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<List<OrganizationResponse>>();
    }

    [Fact]
    public async Task NoOrganizationsIsAnEmptyListNotA404()
    {
        var response = await Client().GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<List<OrganizationResponse>>();
        Assert.True(envelope.Success);
        Assert.NotNull(envelope.Data);
        Assert.Empty(envelope.Data!);
    }

    [Fact]
    public async Task ReturnsEachOrganizationsIdNameAndCountry()
    {
        var seeded = await Factory.SeedOrganizationsAsync(
            OrganizationTestData.Row("Acme Consulting", OrganizationTestData.UnitedStatesCountryId));

        var organization = Assert.Single(await ListAsync());

        Assert.Equal(seeded[0].OrganizationId, organization.OrganizationId);
        Assert.Equal("Acme Consulting", organization.OrganizationName);
        Assert.Equal(OrganizationTestData.UnitedStatesCountryId, organization.CountryId);
    }

    [Fact]
    public async Task IsOrderedByName()
    {
        await Factory.SeedOrganizationsAsync(
            OrganizationTestData.Row("Gamma Ltd"),
            OrganizationTestData.Row("Alpha Ltd"),
            OrganizationTestData.Row("Beta Ltd"));

        Assert.Equal(
            new[] { "Alpha Ltd", "Beta Ltd", "Gamma Ltd" },
            (await ListAsync()).Select(organization => organization.OrganizationName));
    }

    /// <summary>
    /// Two organisations with the same name come back in id order, so the list
    /// is stable between calls.
    /// </summary>
    [Fact]
    public async Task OrganizationsWithTheSameNameAreOrderedById()
    {
        var seeded = await Factory.SeedOrganizationsAsync(
            OrganizationTestData.Row("Twin Corp", OrganizationTestData.IndiaCountryId),
            OrganizationTestData.Row("Twin Corp", OrganizationTestData.UnitedStatesCountryId));

        var expected = seeded.Select(organization => organization.OrganizationId).OrderBy(id => id);

        Assert.Equal(expected, (await ListAsync()).Select(organization => organization.OrganizationId));
    }

    /// <summary>The procedure filters <c>OrganizationName IS NOT NULL</c>.</summary>
    [Fact]
    public async Task LeavesOutOrganizationsWithNoName()
    {
        await Factory.SeedOrganizationsAsync(
            OrganizationTestData.Row("Named Org"),
            OrganizationTestData.Row(null));

        var organization = Assert.Single(await ListAsync());
        Assert.Equal("Named Org", organization.OrganizationName);
    }

    /// <summary>
    /// <c>CountryID</c> is nullable on the table; a null comes back as a
    /// present <c>countryId: null</c> key, never an omitted one (CLAUDE.md §5).
    /// </summary>
    [Fact]
    public async Task ANullCountryIsSentAsAnExplicitNull()
    {
        await Factory.SeedOrganizationsAsync(OrganizationTestData.Row("No Country Org", countryId: null));

        var response = await Client().GetAsync(Url);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"countryId\":null", json, StringComparison.Ordinal);
        Assert.Null(Assert.Single(await ListAsync()).CountryId);
    }
}
