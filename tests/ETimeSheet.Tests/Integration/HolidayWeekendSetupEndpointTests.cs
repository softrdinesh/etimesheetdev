using System.Net;
using System.Net.Http.Json;
using ETimeSheet.Application.Models;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;
using static ETimeSheet.Tests.TestData.HolidayWeekendTestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// The holiday and weekend setup endpoints of <c>TimesheetProjectController</c>,
/// end to end against a real SQL Server: the recorded
/// <c>spc_GetTimesheetHolidaySetup</c>, <c>spc_AddEditTimesheetHolidaySetup</c>,
/// <c>spc_GetTimesheetWeekendSetup</c> and <c>spc_AddEditTimesheetWeekendSetup</c>,
/// the EF soft deletes, and the real middleware.
/// <para>
/// What only a database can prove lives here: the filters and ordering in the
/// procedures, the LEFT JOINs that name the country, client and project, the
/// organisation fence on an edit, and the one-setup-per-project rule.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class HolidayWeekendSetupEndpointTests : IntegrationTestBase
{
    private const string HolidayListUrl = "/api/v1/TimesheetProject/get-timesheet-holiday-setup";
    private const string HolidaySaveUrl = "/api/v1/TimesheetProject/add-edit-timesheet-holiday-setup";
    private const string HolidayRemoveUrl = "/api/v1/TimesheetProject/remove-timesheet-holiday-setup";

    private const string WeekendListUrl = "/api/v1/TimesheetProject/get-timesheet-weekend-setup";
    private const string WeekendSaveUrl = "/api/v1/TimesheetProject/add-edit-timesheet-weekend-setup";
    private const string WeekendRemoveUrl = "/api/v1/TimesheetProject/remove-timesheet-weekend-setup";

    public HolidayWeekendSetupEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // The controller is [AllowAnonymous] while authentication is off, so an
    // unauthenticated client is the honest way to call it.
    private HttpClient Client() => Factory.CreateClient();

    private async Task<List<T>> ListAsync<T>(string url)
    {
        var response = await Client().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<List<T>>();
    }

    private async Task<T> SaveAsync<T>(string url, object request)
    {
        var response = await Client().PostAsJsonAsync(url, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<T>();
    }

    /// <summary>
    /// A 400 in the error envelope, with one entry per named field. Entries
    /// are <c>"Field: message"</c>, so the prefix is the field the client sent.
    /// </summary>
    private static async Task AssertBadRequestNamingAsync(HttpResponseMessage response, params string[] fields)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<object>();
        Assert.False(envelope.Success);

        foreach (var field in fields)
        {
            Assert.Contains(envelope.Errors, error => error.StartsWith($"{field}:", StringComparison.Ordinal));
        }
    }

    private static async Task AssertFailureAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.False((await response.ReadEnvelopeAsync<object>()).Success);
    }

    // ================================================================ holiday list

    [Theory]
    [InlineData("")]
    [InlineData("?orgID=0")]
    [InlineData("?orgID=0&projectID=0")]
    public async Task HolidayListWithNoFilterIsABadRequestNamingOrgId(string query)
    {
        var response = await Client().GetAsync(HolidayListUrl + query);

        await AssertBadRequestNamingAsync(response, "orgID");
    }

    [Fact]
    public async Task HolidayListRejectsNegativeFiltersNamingEach()
    {
        var response = await Client().GetAsync($"{HolidayListUrl}?orgID=-1&projectID=-2");

        await AssertBadRequestNamingAsync(response, "orgID", "projectID");
    }

    /// <summary>Refused by model binding, before the service, but in the same envelope.</summary>
    [Fact]
    public async Task HolidayListRejectsANonNumericOrgId()
    {
        var response = await Client().GetAsync($"{HolidayListUrl}?orgID=abc");

        await AssertBadRequestNamingAsync(response, "orgID");
    }

    [Fact]
    public async Task HolidayListIsEmptyWhenNothingMatches()
    {
        Assert.Empty(await ListAsync<TimesheetHolidaySetupResponse>($"{HolidayListUrl}?orgID={OrganizationId}"));
    }

    /// <summary>
    /// <c>orgID</c> alone: every live holiday of the organisation, with or
    /// without a project, ordered by date and then id. A NULL <c>IsDelete</c>
    /// counts as live; a deleted holiday and another organisation's do not appear.
    /// </summary>
    [Fact]
    public async Task HolidayListByOrganisationReturnsItsLiveHolidaysInDateThenIdOrder()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedHolidaysAsync(
            Holiday(101, NewYearsDay, projectId: WebsiteProjectId),
            Holiday(103, ChristmasDay),
            Holiday(102, ChristmasDay, projectId: MobileProjectId, isDelete: null),
            Holiday(104, new DateTime(2026, 8, 15), isDelete: true),
            Holiday(105, new DateTime(2026, 5, 1), organizationId: OtherOrganizationId));

        var holidays = await ListAsync<TimesheetHolidaySetupResponse>($"{HolidayListUrl}?orgID={OrganizationId}");

        Assert.Equal(new[] { 102, 103, 101 }, holidays.Select(holiday => holiday.TimesheetHolidaySetupId));
    }

    /// <summary><c>projectID</c> alone matches the project whatever the organisation.</summary>
    [Fact]
    public async Task HolidayListByProjectReturnsThatProjectsHolidaysAcrossOrganisations()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedHolidaysAsync(
            Holiday(101, ChristmasDay, projectId: WebsiteProjectId),
            Holiday(102, NewYearsDay, organizationId: OtherOrganizationId, projectId: WebsiteProjectId),
            Holiday(103, ChristmasDay, projectId: MobileProjectId),
            Holiday(104, ChristmasDay));

        var holidays = await ListAsync<TimesheetHolidaySetupResponse>($"{HolidayListUrl}?projectID={WebsiteProjectId}");

        Assert.Equal(new[] { 101, 102 }, holidays.Select(holiday => holiday.TimesheetHolidaySetupId));
    }

    [Fact]
    public async Task HolidayListWithBothFiltersNarrowsToTheProjectInTheOrganisation()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedHolidaysAsync(
            Holiday(101, ChristmasDay, projectId: WebsiteProjectId),
            Holiday(102, NewYearsDay, organizationId: OtherOrganizationId, projectId: WebsiteProjectId),
            Holiday(103, ChristmasDay));

        var holidays = await ListAsync<TimesheetHolidaySetupResponse>(
            $"{HolidayListUrl}?orgID={OrganizationId}&projectID={WebsiteProjectId}");

        Assert.Equal(101, Assert.Single(holidays).TimesheetHolidaySetupId);
    }

    [Fact]
    public async Task HolidayListJoinsTheCountryClientAndProjectNames()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedHolidaysAsync(
            Holiday(101, ChristmasDay, "Christmas Day",
                countryId: IndiaCountryId, clientId: AcmeClientId, projectId: WebsiteProjectId));

        var holiday = Assert.Single(await ListAsync<TimesheetHolidaySetupResponse>($"{HolidayListUrl}?orgID={OrganizationId}"));

        Assert.Equal(ChristmasDay, holiday.HolidayDate);
        Assert.Equal("Christmas Day", holiday.Description);
        Assert.Equal(OrganizationId, holiday.OrganizationId);
        Assert.Equal(IndiaCountryId, holiday.CountryId);
        Assert.Equal(IndiaCountryName, holiday.CountryName);
        Assert.Equal(AcmeClientId, holiday.ClientId);
        Assert.Equal(AcmeClientName, holiday.ClientName);
        Assert.Equal(WebsiteProjectId, holiday.ProjectId);
        Assert.Equal(WebsiteProjectCode, holiday.ProjectCode);
        Assert.Equal(WebsiteProjectName, holiday.ProjectName);
        Assert.Equal(SeededCreateDate, holiday.CreateDate);
        Assert.Equal(SeedCreatorUserId, holiday.CreatedBy);
    }

    /// <summary>
    /// The joins are LEFT JOINs: ids that name nothing still list the holiday,
    /// with null names - and the name keys are still sent (CLAUDE.md §5).
    /// </summary>
    [Fact]
    public async Task HolidayListKeepsAHolidayWhoseReferencesNameNothing()
    {
        await Factory.SeedHolidaysAsync(Holiday(101, ChristmasDay, countryId: 999, clientId: 998, projectId: 997));

        var response = await Client().GetAsync($"{HolidayListUrl}?orgID={OrganizationId}");
        var json = await response.Content.ReadAsStringAsync();
        var holiday = Assert.Single(await response.ReadDataAsync<List<TimesheetHolidaySetupResponse>>());

        Assert.Equal(999, holiday.CountryId);
        Assert.Null(holiday.CountryName);
        Assert.Null(holiday.ClientName);
        Assert.Null(holiday.ProjectCode);
        Assert.Null(holiday.ProjectName);
        Assert.Contains("\"countryName\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"projectName\":null", json, StringComparison.Ordinal);
        Assert.DoesNotContain("isDelete", json, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================ holiday add

    [Fact]
    public async Task AddingAHolidayStoresItAndAnswersWithTheNames()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();

        var saved = await SaveAsync<TimesheetHolidaySetupResponse>(HolidaySaveUrl, HolidayRequest());

        Assert.Equal(ChristmasDay, saved.HolidayDate);
        Assert.Equal("Christmas Day", saved.Description);
        Assert.Equal(IndiaCountryName, saved.CountryName);
        Assert.Equal(AcmeClientName, saved.ClientName);
        Assert.Equal(WebsiteProjectName, saved.ProjectName);
        Assert.Equal(TimeLogTestData.Now, saved.CreateDate);
        Assert.Equal(EditorUserId, saved.CreatedBy);

        // And it is what landed in the table, not only what was answered.
        var row = Assert.Single(await Factory.AllHolidaysAsync());
        Assert.Equal(saved.TimesheetHolidaySetupId, row.TimesheetHolidaySetupId);
        Assert.Equal(ChristmasDay, row.HolidayDate);
        Assert.Equal("Christmas Day", row.Description);
        Assert.Equal(OrganizationId, row.OrganizationId);
        Assert.Equal(IndiaCountryId, row.CountryId);
        Assert.Equal(AcmeClientId, row.ClientId);
        Assert.Equal(WebsiteProjectId, row.ProjectId);
        Assert.Equal(TimeLogTestData.Now, row.CreateDate);
        Assert.Equal(EditorUserId, row.CreatedBy);
        Assert.Equal(false, row.IsDelete);
    }

    /// <summary>
    /// Country, client and project are optional; left out they are stored as
    /// NULL. The time of day sent with the date is dropped.
    /// </summary>
    [Fact]
    public async Task AddingWithOnlyTheRequiredFieldsStoresNullsAndADateWithoutTime()
    {
        var request = HolidayRequest(countryId: null, clientId: null, projectId: null);
        request.HolidayDate = new DateTime(2026, 12, 25, 15, 30, 0);

        var saved = await SaveAsync<TimesheetHolidaySetupResponse>(HolidaySaveUrl, request);

        Assert.Equal(ChristmasDay, saved.HolidayDate);
        Assert.Null(saved.CountryId);
        Assert.Null(saved.ClientId);
        Assert.Null(saved.ProjectId);

        var row = Assert.Single(await Factory.AllHolidaysAsync());
        Assert.Equal(ChristmasDay, row.HolidayDate);
        Assert.Null(row.CountryId);
        Assert.Null(row.ClientId);
        Assert.Null(row.ProjectId);
    }

    [Theory]
    [InlineData("  Christmas Day  ", "Christmas Day")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public async Task AddingTrimsTheDescriptionAndStoresABlankOneAsNull(string? sent, string? stored)
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();

        var saved = await SaveAsync<TimesheetHolidaySetupResponse>(HolidaySaveUrl, HolidayRequest(description: sent));

        Assert.Equal(stored, saved.Description);
        Assert.Equal(stored, Assert.Single(await Factory.AllHolidaysAsync()).Description);
    }

    /// <summary>Every shape failure is reported at once, keyed on the field sent, and nothing is written.</summary>
    [Fact]
    public async Task AddingAMalformedHolidayIsABadRequestNamingEveryFieldAndSavesNothing()
    {
        var request = new TimesheetHolidaySetupSaveRequest
        {
            TimesheetHolidaySetupId = -1,
            HolidayDate = null,
            Description = new string('x', 101),
            OrganizationId = 0,
            CountryId = 0,
            ClientId = 0,
            ProjectId = 0,
            CreatedBy = 0,
        };

        var response = await Client().PostAsJsonAsync(HolidaySaveUrl, request);

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetHolidaySetupSaveRequest.TimesheetHolidaySetupId),
            nameof(TimesheetHolidaySetupSaveRequest.HolidayDate),
            nameof(TimesheetHolidaySetupSaveRequest.Description),
            nameof(TimesheetHolidaySetupSaveRequest.OrganizationId),
            nameof(TimesheetHolidaySetupSaveRequest.CountryId),
            nameof(TimesheetHolidaySetupSaveRequest.ClientId),
            nameof(TimesheetHolidaySetupSaveRequest.ProjectId),
            nameof(TimesheetHolidaySetupSaveRequest.CreatedBy));
        Assert.Empty(await Factory.AllHolidaysAsync());
    }

    [Fact]
    public async Task ADescriptionOfExactly100CharactersIsAccepted()
    {
        var description = new string('x', 100);

        var saved = await SaveAsync<TimesheetHolidaySetupResponse>(
            HolidaySaveUrl,
            HolidayRequest(description: description, countryId: null, clientId: null, projectId: null));

        Assert.Equal(description, saved.Description);
    }

    /// <summary>
    /// No foreign key protects these columns, so the service looks each one
    /// up: an unknown country, and a client and project of another
    /// organisation, are all refused together.
    /// </summary>
    [Fact]
    public async Task AddingAgainstReferencesOutsideTheOrganisationIsABadRequestAndSavesNothing()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();

        var response = await Client().PostAsJsonAsync(
            HolidaySaveUrl,
            HolidayRequest(countryId: 999, clientId: OtherOrganizationClientId, projectId: OtherOrganizationProjectId));

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetHolidaySetupSaveRequest.CountryId),
            nameof(TimesheetHolidaySetupSaveRequest.ClientId),
            nameof(TimesheetHolidaySetupSaveRequest.ProjectId));
        Assert.Empty(await Factory.AllHolidaysAsync());
    }

    [Theory]
    [InlineData(DeletedClientId)]
    [InlineData(9999)]
    public async Task AddingAgainstADeletedOrUnknownClientIsABadRequest(int clientId)
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();

        var response = await Client().PostAsJsonAsync(HolidaySaveUrl, HolidayRequest(clientId: clientId));

        await AssertBadRequestNamingAsync(response, nameof(TimesheetHolidaySetupSaveRequest.ClientId));
        Assert.Empty(await Factory.AllHolidaysAsync());
    }

    // ================================================================ holiday edit

    /// <summary>
    /// An edit overwrites the date, description, country, client and project
    /// - a field left out is cleared - and leaves who created the row, and
    /// when, as they were.
    /// </summary>
    [Fact]
    public async Task EditingAHolidayOverwritesItsFieldsAndKeepsItsCreator()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedHolidaysAsync(
            Holiday(101, ChristmasDay, "Old Name",
                countryId: IndiaCountryId, clientId: AcmeClientId, projectId: WebsiteProjectId));

        var request = HolidayRequest(
            id: 101,
            description: "New Year",
            countryId: UnitedKingdomCountryId,
            clientId: null,
            projectId: MobileProjectId);
        request.HolidayDate = NewYearsDay;

        var saved = await SaveAsync<TimesheetHolidaySetupResponse>(HolidaySaveUrl, request);

        Assert.Equal(101, saved.TimesheetHolidaySetupId);
        Assert.Equal(NewYearsDay, saved.HolidayDate);
        Assert.Equal("New Year", saved.Description);
        Assert.Equal(UnitedKingdomCountryName, saved.CountryName);
        Assert.Null(saved.ClientId);
        Assert.Null(saved.ClientName);
        Assert.Equal(MobileProjectName, saved.ProjectName);
        Assert.Equal(SeededCreateDate, saved.CreateDate);
        Assert.Equal(SeedCreatorUserId, saved.CreatedBy);

        var row = Assert.Single(await Factory.AllHolidaysAsync());
        Assert.Equal(NewYearsDay, row.HolidayDate);
        Assert.Equal("New Year", row.Description);
        Assert.Equal(UnitedKingdomCountryId, row.CountryId);
        Assert.Null(row.ClientId);
        Assert.Equal(MobileProjectId, row.ProjectId);
        Assert.Equal(SeededCreateDate, row.CreateDate);
        Assert.Equal(SeedCreatorUserId, row.CreatedBy);
    }

    /// <summary>
    /// The procedure fences an edit by organisation: another organisation's
    /// holiday is "not found", and is left exactly as it was.
    /// </summary>
    [Fact]
    public async Task EditingAnotherOrganisationsHolidayIsNotFoundAndChangesNothing()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedHolidaysAsync(Holiday(101, ChristmasDay, "Theirs", organizationId: OtherOrganizationId));

        var response = await Client().PostAsJsonAsync(HolidaySaveUrl, HolidayRequest(id: 101, description: "Ours"));

        await AssertFailureAsync(response, HttpStatusCode.NotFound);

        var row = Assert.Single(await Factory.AllHolidaysAsync());
        Assert.Equal("Theirs", row.Description);
        Assert.Equal(OtherOrganizationId, row.OrganizationId);
    }

    /// <summary>A deleted holiday is not edited back to life, and an unknown id does not become an add.</summary>
    [Fact]
    public async Task EditingADeletedOrUnknownHolidayIsNotFound()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedHolidaysAsync(Holiday(101, ChristmasDay, "Gone", isDelete: true));

        await AssertFailureAsync(
            await Client().PostAsJsonAsync(HolidaySaveUrl, HolidayRequest(id: 101, description: "Back")),
            HttpStatusCode.NotFound);
        await AssertFailureAsync(
            await Client().PostAsJsonAsync(HolidaySaveUrl, HolidayRequest(id: 555)),
            HttpStatusCode.NotFound);

        var row = Assert.Single(await Factory.AllHolidaysAsync());
        Assert.Equal("Gone", row.Description);
        Assert.Equal(true, row.IsDelete);
    }

    // ================================================================ holiday remove

    [Fact]
    public async Task RemovingAHolidaySoftDeletesItAndTheListStopsShowingIt()
    {
        await Factory.SeedHolidaysAsync(Holiday(101, ChristmasDay), Holiday(102, NewYearsDay));

        var response = await Client().PostAsJsonAsync(
            HolidayRemoveUrl,
            new TimesheetHolidaySetupDeleteRequest { TimesheetHolidaySetupId = 101, OrganizationId = OrganizationId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadEnvelopeAsync<object>()).Success);

        // The row stays; only the flag changes.
        var row = await Factory.FindHolidayAsync(101);
        Assert.NotNull(row);
        Assert.Equal(true, row!.IsDelete);
        Assert.Equal(false, (await Factory.FindHolidayAsync(102))!.IsDelete);

        var listed = await ListAsync<TimesheetHolidaySetupResponse>($"{HolidayListUrl}?orgID={OrganizationId}");
        Assert.Equal(102, Assert.Single(listed).TimesheetHolidaySetupId);
    }

    [Fact]
    public async Task RemovingAHolidayTwiceIsNotFoundTheSecondTime()
    {
        await Factory.SeedHolidaysAsync(Holiday(101, ChristmasDay));
        var request = new TimesheetHolidaySetupDeleteRequest { TimesheetHolidaySetupId = 101, OrganizationId = OrganizationId };

        Assert.Equal(HttpStatusCode.OK, (await Client().PostAsJsonAsync(HolidayRemoveUrl, request)).StatusCode);

        await AssertFailureAsync(await Client().PostAsJsonAsync(HolidayRemoveUrl, request), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemovingAnotherOrganisationsHolidayIsNotFoundAndLeavesItLive()
    {
        await Factory.SeedHolidaysAsync(Holiday(101, ChristmasDay, organizationId: OtherOrganizationId));

        var response = await Client().PostAsJsonAsync(
            HolidayRemoveUrl,
            new TimesheetHolidaySetupDeleteRequest { TimesheetHolidaySetupId = 101, OrganizationId = OrganizationId });

        await AssertFailureAsync(response, HttpStatusCode.NotFound);
        Assert.Equal(false, (await Factory.FindHolidayAsync(101))!.IsDelete);
    }

    [Fact]
    public async Task RemovingAnUnknownHolidayIsNotFound()
    {
        var response = await Client().PostAsJsonAsync(
            HolidayRemoveUrl,
            new TimesheetHolidaySetupDeleteRequest { TimesheetHolidaySetupId = 555, OrganizationId = OrganizationId });

        await AssertFailureAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemovingWithNonPositiveIdsIsABadRequestNamingEach()
    {
        var response = await Client().PostAsJsonAsync(
            HolidayRemoveUrl,
            new TimesheetHolidaySetupDeleteRequest { TimesheetHolidaySetupId = 0, OrganizationId = -1 });

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetHolidaySetupDeleteRequest.TimesheetHolidaySetupId),
            nameof(TimesheetHolidaySetupDeleteRequest.OrganizationId));
    }

    // ================================================================ weekend list

    [Theory]
    [InlineData("")]
    [InlineData("?projectID=0")]
    [InlineData("?orgID=0&projectID=0")]
    public async Task WeekendListWithNoFilterIsABadRequestNamingOrgId(string query)
    {
        var response = await Client().GetAsync(WeekendListUrl + query);

        await AssertBadRequestNamingAsync(response, "orgID");
    }

    [Fact]
    public async Task WeekendListRejectsNegativeFiltersNamingEach()
    {
        var response = await Client().GetAsync($"{WeekendListUrl}?orgID=-5&projectID=-1");

        await AssertBadRequestNamingAsync(response, "orgID", "projectID");
    }

    [Fact]
    public async Task WeekendListRejectsANonNumericProjectId()
    {
        var response = await Client().GetAsync($"{WeekendListUrl}?projectID=xyz");

        await AssertBadRequestNamingAsync(response, "projectID");
    }

    /// <summary>
    /// Ordered by project name, not by id; deleted setups and another
    /// organisation's do not appear.
    /// </summary>
    [Fact]
    public async Task WeekendListByOrganisationReturnsLiveSetupsOrderedByProjectName()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(
            WeekendSetup(101, WebsiteProjectId),
            WeekendSetup(102, MobileProjectId),
            WeekendSetup(103, WebsiteProjectId, isDelete: true),
            WeekendSetup(104, OtherOrganizationProjectId, organizationId: OtherOrganizationId));

        var setups = await ListAsync<TimesheetWeekendSetupResponse>($"{WeekendListUrl}?orgID={OrganizationId}");

        Assert.Equal(new[] { 102, 101 }, setups.Select(setup => setup.TimeSheetWeekendSetupId));
    }

    [Fact]
    public async Task WeekendListByProjectReturnsThatProjectsSetup()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(
            WeekendSetup(101, WebsiteProjectId),
            WeekendSetup(102, MobileProjectId));

        var setups = await ListAsync<TimesheetWeekendSetupResponse>($"{WeekendListUrl}?projectID={MobileProjectId}");

        Assert.Equal(102, Assert.Single(setups).TimeSheetWeekendSetupId);
    }

    [Fact]
    public async Task WeekendListWithBothFiltersFromDifferentOrganisationsIsEmpty()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(WeekendSetup(101, WebsiteProjectId));

        Assert.Empty(await ListAsync<TimesheetWeekendSetupResponse>(
            $"{WeekendListUrl}?orgID={OtherOrganizationId}&projectID={WebsiteProjectId}"));
    }

    [Fact]
    public async Task WeekendListCarriesTheDayFlagsAndTheJoinedNames()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(
            WeekendSetup(101, WebsiteProjectId, clientId: AcmeClientId, isFridayWeekend: true));

        var setup = Assert.Single(await ListAsync<TimesheetWeekendSetupResponse>($"{WeekendListUrl}?orgID={OrganizationId}"));

        Assert.Equal(OrganizationId, setup.OrganizationId);
        Assert.Equal(AcmeClientId, setup.ClientId);
        Assert.Equal(AcmeClientName, setup.ClientName);
        Assert.Equal(WebsiteProjectCode, setup.ProjectCode);
        Assert.Equal(WebsiteProjectName, setup.ProjectName);
        Assert.Equal(false, setup.IsMondayWeekend);
        Assert.Equal(false, setup.IsThursdayWeekend);
        Assert.Equal(true, setup.IsFridayWeekend);
        Assert.Equal(true, setup.IsSaturdayWeekend);
        Assert.Equal(true, setup.IsSundayWeekend);
        Assert.Equal(SeededCreateDate, setup.CreateDate);
        Assert.Equal(SeedCreatorUserId, setup.CreatedBy);
        Assert.Null(setup.UpdatedDate);
        Assert.Null(setup.UpdatedBy);
    }

    // ================================================================ weekend add

    [Fact]
    public async Task AddingAWeekendSetupStoresItAndAnswersWithTheNames()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();

        var saved = await SaveAsync<TimesheetWeekendSetupResponse>(WeekendSaveUrl, WeekendRequest());

        Assert.Equal(OrganizationId, saved.OrganizationId);
        Assert.Equal(AcmeClientName, saved.ClientName);
        Assert.Equal(WebsiteProjectName, saved.ProjectName);
        Assert.Equal(true, saved.IsFridayWeekend);
        Assert.Equal(true, saved.IsSaturdayWeekend);
        Assert.Equal(false, saved.IsSundayWeekend);
        Assert.Equal(TimeLogTestData.Now, saved.CreateDate);
        Assert.Equal(EditorUserId, saved.CreatedBy);
        Assert.Null(saved.UpdatedDate);

        var row = Assert.Single(await Factory.AllWeekendSetupsAsync());
        Assert.Equal(saved.TimeSheetWeekendSetupId, row.TimeSheetWeekendSetupId);
        Assert.Equal(OrganizationId, row.OrganizationId);
        Assert.Equal(AcmeClientId, row.ClientId);
        Assert.Equal(WebsiteProjectId, row.ProjectId);
        Assert.Equal(false, row.IsMondayWeekend);
        Assert.Equal(true, row.IsFridayWeekend);
        Assert.Equal(true, row.IsSaturdayWeekend);
        Assert.Equal(false, row.IsSundayWeekend);
        Assert.Equal(TimeLogTestData.Now, row.CreateDate);
        Assert.Equal(EditorUserId, row.CreatedBy);
        Assert.Equal(false, row.IsDelete);
        Assert.Null(row.UpdatedDate);
        Assert.Null(row.UpdatedBy);
    }

    [Fact]
    public async Task AddingAWeekendSetupWithoutAClientIsAllowed()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();

        var saved = await SaveAsync<TimesheetWeekendSetupResponse>(WeekendSaveUrl, WeekendRequest(clientId: null));

        Assert.Null(saved.ClientId);
        Assert.Null(saved.ClientName);
        Assert.Null(Assert.Single(await Factory.AllWeekendSetupsAsync()).ClientId);
    }

    [Fact]
    public async Task AddingAMalformedWeekendSetupIsABadRequestNamingEveryFieldAndSavesNothing()
    {
        var request = new TimesheetWeekendSetupSaveRequest
        {
            TimeSheetWeekendSetupId = -1,
            OrganizationId = 0,
            ClientId = 0,
            ProjectId = 0,
            UserId = 0,
        };

        var response = await Client().PostAsJsonAsync(WeekendSaveUrl, request);

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetWeekendSetupSaveRequest.TimeSheetWeekendSetupId),
            nameof(TimesheetWeekendSetupSaveRequest.OrganizationId),
            nameof(TimesheetWeekendSetupSaveRequest.ClientId),
            nameof(TimesheetWeekendSetupSaveRequest.ProjectId),
            nameof(TimesheetWeekendSetupSaveRequest.UserId));
        Assert.Empty(await Factory.AllWeekendSetupsAsync());
    }

    [Theory]
    [InlineData(OtherOrganizationProjectId)]
    [InlineData(9999)]
    public async Task AddingForAProjectOutsideTheOrganisationIsABadRequest(int projectId)
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();

        var response = await Client().PostAsJsonAsync(WeekendSaveUrl, WeekendRequest(projectId: projectId));

        await AssertBadRequestNamingAsync(response, nameof(TimesheetWeekendSetupSaveRequest.ProjectId));
        Assert.Empty(await Factory.AllWeekendSetupsAsync());
    }

    [Theory]
    [InlineData(OtherOrganizationClientId)]
    [InlineData(DeletedClientId)]
    public async Task AddingWithAClientOutsideTheOrganisationIsABadRequest(int clientId)
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();

        var response = await Client().PostAsJsonAsync(WeekendSaveUrl, WeekendRequest(clientId: clientId));

        await AssertBadRequestNamingAsync(response, nameof(TimesheetWeekendSetupSaveRequest.ClientId));
        Assert.Empty(await Factory.AllWeekendSetupsAsync());
    }

    [Fact]
    public async Task AddingASecondSetupForTheSameProjectIsAConflict()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(WeekendSetup(101, WebsiteProjectId));

        var response = await Client().PostAsJsonAsync(WeekendSaveUrl, WeekendRequest());

        await AssertFailureAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(101, Assert.Single(await Factory.AllWeekendSetupsAsync()).TimeSheetWeekendSetupId);
    }

    /// <summary>A deleted setup no longer counts: the project may be given a new one.</summary>
    [Fact]
    public async Task ADeletedSetupDoesNotBlockANewOneForTheSameProject()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(WeekendSetup(101, WebsiteProjectId, isDelete: true));

        var saved = await SaveAsync<TimesheetWeekendSetupResponse>(WeekendSaveUrl, WeekendRequest());

        Assert.NotEqual(101, saved.TimeSheetWeekendSetupId);

        var listed = await ListAsync<TimesheetWeekendSetupResponse>($"{WeekendListUrl}?projectID={WebsiteProjectId}");
        Assert.Equal(saved.TimeSheetWeekendSetupId, Assert.Single(listed).TimeSheetWeekendSetupId);
    }

    // ================================================================ weekend edit

    /// <summary>
    /// An edit that keeps its project is not a conflict with itself. It
    /// overwrites the client and every day flag, stamps who updated it and
    /// when, and leaves the creator alone.
    /// </summary>
    [Fact]
    public async Task EditingAWeekendSetupOverwritesItAndStampsTheUpdate()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(WeekendSetup(101, WebsiteProjectId));

        var saved = await SaveAsync<TimesheetWeekendSetupResponse>(WeekendSaveUrl, WeekendRequest(id: 101));

        Assert.Equal(101, saved.TimeSheetWeekendSetupId);
        Assert.Equal(AcmeClientName, saved.ClientName);
        Assert.Equal(true, saved.IsFridayWeekend);
        Assert.Equal(false, saved.IsSundayWeekend);
        Assert.Equal(TimeLogTestData.Now, saved.UpdatedDate);
        Assert.Equal(EditorUserId, saved.UpdatedBy);
        Assert.Equal(SeededCreateDate, saved.CreateDate);
        Assert.Equal(SeedCreatorUserId, saved.CreatedBy);

        var row = Assert.Single(await Factory.AllWeekendSetupsAsync());
        Assert.Equal(AcmeClientId, row.ClientId);
        Assert.Equal(true, row.IsFridayWeekend);
        Assert.Equal(true, row.IsSaturdayWeekend);
        Assert.Equal(false, row.IsSundayWeekend);
        Assert.Equal(TimeLogTestData.Now, row.UpdatedDate);
        Assert.Equal(EditorUserId, row.UpdatedBy);
        Assert.Equal(SeededCreateDate, row.CreateDate);
        Assert.Equal(SeedCreatorUserId, row.CreatedBy);
    }

    [Fact]
    public async Task EditingASetupOntoAProjectThatAlreadyHasOneIsAConflict()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(
            WeekendSetup(101, WebsiteProjectId),
            WeekendSetup(102, MobileProjectId));

        var response = await Client().PostAsJsonAsync(
            WeekendSaveUrl,
            WeekendRequest(id: 102, projectId: WebsiteProjectId));

        await AssertFailureAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(MobileProjectId, (await Factory.FindWeekendSetupAsync(102))!.ProjectId);
    }

    [Fact]
    public async Task EditingAnotherOrganisationsSetupIsNotFoundAndChangesNothing()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(
            WeekendSetup(104, OtherOrganizationProjectId, organizationId: OtherOrganizationId));

        var response = await Client().PostAsJsonAsync(WeekendSaveUrl, WeekendRequest(id: 104));

        await AssertFailureAsync(response, HttpStatusCode.NotFound);

        var row = Assert.Single(await Factory.AllWeekendSetupsAsync());
        Assert.Equal(OtherOrganizationProjectId, row.ProjectId);
        Assert.Null(row.UpdatedDate);
    }

    [Fact]
    public async Task EditingADeletedOrUnknownSetupIsNotFound()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(WeekendSetup(103, WebsiteProjectId, isDelete: true));

        await AssertFailureAsync(
            await Client().PostAsJsonAsync(WeekendSaveUrl, WeekendRequest(id: 103)),
            HttpStatusCode.NotFound);
        await AssertFailureAsync(
            await Client().PostAsJsonAsync(WeekendSaveUrl, WeekendRequest(id: 555, projectId: MobileProjectId)),
            HttpStatusCode.NotFound);

        var row = Assert.Single(await Factory.AllWeekendSetupsAsync());
        Assert.Equal(true, row.IsDelete);
        Assert.Null(row.UpdatedDate);
    }

    // ================================================================ weekend remove

    [Fact]
    public async Task RemovingAWeekendSetupSoftDeletesAndStampsIt()
    {
        await Factory.SeedHolidayWeekendReferenceDataAsync();
        await Factory.SeedWeekendSetupsAsync(WeekendSetup(101, WebsiteProjectId));

        var response = await Client().PostAsJsonAsync(
            WeekendRemoveUrl,
            new TimesheetWeekendSetupDeleteRequest { TimeSheetWeekendSetupId = 101, DeletedBy = EditorUserId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var row = await Factory.FindWeekendSetupAsync(101);
        Assert.NotNull(row);
        Assert.Equal(true, row!.IsDelete);
        Assert.Equal(TimeLogTestData.Now, row.DeletedDate);
        Assert.Equal(EditorUserId, row.DeletedBy);

        Assert.Empty(await ListAsync<TimesheetWeekendSetupResponse>($"{WeekendListUrl}?projectID={WebsiteProjectId}"));
    }

    /// <summary>A second remove is a 404, and never re-stamps who deleted it.</summary>
    [Fact]
    public async Task RemovingAWeekendSetupTwiceIsNotFoundAndKeepsTheFirstStamp()
    {
        await Factory.SeedWeekendSetupsAsync(WeekendSetup(101, WebsiteProjectId));

        Assert.Equal(
            HttpStatusCode.OK,
            (await Client().PostAsJsonAsync(
                WeekendRemoveUrl,
                new TimesheetWeekendSetupDeleteRequest { TimeSheetWeekendSetupId = 101, DeletedBy = EditorUserId })).StatusCode);

        var second = await Client().PostAsJsonAsync(
            WeekendRemoveUrl,
            new TimesheetWeekendSetupDeleteRequest { TimeSheetWeekendSetupId = 101, DeletedBy = 99 });

        await AssertFailureAsync(second, HttpStatusCode.NotFound);
        Assert.Equal(EditorUserId, (await Factory.FindWeekendSetupAsync(101))!.DeletedBy);
    }

    [Fact]
    public async Task RemovingAnUnknownWeekendSetupIsNotFound()
    {
        var response = await Client().PostAsJsonAsync(
            WeekendRemoveUrl,
            new TimesheetWeekendSetupDeleteRequest { TimeSheetWeekendSetupId = 555, DeletedBy = EditorUserId });

        await AssertFailureAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemovingAWeekendSetupWithNonPositiveValuesIsABadRequestNamingEach()
    {
        await Factory.SeedWeekendSetupsAsync(WeekendSetup(101, WebsiteProjectId));

        var response = await Client().PostAsJsonAsync(
            WeekendRemoveUrl,
            new TimesheetWeekendSetupDeleteRequest { TimeSheetWeekendSetupId = 0, DeletedBy = 0 });

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetWeekendSetupDeleteRequest.TimeSheetWeekendSetupId),
            nameof(TimesheetWeekendSetupDeleteRequest.DeletedBy));
        Assert.Equal(false, (await Factory.FindWeekendSetupAsync(101))!.IsDelete);
    }
}
