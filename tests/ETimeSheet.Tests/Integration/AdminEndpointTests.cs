using System.Net;
using System.Net.Http.Json;
using ETimeSheet.Application.Models;
using ETimeSheet.Shared.Responses;
using ETimeSheet.Shared.Utilities;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// Every Admin endpoint except <c>get-all-employees-by-orgid</c> (which has
/// <see cref="EmployeeListEndpointTests"/>), end to end against a real SQL
/// Server: the recorded procedures, the EF mappings over the recorded tables,
/// the real validators and middleware.
/// <para>
/// This is where the rules that live in SQL or in the soft-delete filters are
/// proven: add-versus-edit in <c>spc_AddEditUserRoleByAdmin</c> and
/// <c>spc_AddEditTimesheetProjectCategory</c>, the organisation scoping in both,
/// the dashboard arithmetic, and the save that revives a deleted setup instead
/// of duplicating it.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class AdminEndpointTests : IntegrationTestBase
{
    private const string Base = "/api/v1/Admin";
    private const string SaveSetupUrl = Base + "/save-user-timesheet-setup";
    private const string DeleteSetupUrl = Base + "/delete-timesheet-setup";
    private const string CountriesUrl = Base + "/get-country-list-with-timezones";
    private const string SaveRoleUrl = Base + "/add-edit-user-role";
    private const string DeleteRoleUrl = Base + "/delete-user-role";
    private const string SaveCategoryUrl = Base + "/add-edit-timesheet-project-category";
    private const string RemoveCategoryUrl = Base + "/remove-timesheet-project-category";

    private const string NoSetupMessage = "This user has no timesheet setup.";

    private static string GetSetupUrl(int userId) => $"{Base}/get-user-timesheet-setup/{userId}";
    private static string DashboardUrl(int orgId) => $"{Base}/get-admin-dashboard-summary-by-orgID/{orgId}";
    private static string RoleListUrl(int orgId) => $"{Base}/get-user-role-list-by-orgid/{orgId}";
    private static string CategoryListUrl(int orgId) => $"{Base}/get-timesheet-project-category-by-orgID/{orgId}";

    public AdminEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // Every Admin endpoint is [AllowAnonymous] while authentication is off, so
    // an unauthenticated client is the honest way to call them.
    private HttpClient Client() => Factory.CreateClient();

    private Task<HttpResponseMessage> PostAsync<T>(string url, T body) => Client().PostAsJsonAsync(url, body);

    private async Task<T> OkDataAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<T>();
    }

    private async Task<T> GetOkAsync<T>(string url) => await OkDataAsync<T>(await Client().GetAsync(url));

    private async Task<T> PostOkAsync<TBody, T>(string url, TBody body) => await OkDataAsync<T>(await PostAsync(url, body));

    /// <summary>
    /// Asserts a 400 in the standard envelope whose errors name every one of
    /// <paramref name="fields"/>. Both the FluentValidation path and the
    /// service's <c>ValidationException</c> write errors as <c>"Field: message"</c>.
    /// </summary>
    private static async Task AssertBadRequestNamingAsync(HttpResponseMessage response, params string[] fields)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<object>();
        Assert.False(envelope.Success);

        foreach (var field in fields)
        {
            Assert.Contains(envelope.Errors, error => error.Contains(field + ":", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False((await response.ReadEnvelopeAsync<object>()).Success);
    }

    // ================================================================ save-user-timesheet-setup

    [Fact]
    public async Task SaveAddsTheFirstSetupForAUserAndStoresEveryField()
    {
        await Factory.SeedAdminCountriesAsync(AdminTestData.Country(AdminTestData.IndiaCountryId, AdminTestData.IndiaName, AdminTestData.IndiaZone));

        var saved = await PostOkAsync<AdminSaveRequest, AdminResponse>(SaveSetupUrl, AdminTestData.SaveRequest());

        var row = Assert.Single(await Factory.FindAdminSetupsForUserAsync(AdminTestData.UserId));
        Assert.Equal(row.SetupId, saved.SetupId);

        Assert.Equal(TimeSpan.FromHours(8), row.MaxTimeInHrs);
        Assert.Equal(TimeSpan.FromMinutes(30), row.MaxTimInMins);
        Assert.Equal(AdminTestData.OrganizationId, row.OrganizationId);
        Assert.Equal(AdminTestData.FullTime, row.ContractType);
        Assert.Equal(Constants.DayMaster.DayId.Monday, row.StartDay);
        Assert.Equal(Constants.DayMaster.DayId.Friday, row.EndDay);
        Assert.Equal(Constants.DayMaster.DayId.Saturday, row.ExceptionDay);
        Assert.Equal(AdminTestData.IndiaCountryId, row.CountryId);
        Assert.Equal(AdminTestData.IndiaZone, row.TimeZone);
        Assert.Equal(new TimeSpan(18, 0, 0), row.TimeEntryLockAt);
        Assert.True(row.NeedToSendReminder);
        Assert.Equal(TimeSpan.FromHours(1), row.ReminderTimeBeforeCutoff);
        Assert.False(row.IsDelete);
        Assert.Equal(AdminTestData.ActingUserId, row.CreatedBy);
        Assert.Equal(Factory.Clock.UtcNow, row.CreateDate);
        Assert.Null(row.UpdatedBy);
        Assert.Null(row.UpdateDate);

        // The response is the saved row, with the country named on the way out.
        Assert.Equal(AdminTestData.UserId, saved.UserId);
        Assert.Equal(TimeSpan.FromHours(8), saved.MaxTimeInHrs);
        Assert.Equal(new TimeSpan(18, 0, 0), saved.TimeEntryLockAt);
        Assert.Equal(AdminTestData.IndiaName, saved.CountryName);
        Assert.Equal($"{AdminTestData.IndiaName}-{AdminTestData.IndiaZone}", saved.CountryWithTimeZone);
        Assert.Equal(AdminTestData.ActingUserId, saved.CreatedBy);
    }

    /// <summary>
    /// The user, not a setup id, names the row: saving again for someone who
    /// has a setup overwrites it, and records the saver as UpdatedBy while
    /// leaving the original creator alone.
    /// </summary>
    [Fact]
    public async Task SavingForAUserWhoHasASetupUpdatesThatRowInPlace()
    {
        await Factory.SeedAdminSetupsWithIdsAsync(AdminTestData.Setup(31));

        var saved = await PostOkAsync<AdminSaveRequest, AdminResponse>(SaveSetupUrl, AdminTestData.SaveRequest());

        Assert.Equal(31, saved.SetupId);

        var row = Assert.Single(await Factory.FindAdminSetupsForUserAsync(AdminTestData.UserId));
        Assert.Equal(31, row.SetupId);
        Assert.Equal(AdminTestData.FullTime, row.ContractType);
        Assert.Equal(AdminTestData.OrganizationId, row.OrganizationId);
        Assert.Equal(Constants.DayMaster.DayId.Monday, row.StartDay);
        Assert.Equal(Constants.DayMaster.DayId.Friday, row.EndDay);
        Assert.Equal(TimeSpan.FromHours(8), row.MaxTimeInHrs);
        Assert.Equal(AdminTestData.ActingUserId, row.UpdatedBy);
        Assert.Equal(Factory.Clock.UtcNow, row.UpdateDate);
        Assert.Equal(AdminTestData.OriginalCreatorId, row.CreatedBy);
        Assert.Equal(AdminTestData.EarlierDate, row.CreateDate);
    }

    /// <summary>
    /// A deleted setup comes back rather than gaining a twin: the same id,
    /// live again, with its delete stamps cleared.
    /// </summary>
    [Fact]
    public async Task SavingForAUserWhoseSetupWasDeletedRevivesTheSameRow()
    {
        await Factory.SeedAdminSetupsWithIdsAsync(AdminTestData.Setup(40, isDelete: true));

        var saved = await PostOkAsync<AdminSaveRequest, AdminResponse>(SaveSetupUrl, AdminTestData.SaveRequest());

        Assert.Equal(40, saved.SetupId);

        var row = Assert.Single(await Factory.FindAdminSetupsForUserAsync(AdminTestData.UserId));
        Assert.Equal(40, row.SetupId);
        Assert.False(row.IsDelete);
        Assert.Null(row.DeleteDate);
        Assert.Null(row.DeletedBy);
        Assert.Equal(AdminTestData.FullTime, row.ContractType);
        Assert.Equal(AdminTestData.OriginalCreatorId, row.CreatedBy);
        Assert.Equal(AdminTestData.ActingUserId, row.UpdatedBy);
    }

    /// <summary>Live rows are preferred over deleted ones, so a deleted row is not revived beside a live one.</summary>
    [Fact]
    public async Task SavingUpdatesTheLiveRowWhenTheUserAlsoHasADeletedOne()
    {
        await Factory.SeedAdminSetupsWithIdsAsync(
            AdminTestData.Setup(50, isDelete: true),
            AdminTestData.Setup(51));

        var saved = await PostOkAsync<AdminSaveRequest, AdminResponse>(SaveSetupUrl, AdminTestData.SaveRequest());

        Assert.Equal(51, saved.SetupId);

        var deleted = await Factory.FindAdminSetupAsync(50);
        Assert.NotNull(deleted);
        Assert.True(deleted!.IsDelete);
        Assert.Equal(AdminTestData.PartTime, deleted.ContractType);
        Assert.Null(deleted.UpdatedBy);
    }

    [Fact]
    public async Task SavingForOneUserLeavesAnotherUsersSetupAlone()
    {
        await Factory.SeedAdminSetupsWithIdsAsync(AdminTestData.Setup(60, userId: AdminTestData.OtherUserId));

        var saved = await PostOkAsync<AdminSaveRequest, AdminResponse>(SaveSetupUrl, AdminTestData.SaveRequest());

        Assert.NotEqual(60, saved.SetupId);

        var other = Assert.Single(await Factory.FindAdminSetupsForUserAsync(AdminTestData.OtherUserId));
        Assert.Equal(AdminTestData.PartTime, other.ContractType);
        Assert.Null(other.UpdatedBy);
        Assert.Single(await Factory.FindAdminSetupsForUserAsync(AdminTestData.UserId));
    }

    /// <summary>
    /// CountryId and TimeZone are stored exactly as sent - nothing checks them
    /// against dbo.Country - and a country with no row reads back unnamed.
    /// </summary>
    [Fact]
    public async Task SaveStoresAnUnknownCountryAndZoneAsSent()
    {
        var saved = await PostOkAsync<AdminSaveRequest, AdminResponse>(
            SaveSetupUrl,
            AdminTestData.SaveRequest(countryId: 9999, timeZone: "Mars/Olympus_Mons"));

        var row = Assert.Single(await Factory.FindAdminSetupsForUserAsync(AdminTestData.UserId));
        Assert.Equal(9999, row.CountryId);
        Assert.Equal("Mars/Olympus_Mons", row.TimeZone);

        Assert.Null(saved.CountryName);
        Assert.Equal("Mars/Olympus_Mons", saved.CountryWithTimeZone);
    }

    [Fact]
    public async Task SaveLeavesTheOptionalTimesNullWhenTheyAreNotSent()
    {
        var request = AdminTestData.SaveRequest();
        request.TimeEntryLockAt = null;
        request.ReminderTimeBeforeCutoff = null;
        request.ExceptionDay = null;

        var saved = await PostOkAsync<AdminSaveRequest, AdminResponse>(SaveSetupUrl, request);

        var row = Assert.Single(await Factory.FindAdminSetupsForUserAsync(AdminTestData.UserId));
        Assert.Null(row.TimeEntryLockAt);
        Assert.Null(row.ReminderTimeBeforeCutoff);
        Assert.Null(row.ExceptionDay);
        Assert.Null(saved.TimeEntryLockAt);
        Assert.Null(saved.ReminderTimeBeforeCutoff);
    }

    /// <summary>The two bounded fields accept their inclusive upper bounds.</summary>
    [Fact]
    public async Task SaveAcceptsTheUpperBoundOfEachDailyMaximumField()
    {
        var request = AdminTestData.SaveRequest();
        request.MaxTimeInHrs = "23:00:00";
        request.MaxTimInMins = "00:59:00";

        await PostOkAsync<AdminSaveRequest, AdminResponse>(SaveSetupUrl, request);

        var row = Assert.Single(await Factory.FindAdminSetupsForUserAsync(AdminTestData.UserId));
        Assert.Equal(new TimeSpan(23, 0, 0), row.MaxTimeInHrs);
        Assert.Equal(new TimeSpan(0, 59, 0), row.MaxTimInMins);
    }

    [Fact]
    public async Task SaveWithAMalformedPayloadIsABadRequestNamingEachFieldAndSavesNothing()
    {
        var request = AdminTestData.SaveRequest();
        request.UserId = 0;
        request.CreatedBy = 0;
        request.ContractType = 3;
        request.StartDay = 8;
        request.EndDay = null;
        request.CountryId = 0;
        request.TimeZone = "   ";

        var response = await PostAsync(SaveSetupUrl, request);

        await AssertBadRequestNamingAsync(
            response,
            nameof(AdminSaveRequest.UserId),
            nameof(AdminSaveRequest.CreatedBy),
            nameof(AdminSaveRequest.ContractType),
            nameof(AdminSaveRequest.StartDay),
            nameof(AdminSaveRequest.EndDay),
            nameof(AdminSaveRequest.CountryId),
            nameof(AdminSaveRequest.TimeZone));

        Assert.Empty(await Factory.FindAdminSetupsForUserAsync(0));
    }

    /// <summary>
    /// The time fields are read by the service through TimeOfDay, so a missing
    /// required one, a value that is not hh:mm:ss, and one past its range are
    /// all a 400 keyed on the field - and nothing is written.
    /// </summary>
    [Theory]
    [InlineData(nameof(AdminSaveRequest.MaxTimeInHrs), null)]
    [InlineData(nameof(AdminSaveRequest.MaxTimeInHrs), "08:00")]
    [InlineData(nameof(AdminSaveRequest.MaxTimeInHrs), "23:00:01")]
    [InlineData(nameof(AdminSaveRequest.MaxTimInMins), "")]
    [InlineData(nameof(AdminSaveRequest.MaxTimInMins), "01:00:00")]
    [InlineData(nameof(AdminSaveRequest.TimeEntryLockAt), "1.06:00:00")]
    [InlineData(nameof(AdminSaveRequest.TimeEntryLockAt), "25:00:00")]
    [InlineData(nameof(AdminSaveRequest.ReminderTimeBeforeCutoff), "soon")]
    public async Task SaveRejectsABadTimeFieldNamingItAndSavesNothing(string field, string? value)
    {
        var request = AdminTestData.SaveRequest();

        switch (field)
        {
            case nameof(AdminSaveRequest.MaxTimeInHrs): request.MaxTimeInHrs = value; break;
            case nameof(AdminSaveRequest.MaxTimInMins): request.MaxTimInMins = value; break;
            case nameof(AdminSaveRequest.TimeEntryLockAt): request.TimeEntryLockAt = value; break;
            case nameof(AdminSaveRequest.ReminderTimeBeforeCutoff): request.ReminderTimeBeforeCutoff = value; break;
        }

        await AssertBadRequestNamingAsync(await PostAsync(SaveSetupUrl, request), field);

        Assert.Empty(await Factory.FindAdminSetupsForUserAsync(AdminTestData.UserId));
    }

    // ================================================================ get-user-timesheet-setup

    /// <summary>No setup is an answer, not an error: 200, success, null data, and a message saying so.</summary>
    [Fact]
    public async Task GetSetupForAUserWithNoneIsSuccessWithNullData()
    {
        var response = await Client().GetAsync(GetSetupUrl(AdminTestData.UserId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<AdminResponse>();
        Assert.True(envelope.Success);
        Assert.Null(envelope.Data);
        Assert.Equal(NoSetupMessage, envelope.Message);
    }

    [Fact]
    public async Task GetSetupTreatsADeletedSetupAsNone()
    {
        await Factory.SeedAdminSetupsWithIdsAsync(AdminTestData.Setup(70, isDelete: true));

        var envelope = await (await Client().GetAsync(GetSetupUrl(AdminTestData.UserId))).ReadEnvelopeAsync<AdminResponse>();

        Assert.True(envelope.Success);
        Assert.Null(envelope.Data);
        Assert.Equal(NoSetupMessage, envelope.Message);
    }

    [Fact]
    public async Task GetSetupReturnsTheUsersSetupWithItsCountryNamed()
    {
        await Factory.SeedAdminCountriesAsync(AdminTestData.Country(AdminTestData.IndiaCountryId, AdminTestData.IndiaName, AdminTestData.IndiaZone));
        await Factory.SeedAdminSetupsWithIdsAsync(
            AdminTestData.Setup(80),
            AdminTestData.Setup(81, userId: AdminTestData.OtherUserId, contractType: AdminTestData.FullTime));

        var setup = await GetOkAsync<AdminResponse>(GetSetupUrl(AdminTestData.UserId));

        Assert.Equal(80, setup.SetupId);
        Assert.Equal(AdminTestData.UserId, setup.UserId);
        Assert.Equal(AdminTestData.PartTime, setup.ContractType);
        Assert.Equal(AdminTestData.OtherOrganizationId, setup.OrganizationId);
        Assert.Equal(Constants.DayMaster.DayId.Tuesday, setup.StartDay);
        Assert.Equal(Constants.DayMaster.DayId.Thursday, setup.EndDay);
        Assert.Equal(TimeSpan.FromHours(6), setup.MaxTimeInHrs);
        Assert.Equal(TimeSpan.FromMinutes(15), setup.MaxTimInMins);
        Assert.Equal(new TimeSpan(20, 0, 0), setup.TimeEntryLockAt);
        Assert.Equal(new TimeSpan(0, 30, 0), setup.ReminderTimeBeforeCutoff);
        Assert.False(setup.NeedToSendReminder);
        Assert.Equal(AdminTestData.IndiaCountryId, setup.CountryId);
        Assert.Equal(AdminTestData.IndiaName, setup.CountryName);
        Assert.Equal(AdminTestData.IndiaZone, setup.TimeZone);
        Assert.Equal($"{AdminTestData.IndiaName}-{AdminTestData.IndiaZone}", setup.CountryWithTimeZone);
        Assert.Equal(AdminTestData.OriginalCreatorId, setup.CreatedBy);
        Assert.Equal(AdminTestData.EarlierDate, setup.CreateDate);
    }

    /// <summary>
    /// A stored country id with no dbo.Country row reads back with a null name
    /// and the bare zone as its label - and the null keys are still written
    /// (CLAUDE.md section 5).
    /// </summary>
    [Fact]
    public async Task GetSetupForACountryThatDoesNotExistHasANullNameAndTheBareZone()
    {
        await Factory.SeedAdminSetupsWithIdsAsync(AdminTestData.Setup(90, countryId: 4242, timeZone: "Europe/Nowhere"));

        var response = await Client().GetAsync(GetSetupUrl(AdminTestData.UserId));
        var json = await response.Content.ReadAsStringAsync();
        var setup = await OkDataAsync<AdminResponse>(response);

        Assert.Null(setup.CountryName);
        Assert.Equal("Europe/Nowhere", setup.CountryWithTimeZone);
        Assert.Contains("\"countryName\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"updatedBy\":null", json, StringComparison.Ordinal);
    }

    /// <summary>The route carries :min(1), so a 0 matches no endpoint at all.</summary>
    [Fact]
    public async Task GetSetupWithANonPositiveUserIdMatchesNoRoute()
    {
        var response = await Client().GetAsync(GetSetupUrl(0));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ================================================================ delete-timesheet-setup

    [Fact]
    public async Task DeleteSoftDeletesTheSetupAndStampsWhoAndWhen()
    {
        await Factory.SeedAdminSetupsWithIdsAsync(AdminTestData.Setup(100));

        var response = await PostAsync(DeleteSetupUrl, AdminTestData.DeleteRequest(100));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadEnvelopeAsync<object>()).Success);

        var row = await Factory.FindAdminSetupAsync(100);
        Assert.NotNull(row);
        Assert.True(row!.IsDelete);
        Assert.Equal(Factory.Clock.UtcNow, row.DeleteDate);
        Assert.Equal(AdminTestData.ActingUserId, row.DeletedBy);

        // Gone from the read path, which now answers "no setup".
        var envelope = await (await Client().GetAsync(GetSetupUrl(AdminTestData.UserId))).ReadEnvelopeAsync<AdminResponse>();
        Assert.Null(envelope.Data);
    }

    /// <summary>
    /// Every email still waiting to go to the user - Pending or Error - is
    /// closed as Sent with "Setup deleted" and no SentDate. One mid-send
    /// (Processing) or already Sent is left alone, as is anyone else's.
    /// </summary>
    [Fact]
    public async Task DeleteClosesTheUsersUnsentEmailsOnly()
    {
        const byte pending = Constants.EmailQueue.Status.Pending;
        const byte error = Constants.EmailQueue.Status.Error;
        const byte processing = Constants.EmailQueue.Status.Processing;
        const byte sent = Constants.EmailQueue.Status.Sent;

        await Factory.SeedAdminSetupsWithIdsAsync(AdminTestData.Setup(110));
        await Factory.SeedAdminEmailsAsync(
            AdminTestData.Email(AdminTestData.UserId, pending),
            AdminTestData.Email(AdminTestData.UserId, error, "SMTP timeout"),
            AdminTestData.Email(AdminTestData.UserId, processing),
            AdminTestData.Email(AdminTestData.UserId, sent),
            AdminTestData.Email(AdminTestData.OtherUserId, pending));

        var response = await PostAsync(DeleteSetupUrl, AdminTestData.DeleteRequest(110));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var emails = await Factory.FindAdminEmailsForUserAsync(AdminTestData.UserId);
        Assert.Equal(4, emails.Count);

        // Matched on content rather than position: the seeded Pending and Error
        // rows are the only two that can carry "Setup deleted" afterwards.
        var closed = emails.Where(email => email.ErrorMessage == "Setup deleted").ToList();
        Assert.Equal(2, closed.Count);
        Assert.All(closed, email =>
        {
            Assert.Equal(sent, email.EmailStatusId);
            Assert.Null(email.SentDate);
        });

        Assert.DoesNotContain(emails, email => email.ErrorMessage == "SMTP timeout");
        Assert.Single(emails, email => email.EmailStatusId == processing && email.ErrorMessage == null);
        Assert.Single(emails, email => email.EmailStatusId == sent && email.ErrorMessage == null);

        var others = Assert.Single(await Factory.FindAdminEmailsForUserAsync(AdminTestData.OtherUserId));
        Assert.Equal(pending, others.EmailStatusId);
        Assert.Null(others.ErrorMessage);
    }

    /// <summary>Deleting twice is not a silent success, and the first delete's stamp survives.</summary>
    [Fact]
    public async Task DeletingASetupTwiceIsNotFoundAndKeepsTheFirstStamp()
    {
        await Factory.SeedAdminSetupsWithIdsAsync(AdminTestData.Setup(120));

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(DeleteSetupUrl, AdminTestData.DeleteRequest(120))).StatusCode);

        await AssertNotFoundAsync(await PostAsync(DeleteSetupUrl, AdminTestData.DeleteRequest(120, deletedBy: 99)));

        var row = await Factory.FindAdminSetupAsync(120);
        Assert.Equal(AdminTestData.ActingUserId, row!.DeletedBy);
    }

    [Fact]
    public async Task DeletingASetupThatWasSeededDeletedIsNotFound()
    {
        await Factory.SeedAdminSetupsWithIdsAsync(AdminTestData.Setup(125, isDelete: true));

        await AssertNotFoundAsync(await PostAsync(DeleteSetupUrl, AdminTestData.DeleteRequest(125)));

        Assert.Equal(AdminTestData.OriginalCreatorId, (await Factory.FindAdminSetupAsync(125))!.DeletedBy);
    }

    [Fact]
    public async Task DeletingASetupThatDoesNotExistIsNotFound()
    {
        await AssertNotFoundAsync(await PostAsync(DeleteSetupUrl, AdminTestData.DeleteRequest(4040)));
    }

    [Fact]
    public async Task DeleteWithNonPositiveIdsIsABadRequestNamingBoth()
    {
        await Factory.SeedAdminSetupsWithIdsAsync(AdminTestData.Setup(130));

        await AssertBadRequestNamingAsync(
            await PostAsync(DeleteSetupUrl, AdminTestData.DeleteRequest(0, deletedBy: 0)),
            nameof(AdminDeleteRequest.SetupId),
            nameof(AdminDeleteRequest.DeletedBy));

        Assert.False((await Factory.FindAdminSetupAsync(130))!.IsDelete);
    }

    // ================================================================ get-admin-dashboard-summary-by-orgID

    [Fact]
    public async Task DashboardForAnEmptyOrganizationIsAllZeros()
    {
        var summary = await GetOkAsync<AdminDashboardSummaryResponse>(DashboardUrl(AdminTestData.OrganizationId));

        Assert.Equal(AdminTestData.OrganizationId, summary.OrganizationId);
        Assert.Equal(0, summary.TotalEmployees);
        Assert.Equal(0, summary.TotalFullTimeEmployees);
        Assert.Equal(0, summary.TotalPartTimeEmployees);
        Assert.Equal(0, summary.WeekLoggedMinutes);
        Assert.Equal("0h", summary.WeekLoggedText);
        Assert.Equal(0, summary.WeekExpectedMinutes);
        Assert.Equal("0h", summary.WeekExpectedText);
        Assert.Equal(0, summary.PendingTimesheets);
        Assert.Equal(0, summary.ApprovedTimesheets);
    }

    /// <summary>
    /// Everyone live in the organisation is counted; full and part time come
    /// only from live setups, so someone with no setup or a deleted one is in
    /// the total but in neither split.
    /// </summary>
    [Fact]
    public async Task DashboardCountsTheOrganizationsPeopleAndSplitsThemByLiveContract()
    {
        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(AdminTestData.UserId, organizationId: AdminTestData.OrganizationId),
            EmployeeListTestData.Employee(AdminTestData.OtherUserId, organizationId: AdminTestData.OrganizationId),
            EmployeeListTestData.Employee(AdminTestData.ThirdUserId, organizationId: AdminTestData.OrganizationId),
            EmployeeListTestData.Employee(AdminTestData.FourthUserId, organizationId: AdminTestData.OrganizationId),
            EmployeeListTestData.Employee(6005, organizationId: AdminTestData.OrganizationId, isDelete: true),
            EmployeeListTestData.Employee(6006, organizationId: AdminTestData.OtherOrganizationId));

        await Factory.SeedSetupsAsync(
            AdminTestData.DashboardSetup(AdminTestData.UserId, AdminTestData.FullTime),
            AdminTestData.DashboardSetup(AdminTestData.OtherUserId, AdminTestData.PartTime),
            AdminTestData.DashboardSetup(AdminTestData.FourthUserId, AdminTestData.FullTime, isDelete: true),
            AdminTestData.DashboardSetup(6005, AdminTestData.FullTime),
            AdminTestData.DashboardSetup(6006, AdminTestData.FullTime));

        var summary = await GetOkAsync<AdminDashboardSummaryResponse>(DashboardUrl(AdminTestData.OrganizationId));

        Assert.Equal(4, summary.TotalEmployees);
        Assert.Equal(1, summary.TotalFullTimeEmployees);
        Assert.Equal(1, summary.TotalPartTimeEmployees);
    }

    /// <summary>
    /// Expected time is working days x daily minutes, summed. Mon-Fri at
    /// 08:00 + 00:30 is 5 x 510 = 2550; Sat-Tue (wrapping past Sunday) at 08:00
    /// is 4 x 480 = 1920; nobody else contributes. 4470 minutes = 74h 30m.
    /// </summary>
    [Fact]
    public async Task DashboardSumsExpectedTimeIncludingAWeekThatWrapsPastSunday()
    {
        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(AdminTestData.UserId, organizationId: AdminTestData.OrganizationId),
            EmployeeListTestData.Employee(AdminTestData.OtherUserId, organizationId: AdminTestData.OrganizationId),
            EmployeeListTestData.Employee(AdminTestData.ThirdUserId, organizationId: AdminTestData.OrganizationId));

        await Factory.SeedSetupsAsync(
            AdminTestData.DashboardSetup(AdminTestData.UserId, AdminTestData.FullTime, maxTimInMins: TimeSpan.FromMinutes(30)),
            AdminTestData.DashboardSetup(
                AdminTestData.OtherUserId,
                AdminTestData.PartTime,
                startDay: Constants.DayMaster.DayId.Saturday,
                endDay: Constants.DayMaster.DayId.Tuesday));

        var summary = await GetOkAsync<AdminDashboardSummaryResponse>(DashboardUrl(AdminTestData.OrganizationId));

        Assert.Equal(4470, summary.WeekExpectedMinutes);
        Assert.Equal("74h 30m", summary.WeekExpectedText);
    }

    /// <summary>
    /// Logged time is every live entry inside each person's own week (Monday
    /// for both here: one has a Monday setup, the other none). The week comes
    /// from the server, because the procedure reads GETDATE(). Last week's
    /// entry, a deleted entry and another organisation's entry are all left out:
    /// 120 + 45 = 165 minutes = 2h 45m.
    /// </summary>
    [Fact]
    public async Task DashboardSumsTimeLoggedThisWeekAcrossTheOrganization()
    {
        var weekStart = await Factory.CurrentWeekStartAsync();

        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(AdminTestData.UserId, organizationId: AdminTestData.OrganizationId),
            EmployeeListTestData.Employee(AdminTestData.OtherUserId, organizationId: AdminTestData.OrganizationId),
            EmployeeListTestData.Employee(6006, organizationId: AdminTestData.OtherOrganizationId));

        await Factory.SeedSetupsAsync(AdminTestData.DashboardSetup(AdminTestData.UserId, AdminTestData.FullTime));

        var deleted = TimeLogTestData.Existing(userId: AdminTestData.UserId, startDate: weekStart, startTime: TimeSpan.FromHours(14), durationMinutes: 60);
        deleted.IsDeleted = 1;

        await Factory.SeedAsync(
            TimeLogTestData.Existing(userId: AdminTestData.UserId, startDate: weekStart, startTime: TimeSpan.FromHours(9), durationMinutes: 120),
            TimeLogTestData.Existing(userId: AdminTestData.OtherUserId, startDate: weekStart.AddDays(1), startTime: TimeSpan.FromHours(10), durationMinutes: 45),
            TimeLogTestData.Existing(userId: AdminTestData.UserId, startDate: weekStart.AddDays(-7), startTime: TimeSpan.FromHours(9), durationMinutes: 60),
            deleted,
            TimeLogTestData.Existing(userId: 6006, startDate: weekStart, startTime: TimeSpan.FromHours(9), durationMinutes: 300));

        var summary = await GetOkAsync<AdminDashboardSummaryResponse>(DashboardUrl(AdminTestData.OrganizationId));

        Assert.Equal(165, summary.WeekLoggedMinutes);
        Assert.Equal("2h 45m", summary.WeekLoggedText);
    }

    /// <summary>:int without :min(1), so a non-positive id reaches the service and is told what is wrong.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task DashboardWithANonPositiveOrgIdIsABadRequest(int orgId)
    {
        await AssertBadRequestNamingAsync(await Client().GetAsync(DashboardUrl(orgId)), "orgID");
    }

    // ================================================================ get-country-list-with-timezones

    [Fact]
    public async Task CountryListIsEmptyWhenThereAreNoCountries()
    {
        Assert.Empty(await GetOkAsync<List<CountryTimeZoneResponse>>(CountriesUrl));
    }

    /// <summary>
    /// One entry per zone, the country id repeated across a multi-zone
    /// country; countries by name, each country's zones in column order;
    /// zones trimmed and blanks dropped; a country with no zone absent.
    /// </summary>
    [Fact]
    public async Task CountryListHasOneEntryPerZoneOrderedByCountryName()
    {
        await Factory.SeedAdminCountriesAsync(
            AdminTestData.Country(236, "United States", "America/New_York,America/Chicago,America/Los_Angeles"),
            AdminTestData.Country(AdminTestData.IndiaCountryId, AdminTestData.IndiaName, AdminTestData.IndiaZone),
            AdminTestData.Country(112, "Japan", " Asia/Tokyo , ,"),
            AdminTestData.Country(500, "Atlantis", null),
            AdminTestData.Country(501, "Bouvet Island", "   "));

        var list = await GetOkAsync<List<CountryTimeZoneResponse>>(CountriesUrl);

        Assert.Equal(
            new[]
            {
                (AdminTestData.IndiaCountryId, "India", "Asia/Kolkata", "India-Asia/Kolkata"),
                (112, "Japan", "Asia/Tokyo", "Japan-Asia/Tokyo"),
                (236, "United States", "America/New_York", "United States-America/New_York"),
                (236, "United States", "America/Chicago", "United States-America/Chicago"),
                (236, "United States", "America/Los_Angeles", "United States-America/Los_Angeles"),
            },
            list.Select(entry => (entry.CountryId, entry.CountryName, entry.TimeZone, entry.OptionValue)));
    }

    /// <summary>
    /// The picker's label and the saved setup's label are the same string for
    /// the same pairing, so a screen can preselect by plain comparison.
    /// </summary>
    [Fact]
    public async Task CountryListOptionValueMatchesTheSavedSetupsCountryWithTimeZone()
    {
        await Factory.SeedAdminCountriesAsync(AdminTestData.Country(236, "United States", "America/New_York,America/Chicago"));

        var option = (await GetOkAsync<List<CountryTimeZoneResponse>>(CountriesUrl))[1];

        await PostOkAsync<AdminSaveRequest, AdminResponse>(
            SaveSetupUrl,
            AdminTestData.SaveRequest(countryId: option.CountryId, timeZone: option.TimeZone));

        var setup = await GetOkAsync<AdminResponse>(GetSetupUrl(AdminTestData.UserId));

        Assert.Equal(option.OptionValue, setup.CountryWithTimeZone);
        Assert.Equal("United States-America/Chicago", setup.CountryWithTimeZone);
    }

    // ================================================================ get-user-role-list-by-orgid

    [Fact]
    public async Task RoleListIsEmptyForAnOrganizationWithNoRoles()
    {
        Assert.Empty(await GetOkAsync<List<UserRoleResponse>>(RoleListUrl(AdminTestData.OrganizationId)));
    }

    /// <summary>
    /// Only the organisation's live roles - IsDelete 0 or NULL - ordered by
    /// name and then id.
    /// </summary>
    [Fact]
    public async Task RoleListReturnsTheOrganizationsLiveRolesOrderedByNameThenId()
    {
        await Factory.SeedAdminRolesAsync(
            AdminTestData.Role(5, "Manager"),
            AdminTestData.Role(3, "Admin"),
            AdminTestData.Role(4, "Admin", isDelete: null),
            AdminTestData.Role(2, "Manager"),
            AdminTestData.Role(6, "Auditor", isDelete: true),
            AdminTestData.Role(7, "Contractor", organizationId: AdminTestData.OtherOrganizationId),
            AdminTestData.Role(8, "Orphan", organizationId: null));

        var roles = await GetOkAsync<List<UserRoleResponse>>(RoleListUrl(AdminTestData.OrganizationId));

        Assert.Equal(
            new[] { (3, "Admin"), (4, "Admin"), (2, "Manager"), (5, "Manager") },
            roles.Select(role => (role.RoleId, role.RoleName ?? string.Empty)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RoleListWithANonPositiveOrgIdIsABadRequest(int orgId)
    {
        await AssertBadRequestNamingAsync(await Client().GetAsync(RoleListUrl(orgId)), "orgID");
    }

    // ================================================================ add-edit-user-role

    [Fact]
    public async Task AddingARoleStoresALiveTrimmedRowAndReturnsIt()
    {
        var saved = await PostOkAsync<UserRoleSaveRequest, UserRoleResponse>(
            SaveRoleUrl,
            AdminTestData.RoleSaveRequest("  Reviewer  "));

        Assert.Equal("Reviewer", saved.RoleName);

        var row = Assert.Single(await Factory.FindAdminRolesAsync(AdminTestData.OrganizationId));
        Assert.Equal(saved.RoleId, row.RoleId);
        Assert.Equal("Reviewer", row.RoleName);
        Assert.False(row.IsDelete);
        Assert.Equal(AdminTestData.ActingUserId, row.CreatedBy);
        Assert.Equal(Factory.Clock.UtcNow, row.CreateDate);
        Assert.Null(row.DeletedDate);
        Assert.Null(row.DeletedBy);

        var listed = Assert.Single(await GetOkAsync<List<UserRoleResponse>>(RoleListUrl(AdminTestData.OrganizationId)));
        Assert.Equal(saved.RoleId, listed.RoleId);
    }

    /// <summary>RoleId 0 means "add", exactly like leaving it out.</summary>
    [Fact]
    public async Task ARoleIdOfZeroAddsANewRole()
    {
        await Factory.SeedAdminRolesAsync(AdminTestData.Role(10, "Manager"));

        var saved = await PostOkAsync<UserRoleSaveRequest, UserRoleResponse>(
            SaveRoleUrl,
            AdminTestData.RoleSaveRequest("Reviewer", roleId: 0));

        Assert.NotEqual(10, saved.RoleId);
        Assert.Equal(2, (await Factory.FindAdminRolesAsync(AdminTestData.OrganizationId)).Count);
        Assert.Equal("Manager", (await Factory.FindAdminRoleAsync(10))!.RoleName);
    }

    /// <summary>An edit renames only; who created the role and when are untouched.</summary>
    [Fact]
    public async Task EditingARoleRenamesItAndKeepsItsCreator()
    {
        await Factory.SeedAdminRolesAsync(AdminTestData.Role(10, "Manager"));

        var saved = await PostOkAsync<UserRoleSaveRequest, UserRoleResponse>(
            SaveRoleUrl,
            AdminTestData.RoleSaveRequest("Team Lead", roleId: 10));

        Assert.Equal(10, saved.RoleId);
        Assert.Equal("Team Lead", saved.RoleName);

        var row = Assert.Single(await Factory.FindAdminRolesAsync(AdminTestData.OrganizationId));
        Assert.Equal("Team Lead", row.RoleName);
        Assert.Equal(AdminTestData.OriginalCreatorId, row.CreatedBy);
        Assert.Equal(AdminTestData.EarlierDate, row.CreateDate);
    }

    /// <summary>The role being edited is excluded from the duplicate check.</summary>
    [Fact]
    public async Task RenamingARoleToItsOwnNameIsAllowed()
    {
        await Factory.SeedAdminRolesAsync(AdminTestData.Role(10, "Manager"));

        var saved = await PostOkAsync<UserRoleSaveRequest, UserRoleResponse>(
            SaveRoleUrl,
            AdminTestData.RoleSaveRequest("Manager", roleId: 10));

        Assert.Equal(10, saved.RoleId);
    }

    [Fact]
    public async Task EditingAnotherOrganizationsRoleIsNotFoundAndLeavesItAlone()
    {
        await Factory.SeedAdminRolesAsync(AdminTestData.Role(20, "Contractor", organizationId: AdminTestData.OtherOrganizationId));

        await AssertNotFoundAsync(await PostAsync(SaveRoleUrl, AdminTestData.RoleSaveRequest("Hijacked", roleId: 20)));

        Assert.Equal("Contractor", (await Factory.FindAdminRoleAsync(20))!.RoleName);
        Assert.Empty(await Factory.FindAdminRolesAsync(AdminTestData.OrganizationId));
    }

    [Fact]
    public async Task EditingADeletedRoleIsNotFoundAndLeavesItDeleted()
    {
        await Factory.SeedAdminRolesAsync(AdminTestData.Role(21, "Auditor", isDelete: true));

        await AssertNotFoundAsync(await PostAsync(SaveRoleUrl, AdminTestData.RoleSaveRequest("Revived", roleId: 21)));

        var row = await Factory.FindAdminRoleAsync(21);
        Assert.True(row!.IsDelete);
        Assert.Equal("Auditor", row.RoleName);
    }

    [Fact]
    public async Task EditingARoleThatDoesNotExistIsNotFoundAndAddsNothing()
    {
        await AssertNotFoundAsync(await PostAsync(SaveRoleUrl, AdminTestData.RoleSaveRequest("Ghost", roleId: 999)));

        Assert.Empty(await Factory.FindAdminRolesAsync(AdminTestData.OrganizationId));
    }

    /// <summary>
    /// A second live role of the same name in one organisation is a conflict,
    /// compared after trimming and - under the database's case-insensitive
    /// collation - ignoring case. Adding and renaming are both refused.
    /// </summary>
    [Fact]
    public async Task ADuplicateRoleNameInTheOrganizationIsAConflict()
    {
        await Factory.SeedAdminRolesAsync(
            AdminTestData.Role(30, "Manager"),
            AdminTestData.Role(31, "Reviewer"));

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await PostAsync(SaveRoleUrl, AdminTestData.RoleSaveRequest("  manager "))).StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await PostAsync(SaveRoleUrl, AdminTestData.RoleSaveRequest("Manager", roleId: 31))).StatusCode);

        var rows = await Factory.FindAdminRolesAsync(AdminTestData.OrganizationId);
        Assert.Equal(new[] { "Manager", "Reviewer" }, rows.Select(role => role.RoleName));
    }

    /// <summary>The name only has to be unique among the organisation's live roles.</summary>
    [Fact]
    public async Task ARoleNameUsedByADeletedRoleOrAnotherOrganizationIsFree()
    {
        await Factory.SeedAdminRolesAsync(
            AdminTestData.Role(40, "Manager", isDelete: true),
            AdminTestData.Role(41, "Manager", organizationId: AdminTestData.OtherOrganizationId));

        var saved = await PostOkAsync<UserRoleSaveRequest, UserRoleResponse>(
            SaveRoleUrl,
            AdminTestData.RoleSaveRequest("Manager"));

        Assert.Equal("Manager", saved.RoleName);
        Assert.Equal(2, (await Factory.FindAdminRolesAsync(AdminTestData.OrganizationId)).Count);
    }

    /// <summary>Every failing field is reported in one 400, and nothing is written.</summary>
    [Fact]
    public async Task SavingARoleWithAnInvalidPayloadIsABadRequestNamingEveryField()
    {
        var response = await PostAsync(
            SaveRoleUrl,
            AdminTestData.RoleSaveRequest("   ", roleId: -1, organizationId: 0, createdBy: 0));

        await AssertBadRequestNamingAsync(
            response,
            nameof(UserRoleSaveRequest.RoleId),
            nameof(UserRoleSaveRequest.OrganizationId),
            nameof(UserRoleSaveRequest.RoleName),
            nameof(UserRoleSaveRequest.CreatedBy));

        Assert.Empty(await Factory.FindAdminRolesAsync(0));
    }

    /// <summary>Rolename is nvarchar(50); the limit applies after trimming.</summary>
    [Fact]
    public async Task ARoleNameLongerThanFiftyCharactersIsABadRequest()
    {
        await PostOkAsync<UserRoleSaveRequest, UserRoleResponse>(
            SaveRoleUrl,
            AdminTestData.RoleSaveRequest("  " + new string('a', 50) + "  "));

        await AssertBadRequestNamingAsync(
            await PostAsync(SaveRoleUrl, AdminTestData.RoleSaveRequest(new string('b', 51))),
            nameof(UserRoleSaveRequest.RoleName));

        Assert.Single(await Factory.FindAdminRolesAsync(AdminTestData.OrganizationId));
    }

    // ================================================================ delete-user-role

    [Fact]
    public async Task DeletingARoleSoftDeletesItAndDropsItFromTheList()
    {
        await Factory.SeedAdminRolesAsync(
            AdminTestData.Role(50, "Manager"),
            AdminTestData.Role(51, "Reviewer"));

        var response = await PostAsync(DeleteRoleUrl, AdminTestData.RoleDeleteRequest(50));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadEnvelopeAsync<object>()).Success);

        var row = await Factory.FindAdminRoleAsync(50);
        Assert.NotNull(row);
        Assert.True(row!.IsDelete);
        Assert.Equal(Factory.Clock.UtcNow, row.DeletedDate);
        Assert.Equal(AdminTestData.ActingUserId, row.DeletedBy);
        Assert.Equal("Manager", row.RoleName);

        var listed = Assert.Single(await GetOkAsync<List<UserRoleResponse>>(RoleListUrl(AdminTestData.OrganizationId)));
        Assert.Equal(51, listed.RoleId);
    }

    /// <summary>A role whose flag was never set (NULL) is live, so it can be deleted.</summary>
    [Fact]
    public async Task ARoleWithANullDeleteFlagCanBeDeleted()
    {
        await Factory.SeedAdminRolesAsync(AdminTestData.Role(52, "Manager", isDelete: null));

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(DeleteRoleUrl, AdminTestData.RoleDeleteRequest(52))).StatusCode);

        Assert.True((await Factory.FindAdminRoleAsync(52))!.IsDelete);
    }

    [Fact]
    public async Task DeletingAnotherOrganizationsRoleIsNotFoundAndLeavesItLive()
    {
        await Factory.SeedAdminRolesAsync(AdminTestData.Role(53, "Contractor", organizationId: AdminTestData.OtherOrganizationId));

        await AssertNotFoundAsync(await PostAsync(DeleteRoleUrl, AdminTestData.RoleDeleteRequest(53)));

        var row = await Factory.FindAdminRoleAsync(53);
        Assert.False(row!.IsDelete);
        Assert.Null(row.DeletedBy);
    }

    [Fact]
    public async Task DeletingARoleTwiceIsNotFoundAndKeepsTheFirstStamp()
    {
        await Factory.SeedAdminRolesAsync(AdminTestData.Role(54, "Manager"));

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(DeleteRoleUrl, AdminTestData.RoleDeleteRequest(54))).StatusCode);

        await AssertNotFoundAsync(await PostAsync(DeleteRoleUrl, AdminTestData.RoleDeleteRequest(54, deletedBy: 99)));

        Assert.Equal(AdminTestData.ActingUserId, (await Factory.FindAdminRoleAsync(54))!.DeletedBy);
    }

    [Fact]
    public async Task DeletingARoleThatDoesNotExistIsNotFound()
    {
        await AssertNotFoundAsync(await PostAsync(DeleteRoleUrl, AdminTestData.RoleDeleteRequest(999)));
    }

    [Fact]
    public async Task DeletingARoleWithNonPositiveIdsIsABadRequestNamingEveryField()
    {
        await AssertBadRequestNamingAsync(
            await PostAsync(DeleteRoleUrl, AdminTestData.RoleDeleteRequest(0, organizationId: 0, deletedBy: 0)),
            nameof(UserRoleDeleteRequest.RoleId),
            nameof(UserRoleDeleteRequest.OrganizationId),
            nameof(UserRoleDeleteRequest.DeletedBy));
    }

    // ================================================================ get-timesheet-project-category-by-orgID

    [Fact]
    public async Task CategoryListIsEmptyForAnOrganizationWithNoCategories()
    {
        Assert.Empty(await GetOkAsync<List<ProjectCategoryResponse>>(CategoryListUrl(AdminTestData.OrganizationId)));
    }

    /// <summary>Only the organisation's live categories - IsDelete 0 or NULL - ordered by name and then id.</summary>
    [Fact]
    public async Task CategoryListReturnsTheOrganizationsLiveCategoriesOrderedByNameThenId()
    {
        await Factory.SeedAdminCategoriesAsync(
            AdminTestData.Category(5, "Design"),
            AdminTestData.Category(3, "Development"),
            AdminTestData.Category(2, "Design", isDelete: null),
            AdminTestData.Category(6, "Archived", isDelete: true),
            AdminTestData.Category(7, "Marketing", organizationId: AdminTestData.OtherOrganizationId));

        var categories = await GetOkAsync<List<ProjectCategoryResponse>>(CategoryListUrl(AdminTestData.OrganizationId));

        Assert.Equal(
            new[] { (2, "Design"), (5, "Design"), (3, "Development") },
            categories.Select(category => (category.CategoryId, category.CategoryName ?? string.Empty)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CategoryListWithANonPositiveOrgIdIsABadRequest(int orgId)
    {
        await AssertBadRequestNamingAsync(await Client().GetAsync(CategoryListUrl(orgId)), "orgID");
    }

    // ================================================================ add-edit-timesheet-project-category

    [Fact]
    public async Task AddingACategoryStoresALiveTrimmedRowAndReturnsIt()
    {
        var saved = await PostOkAsync<ProjectCategorySaveRequest, ProjectCategoryResponse>(
            SaveCategoryUrl,
            AdminTestData.CategorySaveRequest("  Research  "));

        Assert.Equal("Research", saved.CategoryName);

        var row = Assert.Single(await Factory.FindAdminCategoriesAsync(AdminTestData.OrganizationId));
        Assert.Equal(saved.CategoryId, row.CategoryId);
        Assert.Equal("Research", row.CategoryName);
        Assert.False(row.IsDelete);
        Assert.Equal(AdminTestData.ActingUserId, row.CreatedBy);
        Assert.Equal(Factory.Clock.UtcNow, row.CreateDate);
        Assert.Null(row.DeletedDate);
        Assert.Null(row.DeletedBy);
    }

    [Fact]
    public async Task ACategoryIdOfZeroAddsANewCategory()
    {
        await Factory.SeedAdminCategoriesAsync(AdminTestData.Category(10, "Design"));

        var saved = await PostOkAsync<ProjectCategorySaveRequest, ProjectCategoryResponse>(
            SaveCategoryUrl,
            AdminTestData.CategorySaveRequest("Research", categoryId: 0));

        Assert.NotEqual(10, saved.CategoryId);
        Assert.Equal(2, (await Factory.FindAdminCategoriesAsync(AdminTestData.OrganizationId)).Count);
        Assert.Equal("Design", (await Factory.FindAdminCategoryAsync(10))!.CategoryName);
    }

    [Fact]
    public async Task EditingACategoryRenamesItAndKeepsItsCreator()
    {
        await Factory.SeedAdminCategoriesAsync(AdminTestData.Category(10, "Design"));

        var saved = await PostOkAsync<ProjectCategorySaveRequest, ProjectCategoryResponse>(
            SaveCategoryUrl,
            AdminTestData.CategorySaveRequest("UX Design", categoryId: 10));

        Assert.Equal(10, saved.CategoryId);
        Assert.Equal("UX Design", saved.CategoryName);

        var row = Assert.Single(await Factory.FindAdminCategoriesAsync(AdminTestData.OrganizationId));
        Assert.Equal("UX Design", row.CategoryName);
        Assert.Equal(AdminTestData.OriginalCreatorId, row.CreatedBy);
        Assert.Equal(AdminTestData.EarlierDate, row.CreateDate);
    }

    [Fact]
    public async Task EditingAnotherOrganizationsCategoryIsNotFoundAndLeavesItAlone()
    {
        await Factory.SeedAdminCategoriesAsync(AdminTestData.Category(20, "Marketing", organizationId: AdminTestData.OtherOrganizationId));

        await AssertNotFoundAsync(await PostAsync(SaveCategoryUrl, AdminTestData.CategorySaveRequest("Hijacked", categoryId: 20)));

        Assert.Equal("Marketing", (await Factory.FindAdminCategoryAsync(20))!.CategoryName);
        Assert.Empty(await Factory.FindAdminCategoriesAsync(AdminTestData.OrganizationId));
    }

    [Fact]
    public async Task EditingADeletedCategoryIsNotFoundAndLeavesItDeleted()
    {
        await Factory.SeedAdminCategoriesAsync(AdminTestData.Category(21, "Archived", isDelete: true));

        await AssertNotFoundAsync(await PostAsync(SaveCategoryUrl, AdminTestData.CategorySaveRequest("Revived", categoryId: 21)));

        var row = await Factory.FindAdminCategoryAsync(21);
        Assert.True(row!.IsDelete);
        Assert.Equal("Archived", row.CategoryName);
    }

    [Fact]
    public async Task EditingACategoryThatDoesNotExistIsNotFoundAndAddsNothing()
    {
        await AssertNotFoundAsync(await PostAsync(SaveCategoryUrl, AdminTestData.CategorySaveRequest("Ghost", categoryId: 999)));

        Assert.Empty(await Factory.FindAdminCategoriesAsync(AdminTestData.OrganizationId));
    }

    [Fact]
    public async Task SavingACategoryWithAnInvalidPayloadIsABadRequestNamingEveryField()
    {
        var response = await PostAsync(
            SaveCategoryUrl,
            AdminTestData.CategorySaveRequest(null, categoryId: -1, organizationId: 0, createdBy: 0));

        await AssertBadRequestNamingAsync(
            response,
            nameof(ProjectCategorySaveRequest.CategoryId),
            nameof(ProjectCategorySaveRequest.OrganizationId),
            nameof(ProjectCategorySaveRequest.CategoryName),
            nameof(ProjectCategorySaveRequest.CreatedBy));

        Assert.Empty(await Factory.FindAdminCategoriesAsync(0));
    }

    /// <summary>Categoryname is nvarchar(50); the limit applies after trimming.</summary>
    [Fact]
    public async Task ACategoryNameLongerThanFiftyCharactersIsABadRequest()
    {
        await PostOkAsync<ProjectCategorySaveRequest, ProjectCategoryResponse>(
            SaveCategoryUrl,
            AdminTestData.CategorySaveRequest("  " + new string('a', 50) + "  "));

        await AssertBadRequestNamingAsync(
            await PostAsync(SaveCategoryUrl, AdminTestData.CategorySaveRequest(new string('b', 51))),
            nameof(ProjectCategorySaveRequest.CategoryName));

        Assert.Single(await Factory.FindAdminCategoriesAsync(AdminTestData.OrganizationId));
    }

    // ================================================================ remove-timesheet-project-category

    [Fact]
    public async Task RemovingACategorySoftDeletesItAndDropsItFromTheList()
    {
        await Factory.SeedAdminCategoriesAsync(
            AdminTestData.Category(50, "Design"),
            AdminTestData.Category(51, "Research"));

        var response = await PostAsync(RemoveCategoryUrl, AdminTestData.CategoryDeleteRequest(50));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadEnvelopeAsync<object>()).Success);

        var row = await Factory.FindAdminCategoryAsync(50);
        Assert.NotNull(row);
        Assert.True(row!.IsDelete);
        Assert.Equal(Factory.Clock.UtcNow, row.DeletedDate);
        Assert.Equal(AdminTestData.ActingUserId, row.DeletedBy);
        Assert.Equal("Design", row.CategoryName);

        var listed = Assert.Single(await GetOkAsync<List<ProjectCategoryResponse>>(CategoryListUrl(AdminTestData.OrganizationId)));
        Assert.Equal(51, listed.CategoryId);
    }

    [Fact]
    public async Task ACategoryWithANullDeleteFlagCanBeRemoved()
    {
        await Factory.SeedAdminCategoriesAsync(AdminTestData.Category(52, "Design", isDelete: null));

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(RemoveCategoryUrl, AdminTestData.CategoryDeleteRequest(52))).StatusCode);

        Assert.True((await Factory.FindAdminCategoryAsync(52))!.IsDelete);
    }

    [Fact]
    public async Task RemovingAnotherOrganizationsCategoryIsNotFoundAndLeavesItLive()
    {
        await Factory.SeedAdminCategoriesAsync(AdminTestData.Category(53, "Marketing", organizationId: AdminTestData.OtherOrganizationId));

        await AssertNotFoundAsync(await PostAsync(RemoveCategoryUrl, AdminTestData.CategoryDeleteRequest(53)));

        var row = await Factory.FindAdminCategoryAsync(53);
        Assert.False(row!.IsDelete);
        Assert.Null(row.DeletedBy);
    }

    [Fact]
    public async Task RemovingACategoryTwiceIsNotFoundAndKeepsTheFirstStamp()
    {
        await Factory.SeedAdminCategoriesAsync(AdminTestData.Category(54, "Design"));

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(RemoveCategoryUrl, AdminTestData.CategoryDeleteRequest(54))).StatusCode);

        await AssertNotFoundAsync(await PostAsync(RemoveCategoryUrl, AdminTestData.CategoryDeleteRequest(54, deletedBy: 99)));

        Assert.Equal(AdminTestData.ActingUserId, (await Factory.FindAdminCategoryAsync(54))!.DeletedBy);
    }

    [Fact]
    public async Task RemovingACategoryThatDoesNotExistIsNotFound()
    {
        await AssertNotFoundAsync(await PostAsync(RemoveCategoryUrl, AdminTestData.CategoryDeleteRequest(999)));
    }

    [Fact]
    public async Task RemovingACategoryWithNonPositiveIdsIsABadRequestNamingEveryField()
    {
        await AssertBadRequestNamingAsync(
            await PostAsync(RemoveCategoryUrl, AdminTestData.CategoryDeleteRequest(0, organizationId: 0, deletedBy: 0)),
            nameof(ProjectCategoryDeleteRequest.CategoryId),
            nameof(ProjectCategoryDeleteRequest.OrganizationId),
            nameof(ProjectCategoryDeleteRequest.DeletedBy));
    }
}
