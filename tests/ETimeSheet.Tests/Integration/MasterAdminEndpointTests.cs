using System.Net;
using System.Net.Http.Json;
using ETimeSheet.Application.Models;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// The Master Admin client setup endpoints, end to end against a real SQL
/// Server: the recorded <c>spc_GetClientListForMasterAdmin</c> and
/// <c>spc_AddEditClientSetup</c>, the 248 currency rows from the recorded
/// <c>dbo.CountryCurrency_Lookup</c> script, the real middleware.
/// <para>
/// This is where the parts that live in SQL are proven: the <c>CL0001</c>
/// sequence and its lock, the currency derived from the country, and the
/// soft-delete filter in both procedures.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class MasterAdminEndpointTests : IntegrationTestBase
{
    // Every seeded client belongs to MasterAdminTestData.OrganizationId.
    private static readonly string ListUrl = $"/api/v1/MasterAdmin/get-client-list/{MasterAdminTestData.OrganizationId}";
    private const string SaveUrl = "/api/v1/MasterAdmin/add-or-edit-client-setup";
    private const string RemoveUrl = "/api/v1/MasterAdmin/remove-client-setup";

    public MasterAdminEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // The endpoints are [AllowAnonymous] while authentication is off, so an
    // unauthenticated client is the honest way to call them.
    private HttpClient Client() => Factory.CreateClient();

    private async Task<IReadOnlyList<ClientResponse>> ListAsync()
    {
        var response = await Client().GetAsync(ListUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<List<ClientResponse>>();
    }

    /// <summary>
    /// Posts a save the way a browser does: <c>multipart/form-data</c>, one
    /// part per field. A JSON body is refused with 415 since the endpoint
    /// gained its logo upload. No <c>clientLogo</c> part is sent, so these
    /// tests never reach the external document service.
    /// </summary>
    private Task<HttpResponseMessage> PostSaveFormAsync(ClientSaveRequest request)
    {
        var form = new MultipartFormDataContent();

        void Add(string name, object? value)
        {
            if (value is not null)
            {
                form.Add(new StringContent(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!), name);
            }
        }

        Add(nameof(ClientSaveRequest.ClientId), request.ClientId);
        Add(nameof(ClientSaveRequest.ClientName), request.ClientName);
        Add(nameof(ClientSaveRequest.BillingEmail), request.BillingEmail);
        Add(nameof(ClientSaveRequest.PhoneNumber), request.PhoneNumber);
        Add(nameof(ClientSaveRequest.AlternatePhoneNumber), request.AlternatePhoneNumber);
        Add(nameof(ClientSaveRequest.AlternateEmail), request.AlternateEmail);
        Add(nameof(ClientSaveRequest.BillingAddress), request.BillingAddress);
        Add(nameof(ClientSaveRequest.PaymentTermsDays), request.PaymentTermsDays);
        Add(nameof(ClientSaveRequest.CountryId), request.CountryId);
        Add(nameof(ClientSaveRequest.TaxIdentifier), request.TaxIdentifier);

        return Client().PostAsync(SaveUrl, form);
    }

    private async Task<ClientResponse> SaveAsync(ClientSaveRequest request)
    {
        var response = await PostSaveFormAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<ClientResponse>();
    }

    // ================================================================ list

    [Fact]
    public async Task ListIsEmptyWhenThereAreNoClients()
    {
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task ListLeavesOutDeletedClients()
    {
        await Factory.SeedClientsAsync(
            MasterAdminTestData.Client(1, "CL0001", "Live Client"),
            MasterAdminTestData.Client(2, "CL0002", "Gone Client", isDeleted: true));

        var client = Assert.Single(await ListAsync());
        Assert.Equal("CL0001", client.ClientCode);
    }

    [Fact]
    public async Task ListIsOrderedByName()
    {
        await Factory.SeedClientsAsync(
            MasterAdminTestData.Client(1, "CL0001", "Zeta"),
            MasterAdminTestData.Client(2, "CL0002", "Alpha"),
            MasterAdminTestData.Client(3, "CL0003", "Mid"));

        Assert.Equal(new[] { "Alpha", "Mid", "Zeta" }, (await ListAsync()).Select(client => client.ClientName));
    }

    [Fact]
    public async Task ListJoinsTheCurrencyNameAndSymbol()
    {
        await Factory.SeedClientsAsync(
            MasterAdminTestData.Client(
                1, "CL0001",
                countryId: MasterAdminTestData.UnitedStatesCountryId,
                currencyId: MasterAdminTestData.UnitedStatesCurrencyId));

        var client = Assert.Single(await ListAsync());

        Assert.Equal(MasterAdminTestData.UnitedStatesCurrencyId, client.CurrencyId);
        Assert.Equal("United States Dollar", client.CurrencyName);
        Assert.Equal("$", client.CurrencySymbol);
    }

    /// <summary>
    /// A client saved before currencies were derived may hold an id with no
    /// currency row. The LEFT JOIN keeps it listed, with a null name and symbol.
    /// </summary>
    [Fact]
    public async Task ListStillShowsAClientWhoseCurrencyIdNamesNoRow()
    {
        await Factory.SeedClientsAsync(MasterAdminTestData.Client(1, "CL0001", currencyId: 99999));

        var client = Assert.Single(await ListAsync());

        Assert.Equal(99999, client.CurrencyId);
        Assert.Null(client.CurrencyName);
        Assert.Null(client.CurrencySymbol);
    }

    /// <summary>The response keys never depend on the data - CLAUDE.md §5.</summary>
    [Fact]
    public async Task ListNeverSendsTheDeleteColumns()
    {
        await Factory.SeedClientsAsync(MasterAdminTestData.Client(1, "CL0001"));

        var json = await (await Client().GetAsync(ListUrl)).Content.ReadAsStringAsync();

        Assert.DoesNotContain("isDeleted", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deleteDate", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"currencyName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"currencySymbol\"", json, StringComparison.Ordinal);
    }

    // ================================================================ add

    [Fact]
    public async Task TheFirstClientIsCL0001()
    {
        var saved = await SaveAsync(MasterAdminTestData.ValidRequest());

        Assert.Equal("CL0001", saved.ClientCode);
    }

    [Fact]
    public async Task EachNewClientTakesTheNextCode()
    {
        var first = await SaveAsync(MasterAdminTestData.ValidRequest(clientName: "First"));
        var second = await SaveAsync(MasterAdminTestData.ValidRequest(clientName: "Second", clientId: 0));
        var third = await SaveAsync(MasterAdminTestData.ValidRequest(clientName: "Third"));

        Assert.Equal(new[] { "CL0001", "CL0002", "CL0003" }, new[] { first.ClientCode, second.ClientCode, third.ClientCode });
        Assert.Equal(3, (await ListAsync()).Count);
    }

    /// <summary>A deleted client has still spent its code, so it is never reissued.</summary>
    [Fact]
    public async Task TheSequenceCountsDeletedClients()
    {
        await Factory.SeedClientsAsync(
            MasterAdminTestData.Client(1, "CL0001"),
            MasterAdminTestData.Client(2, "CL0007", isDeleted: true));

        var saved = await SaveAsync(MasterAdminTestData.ValidRequest());

        Assert.Equal("CL0008", saved.ClientCode);
    }

    /// <summary>
    /// "Highest" is longest first: CL10000 follows CL9999, and a plain text
    /// comparison would call CL9999 the larger.
    /// </summary>
    [Fact]
    public async Task TheSequenceGrowsPastFourDigits()
    {
        await Factory.SeedClientsAsync(MasterAdminTestData.Client(1, "CL9999"));

        var afterNineNineNineNine = await SaveAsync(MasterAdminTestData.ValidRequest());
        var afterThat = await SaveAsync(MasterAdminTestData.ValidRequest());

        Assert.Equal("CL10000", afterNineNineNineNine.ClientCode);
        Assert.Equal("CL10001", afterThat.ClientCode);
    }

    /// <summary>A hand-entered code that is not CL + digits names no place in the sequence.</summary>
    [Fact]
    public async Task CodesThatAreNotClPlusDigitsAreIgnored()
    {
        await Factory.SeedClientsAsync(
            MasterAdminTestData.Client(1, "CL0003"),
            MasterAdminTestData.Client(2, "CLX999"),
            MasterAdminTestData.Client(3, "ZZ9999"));

        var saved = await SaveAsync(MasterAdminTestData.ValidRequest());

        Assert.Equal("CL0004", saved.ClientCode);
    }

    /// <summary>
    /// The lock in spc_AddEditClientSetup is what this proves: adds racing each
    /// other each read the code the previous one wrote, so none is shared.
    /// </summary>
    [Fact]
    public async Task ConcurrentAddsNeverShareACode()
    {
        const int adds = 10;

        var saved = await Task.WhenAll(Enumerable
            .Range(1, adds)
            .Select(n => SaveAsync(MasterAdminTestData.ValidRequest(clientName: $"Racer {n}"))));

        var codes = saved.Select(client => client.ClientCode).OrderBy(code => code).ToArray();

        Assert.Equal(adds, codes.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, adds).Select(n => $"CL{n:D4}"), codes);
    }

    [Fact]
    public async Task AddStoresEveryFieldItWasSent()
    {
        var request = MasterAdminTestData.ValidRequest();

        var saved = await SaveAsync(request);
        var row = await Factory.FindClientAsync(saved.ClientId);

        Assert.NotNull(row);
        Assert.Equal(request.ClientName, row!.ClientName);
        Assert.Null(row.Avatar); // no logo sent
        Assert.Equal(request.BillingEmail, row.BillingEmail);
        Assert.Equal(request.PhoneNumber, row.PhoneNumber);
        Assert.Equal(request.AlternatePhoneNumber, row.AlternatePhoneNumber);
        Assert.Equal(request.AlternateEmail, row.AlternateEmail);
        Assert.Equal(request.BillingAddress, row.BillingAddress);
        Assert.Equal(request.PaymentTermsDays, row.PaymentTermsDays);
        Assert.Equal(request.CountryId, row.CountryId);
        Assert.Equal(request.TaxIdentifier, row.TaxIdentifier);
        Assert.False(row.IsDeleted);
        Assert.Null(row.DeleteDate);
    }

    [Fact]
    public async Task AddDerivesTheCurrencyFromTheCountry()
    {
        var saved = await SaveAsync(MasterAdminTestData.ValidRequest(countryId: MasterAdminTestData.IndiaCountryId));

        Assert.Equal(MasterAdminTestData.IndiaCurrencyId, saved.CurrencyId);
        Assert.Equal("Indian Rupee", saved.CurrencyName);
        Assert.Equal("₹", saved.CurrencySymbol);

        // And it is what was stored in Currencycode, not only what was answered.
        Assert.Equal(MasterAdminTestData.IndiaCurrencyId, (await Factory.FindClientAsync(saved.ClientId))!.CurrencyCode);
    }

    [Fact]
    public async Task AddingAgainstACountryWithNoCurrencyIsABadRequestAndSavesNothing()
    {
        var response = await PostSaveFormAsync(
            MasterAdminTestData.ValidRequest(countryId: MasterAdminTestData.CountryWithoutCurrencyId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<ClientResponse>();
        Assert.Contains(envelope.Errors, error => error.StartsWith("CountryId:", StringComparison.Ordinal));
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task AMalformedPayloadIsABadRequestNamingTheField()
    {
        var request = MasterAdminTestData.ValidRequest();
        request.ClientName = "";
        request.BillingEmail = "not-an-email";

        var response = await PostSaveFormAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<ClientResponse>();
        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors, error => error.Contains("ClientName", StringComparison.Ordinal));
        Assert.Contains(envelope.Errors, error => error.Contains("BillingEmail", StringComparison.Ordinal));
        Assert.Empty(await ListAsync());
    }

    // ================================================================ edit

    [Fact]
    public async Task EditUpdatesTheClientAndKeepsItsCode()
    {
        await Factory.SeedClientsAsync(MasterAdminTestData.Client(5, "CL0005", "Old Name"));

        var saved = await SaveAsync(MasterAdminTestData.ValidRequest(clientId: 5, clientName: "New Name"));

        Assert.Equal(5, saved.ClientId);
        Assert.Equal("CL0005", saved.ClientCode);
        Assert.Equal("New Name", saved.ClientName);

        var row = await Factory.FindClientAsync(5);
        Assert.Equal("New Name", row!.ClientName);
        Assert.Equal("CL0005", row.ClientCode);
    }

    [Fact]
    public async Task EditDoesNotAddARow()
    {
        await Factory.SeedClientsAsync(MasterAdminTestData.Client(5, "CL0005"));

        await SaveAsync(MasterAdminTestData.ValidRequest(clientId: 5));

        Assert.Single(await ListAsync());
    }

    /// <summary>The currency is re-derived on every save, so moving a client moves its currency.</summary>
    [Fact]
    public async Task ChangingTheCountryChangesTheCurrency()
    {
        await Factory.SeedClientsAsync(MasterAdminTestData.Client(5, "CL0005"));

        var saved = await SaveAsync(
            MasterAdminTestData.ValidRequest(clientId: 5, countryId: MasterAdminTestData.UnitedStatesCountryId));

        Assert.Equal(MasterAdminTestData.UnitedStatesCurrencyId, saved.CurrencyId);
        Assert.Equal("United States Dollar", saved.CurrencyName);
    }

    [Fact]
    public async Task EditingAClientThatDoesNotExistIsNotFound()
    {
        var response = await PostSaveFormAsync(MasterAdminTestData.ValidRequest(clientId: 404));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task EditingADeletedClientIsNotFoundAndLeavesItDeleted()
    {
        await Factory.SeedClientsAsync(MasterAdminTestData.Client(5, "CL0005", "Removed", isDeleted: true));

        var response = await PostSaveFormAsync(
            MasterAdminTestData.ValidRequest(clientId: 5, clientName: "Revived?"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var row = await Factory.FindClientAsync(5);
        Assert.True(row!.IsDeleted);
        Assert.Equal("Removed", row.ClientName);
    }

    // ================================================================ remove

    [Fact]
    public async Task RemoveSoftDeletesTheClient()
    {
        await Factory.SeedClientsAsync(MasterAdminTestData.Client(5, "CL0005"));

        var response = await Client().PostAsJsonAsync(RemoveUrl, new ClientDeleteRequest { ClientId = 5 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Still in the table - marked, stamped with the frozen clock, not deleted.
        var row = await Factory.FindClientAsync(5);
        Assert.NotNull(row);
        Assert.True(row!.IsDeleted);
        Assert.Equal(Factory.Clock.UtcNow, row.DeleteDate);
    }

    [Fact]
    public async Task ARemovedClientLeavesTheList()
    {
        await Factory.SeedClientsAsync(
            MasterAdminTestData.Client(5, "CL0005", "Stays"),
            MasterAdminTestData.Client(6, "CL0006", "Goes"));

        await Client().PostAsJsonAsync(RemoveUrl, new ClientDeleteRequest { ClientId = 6 });

        var client = Assert.Single(await ListAsync());
        Assert.Equal("Stays", client.ClientName);
    }

    [Fact]
    public async Task RemovingTwiceIsNotFoundAndKeepsTheFirstDeleteDate()
    {
        await Factory.SeedClientsAsync(MasterAdminTestData.Client(5, "CL0005", isDeleted: true));
        var before = (await Factory.FindClientAsync(5))!.DeleteDate;

        var response = await Client().PostAsJsonAsync(RemoveUrl, new ClientDeleteRequest { ClientId = 5 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, (await Factory.FindClientAsync(5))!.DeleteDate);
    }

    [Fact]
    public async Task RemovingAClientThatDoesNotExistIsNotFound()
    {
        var response = await Client().PostAsJsonAsync(RemoveUrl, new ClientDeleteRequest { ClientId = 404 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RemoveRejectsAClientIdThatIsNotPositive(int clientId)
    {
        var response = await Client().PostAsJsonAsync(RemoveUrl, new ClientDeleteRequest { ClientId = clientId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>A removed client's code stays spent.</summary>
    [Fact]
    public async Task ARemovedClientsCodeIsNotReissued()
    {
        await Factory.SeedClientsAsync(MasterAdminTestData.Client(5, "CL0005"));
        await Client().PostAsJsonAsync(RemoveUrl, new ClientDeleteRequest { ClientId = 5 });

        var saved = await SaveAsync(MasterAdminTestData.ValidRequest());

        Assert.Equal("CL0006", saved.ClientCode);
    }
}
