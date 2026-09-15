using System.Net;
using System.Net.Http.Headers;
using ETimeSheet.Shared.Enums;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// Proves that the JWT bearer configuration really validates what it claims to:
/// presence, signature, issuer and lifetime.
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class AuthenticationTests : IntegrationTestBase
{
    public AuthenticationTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task WithoutAToken_TheEndpointIsUnauthorized()
    {
        var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/timelog");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithAValidToken_TheEndpointSucceeds()
    {
        var client = Factory.CreateAuthenticatedClient(
            TimeLogTestData.EmployeeUserId,
            (int)RoleType.Employee);

        var response = await client.GetAsync("/api/v1/timelog");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WithAMalformedToken_TheEndpointIsUnauthorized()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this-is-not-a-jwt");

        var response = await client.GetAsync("/api/v1/timelog");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithATokenSignedByTheWrongKey_TheEndpointIsUnauthorized()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenFactory.CreateTokenWithWrongKey(
                TimeLogTestData.EmployeeUserId,
                (int)RoleType.Employee));

        var response = await client.GetAsync("/api/v1/timelog");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithAnUnexpectedIssuer_TheEndpointIsUnauthorized()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenFactory.CreateTokenWithWrongIssuer(
                TimeLogTestData.EmployeeUserId,
                (int)RoleType.Employee));

        var response = await client.GetAsync("/api/v1/timelog");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithAnExpiredToken_TheEndpointIsUnauthorized()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenFactory.CreateExpiredToken(
                TimeLogTestData.EmployeeUserId,
                (int)RoleType.Employee));

        var response = await client.GetAsync("/api/v1/timelog");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
