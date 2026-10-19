using System.Net;
using System.Net.Http.Json;
using ETimeSheet.Application.Models;
using ETimeSheet.Shared.Enums;
using ETimeSheet.Shared.Utilities;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// The TimeLog endpoints, end to end against a real SQL Server: the recorded
/// <c>spc_GetTimeLoggedDetailsForTask</c>, <c>spc_GetTimesheetMasterSetupByUserID</c>
/// and <c>spc_GetUserDashboardSummaryByUserID</c>, the EF write path behind
/// save-employee-time-log, and the real middleware and validation.
/// <para>
/// <b>Two clocks.</b> The save and every rule it applies run on the API's
/// frozen clock - Tuesday 2026-03-10 12:00 UTC - so those tests use fixed dates.
/// The dashboard procedure reads <c>GETDATE()</c>, which nothing can freeze, so
/// its tests arrange rows against the server's own today and week start.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class TimeLogEndpointTests : IntegrationTestBase
{
    private const int UserId = TimeLogEndpointTestData.UserId;
    private const int OtherUserId = TimeLogEndpointTestData.OtherUserId;
    private const string SaveUrl = "/api/v1/TimeLog/save-employee-time-log";

    private static readonly DateTime Today = TimeLogEndpointTestData.Today;
    private static readonly DateTime Monday = TimeLogEndpointTestData.Monday;

    public TimeLogEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // Every TimeLog endpoint is [AllowAnonymous] while authentication is off.
    private HttpClient Client() => Factory.CreateClient();

    private static TimeSpan At(int hours, int minutes = 0) => TimeLogEndpointTestData.At(hours, minutes);

    private static string ListUrl(int userId) => $"/api/v1/TimeLog/get-logged-time-list/{userId}";

    private static string SetupUrl(int userId) => $"/api/v1/TimeLog/get-timesheet-setup-by-user/{userId}";

    private static string TaskListUrl(int userId) => $"/api/v1/TimeLog/get-user-task-list-by-userid/{userId}";

    private static string DashboardUrl(int userId) => $"/api/v1/TimeLog/get-user-dashboard-summary-by-userID/{userId}";

    private async Task<List<TimeLoggedDetailResponse>> ListAsync(int userId = UserId)
    {
        var response = await Client().GetAsync(ListUrl(userId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<List<TimeLoggedDetailResponse>>();
    }

    private Task<HttpResponseMessage> PostSaveAsync(TimeLogSaveRequest request) =>
        Client().PostAsJsonAsync(SaveUrl, request);

    private async Task<TimeLogResponse> SaveAsync(TimeLogSaveRequest request)
    {
        var response = await PostSaveAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<TimeLogResponse>();
    }

    private async Task<UserDashboardSummaryResponse> DashboardAsync(int userId = UserId)
    {
        var response = await Client().GetAsync(DashboardUrl(userId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<UserDashboardSummaryResponse>();
    }

    private static async Task AssertBadRequestNamingAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<object>();

        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors, error => error.StartsWith(field, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.False((await response.ReadEnvelopeAsync<object>()).Success);
    }

    // ================================================================ get-logged-time-list

    [Fact]
    public async Task LoggedTimeListIsEmptyForAUserWithNoEntries()
    {
        Assert.Empty(await ListAsync());
    }

    /// <summary>
    /// The procedure filters on <c>UserID</c> and <c>IsDeleted &lt;&gt; 1</c>:
    /// another user's entries and a soft-deleted one never come back.
    /// </summary>
    [Fact]
    public async Task LoggedTimeListHoldsOnlyTheUsersLiveEntries()
    {
        var seeded = await Factory.SeedAsync(
            TimeLogEndpointTestData.Entry(Today, At(9), At(10)),
            TimeLogEndpointTestData.Entry(Monday, At(9), At(10)),
            TimeLogEndpointTestData.Entry(Monday, At(13), At(14)),
            TimeLogEndpointTestData.Entry(Today, At(9), At(10), userId: OtherUserId));

        await Factory.SoftDeleteAsync(seeded[2].SheetId);

        var ids = (await ListAsync()).Select(entry => entry.SheetId).OrderBy(id => id);

        Assert.Equal(new[] { seeded[0].SheetId, seeded[1].SheetId }.OrderBy(id => id), ids);
    }

    /// <summary>
    /// Newest first by <c>CreateDate</c> - when the entry was logged - not by
    /// the day it was logged against. The dates are arranged opposite to the
    /// creation order so the two cannot be confused.
    /// </summary>
    [Fact]
    public async Task LoggedTimeListIsNewestLoggedFirst()
    {
        var seeded = await Factory.SeedAsync(
            TimeLogEndpointTestData.Entry(Today, At(9), At(10)),
            TimeLogEndpointTestData.Entry(Monday, At(9), At(10)),
            TimeLogEndpointTestData.Entry(TimeLogEndpointTestData.PreviousFriday, At(9), At(10)));

        await Factory.SetTimeLogCreateDateAsync(seeded[0].SheetId, new DateTime(2026, 3, 1, 8, 0, 0));
        await Factory.SetTimeLogCreateDateAsync(seeded[1].SheetId, new DateTime(2026, 3, 5, 8, 0, 0));
        await Factory.SetTimeLogCreateDateAsync(seeded[2].SheetId, new DateTime(2026, 3, 9, 8, 0, 0));

        Assert.Equal(
            new[] { seeded[2].SheetId, seeded[1].SheetId, seeded[0].SheetId },
            (await ListAsync()).Select(entry => entry.SheetId));
    }

    [Fact]
    public async Task LoggedTimeListCarriesEveryFieldAndTheComputedDuration()
    {
        var seeded = await Factory.SeedAsync(TimeLogEndpointTestData.Entry(
            Today, At(9), At(10, 30),
            sheetCode: "SHT0042",
            status: TimeLogStatus.Draft,
            isProjectTask: false,
            description: "Code review."));

        var entry = Assert.Single(await ListAsync());

        Assert.Equal(seeded[0].SheetId, entry.SheetId);
        Assert.Equal("SHT0042", entry.SheetCode);
        Assert.Equal(TimeLogEndpointTestData.TaskId, entry.TaskId);
        Assert.False(entry.IsProjectTask);
        Assert.Equal("Code review.", entry.Description);
        Assert.Equal(Today, entry.StartDate);
        Assert.Equal(At(9), entry.StartTime);
        Assert.Equal(Today, entry.EndDate);
        Assert.Equal(At(10, 30), entry.EndTime);
        Assert.Equal(TimeLogStatus.Draft, entry.Status);
        Assert.Equal("Draft", entry.StatusName);
        Assert.Equal(90, entry.TotalWorkingMinutes);
        Assert.Equal(1.5m, entry.TotalWorkingHours);
    }

    /// <summary>The duration is taken across both dates, so an overnight entry is not negative.</summary>
    [Fact]
    public async Task LoggedTimeListMeasuresAnOvernightEntryAcrossBothDates()
    {
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(Monday, At(22), At(2), endDate: Today));

        var entry = Assert.Single(await ListAsync());

        Assert.Equal(240, entry.TotalWorkingMinutes);
        Assert.Equal(4m, entry.TotalWorkingHours);
        Assert.Equal("Save", entry.StatusName);
    }

    /// <summary><c>{userId:int:min(1)}</c>: a non-positive id matches no route at all.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task LoggedTimeListRejectsANonPositiveUserIdAtTheRoute(int userId)
    {
        var response = await Client().GetAsync(ListUrl(userId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ================================================================ get-timesheet-setup-by-user

    /// <summary>No setup is a successful read with <c>data: null</c>, not a 404.</summary>
    [Fact]
    public async Task SetupIsNullDataWhenTheUserHasNoSetup()
    {
        await Factory.SeedEmployeesAsync(EmployeeListTestData.Employee(UserId));

        var response = await Client().GetAsync(SetupUrl(UserId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<TimesheetMasterSetupResponse>();
        Assert.True(envelope.Success);
        Assert.Null(envelope.Data);
        Assert.Equal("This user has no timesheet setup.", envelope.Message);
    }

    /// <summary>
    /// The procedure INNER JOINs <c>dbo.Signup</c>, so a setup whose user has no
    /// Signup row reads exactly as no setup.
    /// </summary>
    [Fact]
    public async Task SetupIsNullDataWhenTheUserHasNoSignupRow()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var envelope = await (await Client().GetAsync(SetupUrl(UserId)))
            .ReadEnvelopeAsync<TimesheetMasterSetupResponse>();

        Assert.True(envelope.Success);
        Assert.Null(envelope.Data);
    }

    /// <summary>
    /// Every column, bound through the procedure's aliases. <c>countryId</c> is
    /// <c>dbo.Signup</c>'s, not the setup's own <c>CountryID</c> - they are
    /// arranged to differ so the test can tell which one came back.
    /// </summary>
    [Fact]
    public async Task SetupReturnsTheUsersSettings()
    {
        await Factory.SeedEmployeesAsync(EmployeeListTestData.Employee(UserId));
        await Factory.SetSignupCountryAsync(UserId, MasterAdminTestData.IndiaCountryId);

        var setup = TimeLogEndpointTestData.Setup(
            maxTimeInHrs: At(8),
            maxTimInMins: At(0, 30),
            contractType: Constants.TimesheetMasterSetup.ContractType.PartTime,
            timeZone: "Asia/Kolkata");
        setup.CountryId = MasterAdminTestData.UnitedStatesCountryId;

        var seeded = await Factory.SeedSetupsAsync(setup);

        var result = await (await Client().GetAsync(SetupUrl(UserId)))
            .ReadDataAsync<TimesheetMasterSetupResponse>();

        Assert.Equal(seeded[0].SetupId, result.SetupId);
        Assert.Equal(At(8), result.MaxTimeLoggedByUserInHours);
        Assert.Equal(At(0, 30), result.MaxTimeLoggedByUserInMinutes);
        Assert.Equal(Constants.TimesheetMasterSetup.ContractType.PartTime, result.ContractType);
        Assert.Equal(Constants.DayMaster.DayId.Monday, result.StartDay);
        Assert.Equal(Constants.DayMaster.DayId.Friday, result.EndDay);
        Assert.Equal(MasterAdminTestData.IndiaCountryId, result.CountryId);
        Assert.Equal("Asia/Kolkata", result.TimeZone);
    }

    /// <summary>
    /// <c>CAST(GETDATE() AS TIME) &lt;= NULL</c> is UNKNOWN, so a setup with no
    /// cut-off reports 0 - documented in the procedure header.
    /// </summary>
    [Fact]
    public async Task SetupWithNoCutOffReportsPreDayLoggingAsNotAllowed()
    {
        await Factory.SeedEmployeesAsync(EmployeeListTestData.Employee(UserId));
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(timeEntryLockAt: null));

        var result = await (await Client().GetAsync(SetupUrl(UserId)))
            .ReadDataAsync<TimesheetMasterSetupResponse>();

        Assert.False(result.CanUserLoggedPreDayTime);
    }

    /// <summary>
    /// A cut-off of the last tick of the day is at or after every value
    /// <c>GETDATE()</c> can take, so the flag is 1 whatever the real time.
    /// </summary>
    [Fact]
    public async Task SetupWithACutOffNotYetReachedReportsPreDayLoggingAsAllowed()
    {
        await Factory.SeedEmployeesAsync(EmployeeListTestData.Employee(UserId));
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(
            timeEntryLockAt: TimeSpan.FromTicks(TimeSpan.TicksPerDay - 1)));

        var result = await (await Client().GetAsync(SetupUrl(UserId)))
            .ReadDataAsync<TimesheetMasterSetupResponse>();

        Assert.True(result.CanUserLoggedPreDayTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task SetupRejectsANonPositiveUserIdAtTheRoute(int userId)
    {
        var response = await Client().GetAsync(SetupUrl(userId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ================================================================ get-user-task-list-by-userid

    // Only the route is tested. spc_GetUsersTaskList reads dbo.TaskMaster and
    // dbo.SprintTaskManagement, neither of which is recorded in
    // docs/database/schema, so the procedure compiles in the container (name
    // resolution is deferred) but every call fails with "Invalid object name".
    // The happy path becomes testable once the owner supplies those two tables.

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TaskListRejectsANonPositiveUserIdAtTheRoute(int userId)
    {
        var response = await Client().GetAsync(TaskListUrl(userId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ================================================================ get-user-dashboard-summary-by-userID

    [Fact]
    public async Task DashboardIsNullDataForAUserWhoDoesNotExist()
    {
        var response = await Client().GetAsync(DashboardUrl(UserId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<UserDashboardSummaryResponse>();
        Assert.True(envelope.Success);
        Assert.Null(envelope.Data);
        Assert.Equal("This user was not found.", envelope.Message);
    }

    [Fact]
    public async Task DashboardIsNullDataForADeletedUser()
    {
        await Factory.SeedEmployeesAsync(EmployeeListTestData.Employee(UserId, isDelete: true));
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var envelope = await (await Client().GetAsync(DashboardUrl(UserId)))
            .ReadEnvelopeAsync<UserDashboardSummaryResponse>();

        Assert.True(envelope.Success);
        Assert.Null(envelope.Data);
    }

    /// <summary>
    /// No setup: the logged figures still come back, the week falls back to
    /// Monday, and expected / percentage / pending are null - present as keys,
    /// not omitted (CLAUDE.md §5).
    /// </summary>
    [Fact]
    public async Task DashboardWithoutASetupStillReportsLoggedTime()
    {
        var today = await Factory.ServerTodayAsync();
        var weekStart = await Factory.CurrentWeekStartAsync();

        await Factory.SeedEmployeesAsync(EmployeeListTestData.Employee(UserId, name: "Dana"));
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(today, At(9), At(10)));

        var response = await Client().GetAsync(DashboardUrl(UserId));
        var json = await response.Content.ReadAsStringAsync();
        var summary = await response.ReadDataAsync<UserDashboardSummaryResponse>();

        Assert.Equal(UserId, summary.UserId);
        Assert.Equal("Dana", summary.Name);
        Assert.Equal(weekStart, summary.WeekStartDate);
        Assert.Equal(weekStart.AddDays(6), summary.WeekEndDate);
        Assert.Equal(60, summary.TodayLoggedMinutes);
        Assert.Equal("1h", summary.TodayLoggedText);
        Assert.Equal(60, summary.WeekLoggedMinutes);
        Assert.Equal("1h", summary.WeekLoggedText);
        Assert.Null(summary.WeekExpectedMinutes);
        Assert.Null(summary.WeekExpectedText);
        Assert.Null(summary.WeekLoggedPercentage);
        Assert.Null(summary.WeekPendingMinutes);
        Assert.Null(summary.WeekPendingText);
        Assert.Contains("\"weekExpectedMinutes\":null", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Monday to Friday at 8 hours is 2400 expected minutes. Logged: 150 today
    /// plus a 60-minute draft on another day of the week = 210. A deleted entry
    /// and one from the previous week count for nothing.
    /// 210 / 2400 = 8.75%, leaving 2190 minutes (36h 30m).
    /// </summary>
    [Fact]
    public async Task DashboardReportsTodayTheWeekAndWhatIsStillPending()
    {
        var today = await Factory.ServerTodayAsync();
        var weekStart = await Factory.CurrentWeekStartAsync();
        var anotherDayThisWeek = today == weekStart ? weekStart.AddDays(1) : weekStart;

        await Factory.SeedEmployeesAsync(EmployeeListTestData.Employee(UserId));
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var seeded = await Factory.SeedAsync(
            TimeLogEndpointTestData.Entry(today, At(9), At(11, 30)),
            TimeLogEndpointTestData.Entry(anotherDayThisWeek, At(14), At(15), status: TimeLogStatus.Draft),
            TimeLogEndpointTestData.Entry(today, At(16), At(17)),
            TimeLogEndpointTestData.Entry(weekStart.AddDays(-7), At(9), At(11)),
            TimeLogEndpointTestData.Entry(today, At(9), At(10), userId: OtherUserId));

        await Factory.SoftDeleteAsync(seeded[2].SheetId);

        var summary = await DashboardAsync();

        Assert.Equal(150, summary.TodayLoggedMinutes);
        Assert.Equal("2h 30m", summary.TodayLoggedText);
        Assert.Equal(210, summary.WeekLoggedMinutes);
        Assert.Equal("3h 30m", summary.WeekLoggedText);
        Assert.Equal(2400, summary.WeekExpectedMinutes);
        Assert.Equal("40h", summary.WeekExpectedText);
        Assert.Equal(8.75m, summary.WeekLoggedPercentage);
        Assert.Equal(2190, summary.WeekPendingMinutes);
        Assert.Equal("36h 30m", summary.WeekPendingText);
    }

    /// <summary>
    /// A one-day week at one hour expects 60 minutes. Logging 90 caps the
    /// percentage at 100 and the pending time at zero.
    /// </summary>
    [Fact]
    public async Task DashboardCapsThePercentageAndNeverGoesBelowZeroPending()
    {
        var today = await Factory.ServerTodayAsync();

        await Factory.SeedEmployeesAsync(EmployeeListTestData.Employee(UserId));
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(
            startDay: Constants.DayMaster.DayId.Monday,
            endDay: Constants.DayMaster.DayId.Monday,
            maxTimeInHrs: At(1)));
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(today, At(9), At(10, 30)));

        var summary = await DashboardAsync();

        Assert.Equal(60, summary.WeekExpectedMinutes);
        Assert.Equal(90, summary.WeekLoggedMinutes);
        Assert.Equal(100m, summary.WeekLoggedPercentage);
        Assert.Equal(0, summary.WeekPendingMinutes);
        Assert.Equal("0h", summary.WeekPendingText);
    }

    /// <summary>
    /// The week is anchored on <c>StartDay</c>, not on Monday. Wednesday to
    /// Tuesday wraps past Sunday and is seven working days: 7 x 480 = 3360.
    /// </summary>
    [Fact]
    public async Task DashboardAnchorsTheWeekOnTheSetupsStartDay()
    {
        var today = await Factory.ServerTodayAsync();
        var daysSinceWednesday = (TimeLogEndpointTestData.DayIdOf(today) - Constants.DayMaster.DayId.Wednesday + 7) % 7;
        var weekStart = today.AddDays(-daysSinceWednesday);

        await Factory.SeedEmployeesAsync(EmployeeListTestData.Employee(UserId));
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(
            startDay: Constants.DayMaster.DayId.Wednesday,
            endDay: Constants.DayMaster.DayId.Tuesday));

        var summary = await DashboardAsync();

        Assert.Equal(weekStart, summary.WeekStartDate);
        Assert.Equal(weekStart.AddDays(6), summary.WeekEndDate);
        Assert.Equal(3360, summary.WeekExpectedMinutes);
        Assert.Equal("56h", summary.WeekExpectedText);
        Assert.Equal(0, summary.WeekLoggedMinutes);
        Assert.Equal("0h", summary.WeekLoggedText);
    }

    /// <summary>
    /// An entry running 22:00 the evening before the week to 02:00 on its first
    /// day counts only the two hours inside the week.
    /// </summary>
    [Fact]
    public async Task DashboardCountsOnlyThePartOfAnEntryInsideTheWeek()
    {
        var weekStart = await Factory.CurrentWeekStartAsync();

        await Factory.SeedEmployeesAsync(EmployeeListTestData.Employee(UserId));
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(
            weekStart.AddDays(-1), At(22), At(2), endDate: weekStart));

        var summary = await DashboardAsync();

        Assert.Equal(120, summary.WeekLoggedMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DashboardRejectsANonPositiveUserIdAtTheRoute(int userId)
    {
        var response = await Client().GetAsync(DashboardUrl(userId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ================================================================ save-employee-time-log: add

    [Fact]
    public async Task AddStoresTheEntryAndReturnsIt()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<TimeLogResponse>();
        Assert.True(envelope.Success);
        Assert.Equal("Time logged.", envelope.Message);

        var saved = envelope.Data!;
        Assert.Equal("SHT0001", saved.SheetCode);
        Assert.Equal(UserId, saved.UserId);
        Assert.Equal(TimeLogEndpointTestData.TaskId, saved.TaskId);
        Assert.True(saved.IsProjectTask);
        Assert.Equal(Today, saved.StartDate);
        Assert.Equal(Today, saved.EndDate);
        Assert.Equal(At(9), saved.StartTime);
        Assert.Equal(At(11), saved.EndTime);
        Assert.Equal(TimeLogStatus.Save, saved.Status);
        Assert.Equal("Save", saved.StatusName);
        Assert.Equal(2m, saved.TotalWorkingHours);
        Assert.Equal(TimeLogEndpointTestData.ActingUserId, saved.CreatedBy);
        Assert.Equal(Factory.Clock.UtcNow, saved.CreateDate);

        var row = Assert.Single(await Factory.TimeLogRowsAsync(UserId));
        Assert.Equal(saved.SheetId, row.SheetId);
        Assert.Equal("SHT0001", row.SheetCode);
        Assert.Equal(TimeLogEndpointTestData.TaskId, row.TaskId);
        Assert.True(row.IsProjectTask);
        Assert.Equal("Integration test entry.", row.Description);
        Assert.Equal(Today, row.StartDate);
        Assert.Equal(Today, row.EndDate);
        Assert.Equal(At(9), row.StartTime);
        Assert.Equal(At(11), row.EndTime);
        Assert.Equal(TimeLogStatus.Save, row.Status);
        Assert.Equal(TimeLogEndpointTestData.ActingUserId, row.CreatedBy);
        Assert.Equal(0, row.IsDeleted);
    }

    /// <summary>
    /// One code per user per timesheet week: Monday's and Tuesday's entries
    /// share <c>SHT0001</c>; the previous week's Friday opens <c>SHT0002</c>.
    /// </summary>
    [Fact]
    public async Task AddSharesOneSheetCodePerWeekAndOpensTheNextForANewWeek()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var tuesday = await SaveAsync(TimeLogEndpointTestData.SaveRequest(startDate: Today));
        var monday = await SaveAsync(TimeLogEndpointTestData.SaveRequest(startDate: Monday));
        var lastWeek = await SaveAsync(TimeLogEndpointTestData.SaveRequest(
            startDate: TimeLogEndpointTestData.PreviousFriday));

        Assert.Equal("SHT0001", tuesday.SheetCode);
        Assert.Equal("SHT0001", monday.SheetCode);
        Assert.Equal("SHT0002", lastWeek.SheetCode);
    }

    /// <summary>22:00 Monday to 02:00 Tuesday runs forwards once both dates count.</summary>
    [Fact]
    public async Task AddAcceptsAnOvernightEntry()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var saved = await SaveAsync(TimeLogEndpointTestData.SaveRequest(
            startDate: Monday, endDate: Today, startTime: "22:00:00", endTime: "02:00:00"));

        Assert.Equal(Monday, saved.StartDate);
        Assert.Equal(Today, saved.EndDate);
        Assert.Equal(4m, saved.TotalWorkingHours);
    }

    /// <summary>A user with no setup has no limits to check against, so cannot log time.</summary>
    [Fact]
    public async Task AddIsRefusedForAUserWithNoSetup()
    {
        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest());

        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty(await Factory.TimeLogRowsAsync(UserId));
    }

    /// <summary>A soft-deleted setup is hidden by the query filter, so it counts as none.</summary>
    [Fact]
    public async Task AddIsRefusedWhenTheOnlySetupIsDeleted()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(isDelete: true));

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest());

        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddIsRefusedForAFutureDate()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            startDate: TimeLogEndpointTestData.Tomorrow));

        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty(await Factory.TimeLogRowsAsync(UserId));
    }

    [Fact]
    public async Task AddIsRefusedOnADayOutsideTheWorkingWeek()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            startDate: TimeLogEndpointTestData.PreviousSunday));

        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
    }

    /// <summary>The setup's exception day is allowed in addition to the working week.</summary>
    [Fact]
    public async Task AddIsAllowedOnTheExceptionDay()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(
            exceptionDay: Constants.DayMaster.DayId.Sunday));

        var saved = await SaveAsync(TimeLogEndpointTestData.SaveRequest(
            startDate: TimeLogEndpointTestData.PreviousSunday));

        Assert.Equal(TimeLogEndpointTestData.PreviousSunday, saved.StartDate);
    }

    [Fact]
    public async Task AddIsAConflictWhenItOverlapsAnExistingEntry()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(Today, At(9), At(11)));

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            startTime: "10:00:00", endTime: "12:00:00"));

        await AssertStatusAsync(response, HttpStatusCode.Conflict);
        Assert.Single(await Factory.TimeLogRowsAsync(UserId));
    }

    /// <summary>Touching ends are adjacent, not overlapping - that is how a day is filled in.</summary>
    [Fact]
    public async Task AddIsAllowedRightAfterAnExistingEntry()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(Today, At(9), At(11)));

        await SaveAsync(TimeLogEndpointTestData.SaveRequest(startTime: "11:00:00", endTime: "12:00:00"));

        Assert.Equal(2, (await Factory.TimeLogRowsAsync(UserId)).Count);
    }

    /// <summary>Another user's entry at the same time is no conflict.</summary>
    [Fact]
    public async Task AddIgnoresOtherUsersEntriesWhenCheckingOverlap()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(Today, At(9), At(11), userId: OtherUserId));

        await SaveAsync(TimeLogEndpointTestData.SaveRequest());

        Assert.Single(await Factory.TimeLogRowsAsync(UserId));
    }

    /// <summary>Six hours already logged plus three more is nine, over the eight-hour maximum.</summary>
    [Fact]
    public async Task AddIsRefusedWhenItTakesTheDayPastTheDailyMaximum()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(maxTimeInHrs: At(8)));
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(Today, At(8), At(14)));

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            startTime: "15:00:00", endTime: "18:00:00"));

        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
        Assert.Single(await Factory.TimeLogRowsAsync(UserId));
    }

    /// <summary>Exactly reaching the maximum is allowed - the rule is "not above".</summary>
    [Fact]
    public async Task AddIsAllowedWhenItExactlyReachesTheDailyMaximum()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(maxTimeInHrs: At(8)));
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(Today, At(8), At(14)));

        await SaveAsync(TimeLogEndpointTestData.SaveRequest(startTime: "14:00:00", endTime: "16:00:00"));

        Assert.Equal(2, (await Factory.TimeLogRowsAsync(UserId)).Count);
    }

    /// <summary>
    /// The clock reads 12:00 UTC and the cut-off is 11:00: a block starting at
    /// 10:00 is locked, one starting at 11:00 belongs to the open part of the day.
    /// </summary>
    [Fact]
    public async Task AfterTheCutOffOnlyBlocksStartingAtOrAfterItCanBeAdded()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(timeEntryLockAt: At(11)));

        var before = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            startTime: "10:00:00", endTime: "10:30:00"));
        await AssertStatusAsync(before, HttpStatusCode.BadRequest);

        var after = await SaveAsync(TimeLogEndpointTestData.SaveRequest(
            startTime: "11:00:00", endTime: "11:30:00"));
        Assert.Equal(At(11), after.StartTime);
    }

    /// <summary>
    /// An earlier day stays open until the cut-off on the employee's clock:
    /// at 12:00 UTC a 13:00 cut-off has not passed, an 11:00 one has.
    /// </summary>
    [Theory]
    [InlineData(13, HttpStatusCode.OK)]
    [InlineData(11, HttpStatusCode.BadRequest)]
    public async Task AnEarlierDayIsLockedOnceTheCutOffHasPassed(int lockHour, HttpStatusCode expected)
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(timeEntryLockAt: At(lockHour)));

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            startDate: Monday, startTime: "13:00:00", endTime: "14:00:00"));

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>
    /// The cut-off is judged in the setup's time zone. 12:00 UTC is 17:30 in
    /// Asia/Kolkata, past a 13:00 cut-off - where in UTC it would not be.
    /// </summary>
    [Fact]
    public async Task TheCutOffIsJudgedOnTheEmployeesOwnClock()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(
            timeEntryLockAt: At(13),
            timeZone: "Asia/Kolkata"));

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            startTime: "09:00:00", endTime: "10:00:00"));

        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
    }

    /// <summary>Once a sheet has been submitted, time can no longer be added to its week.</summary>
    [Fact]
    public async Task AddIsRefusedIntoAWeekWhoseSheetHasBeenSubmitted()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(Monday, At(9), At(10), sheetCode: "SHT0001"));
        await Factory.SeedSubmissionsAsync(SheetSubmissionTestData.Submission("SHT0001", submittedBy: UserId));

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest());

        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
        Assert.Single(await Factory.TimeLogRowsAsync(UserId));
    }

    // ================================================================ save-employee-time-log: edit

    [Fact]
    public async Task EditOverwritesTheEntryAndKeepsItsSheetCode()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var original = TimeLogEndpointTestData.Entry(Today, At(9), At(10), sheetCode: "SHT0007");
        original.CreatedBy = 777;
        await Factory.SeedTimeLogWithIdAsync(5, original);

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            timeLogId: 5,
            startTime: "13:00:00",
            endTime: "15:30:00",
            status: TimeLogStatus.Draft,
            isProjectTask: false,
            description: "Edited."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<TimeLogResponse>();
        Assert.Equal("Time log updated.", envelope.Message);

        var saved = envelope.Data!;
        Assert.Equal(5, saved.SheetId);
        Assert.Equal("SHT0007", saved.SheetCode);
        Assert.Equal(2.5m, saved.TotalWorkingHours);
        Assert.Equal(TimeLogEndpointTestData.ActingUserId, saved.UpdatedBy);
        Assert.Equal(Factory.Clock.UtcNow, saved.UpdateDate);

        var row = Assert.Single(await Factory.TimeLogRowsAsync(UserId));
        Assert.Equal(5, row.SheetId);
        Assert.Equal("SHT0007", row.SheetCode);
        Assert.Equal(At(13), row.StartTime);
        Assert.Equal(At(15, 30), row.EndTime);
        Assert.Equal(TimeLogStatus.Draft, row.Status);
        Assert.False(row.IsProjectTask);
        Assert.Equal("Edited.", row.Description);
        Assert.Equal(777, row.CreatedBy);
        Assert.Equal(TimeLogEndpointTestData.ActingUserId, row.UpdatedBy);
    }

    /// <summary>The entry being edited is left out of its own day, so it cannot overlap itself.</summary>
    [Fact]
    public async Task EditCanShiftAnEntryAcrossItsOwnTime()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());
        await Factory.SeedTimeLogWithIdAsync(5, TimeLogEndpointTestData.Entry(Today, At(9), At(10)));

        var saved = await SaveAsync(TimeLogEndpointTestData.SaveRequest(
            timeLogId: 5, startTime: "09:30:00", endTime: "10:30:00"));

        Assert.Equal(At(9, 30), saved.StartTime);
    }

    /// <summary>Moved to another week, the entry takes that week's code - here a new one.</summary>
    [Fact]
    public async Task EditMovingAnEntryToAnotherWeekGivesItThatWeeksCode()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());
        await Factory.SeedTimeLogWithIdAsync(5, TimeLogEndpointTestData.Entry(Today, At(9), At(10), sheetCode: "SHT0001"));

        var saved = await SaveAsync(TimeLogEndpointTestData.SaveRequest(
            timeLogId: 5, startDate: TimeLogEndpointTestData.PreviousFriday));

        Assert.Equal("SHT0002", saved.SheetCode);
        Assert.Equal("SHT0002", (await Factory.FindTimeLogAsync(5))!.SheetCode);
    }

    [Fact]
    public async Task EditOfAnEntryThatDoesNotExistIsNotFound()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(timeLogId: 99));

        await AssertStatusAsync(response, HttpStatusCode.NotFound);
        Assert.Empty(await Factory.TimeLogRowsAsync(UserId));
    }

    /// <summary>The query filter hides a deleted entry, so it cannot be edited back to life.</summary>
    [Fact]
    public async Task EditOfADeletedEntryIsNotFound()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());
        await Factory.SeedTimeLogWithIdAsync(5, TimeLogEndpointTestData.Entry(Today, At(9), At(10)), isDeleted: true);

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(timeLogId: 5));

        await AssertStatusAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditOfAnotherUsersEntryIsRefused()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());
        await Factory.SeedTimeLogWithIdAsync(5, TimeLogEndpointTestData.Entry(Today, At(9), At(10), userId: OtherUserId));

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(timeLogId: 5));

        await AssertStatusAsync(response, HttpStatusCode.BadRequest);

        var untouched = (await Factory.FindTimeLogAsync(5))!;
        Assert.Equal(OtherUserId, untouched.UserId);
        Assert.Equal(At(10), untouched.EndTime);
    }

    /// <summary>
    /// After the cut-off, a block that started before it can be neither changed
    /// nor moved - even to a time after the cut-off.
    /// </summary>
    [Fact]
    public async Task EditOfABlockLockedByTheCutOffIsRefused()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(timeEntryLockAt: At(11)));
        await Factory.SeedTimeLogWithIdAsync(5, TimeLogEndpointTestData.Entry(Today, At(9), At(10)));

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            timeLogId: 5, startTime: "13:00:00", endTime: "14:00:00"));

        await AssertStatusAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(At(9), (await Factory.FindTimeLogAsync(5))!.StartTime);
    }

    // ================================================================ save-employee-time-log: validation

    [Fact]
    public async Task SaveRequiresAUser()
    {
        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(userId: 0));

        await AssertBadRequestNamingAsync(response, nameof(TimeLogSaveRequest.UserId));
    }

    [Fact]
    public async Task SaveRequiresATask()
    {
        var request = TimeLogEndpointTestData.SaveRequest();
        request.TaskId = 0;

        await AssertBadRequestNamingAsync(await PostSaveAsync(request), nameof(TimeLogSaveRequest.TaskId));
    }

    [Fact]
    public async Task SaveRequiresIsProjectTask()
    {
        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(isProjectTask: null));

        await AssertBadRequestNamingAsync(response, nameof(TimeLogSaveRequest.IsProjectTask));
    }

    [Fact]
    public async Task SaveRejectsANegativeTimeLogId()
    {
        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(timeLogId: -1));

        await AssertBadRequestNamingAsync(response, nameof(TimeLogSaveRequest.TimeLogId));
    }

    [Fact]
    public async Task SaveRejectsAnUnknownStatus()
    {
        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(status: (TimeLogStatus)7));

        await AssertBadRequestNamingAsync(response, nameof(TimeLogSaveRequest.Status));
    }

    [Fact]
    public async Task SaveRejectsAnEndDateMoreThanADayAfterTheStart()
    {
        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            startDate: Monday, endDate: Monday.AddDays(2)));

        await AssertBadRequestNamingAsync(response, nameof(TimeLogSaveRequest.EndDate));
    }

    [Fact]
    public async Task SaveRejectsAnEndDateBeforeTheStart()
    {
        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            startDate: Today, endDate: Monday));

        await AssertBadRequestNamingAsync(response, nameof(TimeLogSaveRequest.EndDate));
    }

    /// <summary>
    /// Times are parsed by <c>TimeOfDay</c> in the service, which names the
    /// field. The setup is seeded so nothing else could be the reason.
    /// </summary>
    [Theory]
    [InlineData("9:00")]
    [InlineData("24:00:00")]
    [InlineData("09:00")]
    [InlineData("1.06:00:00")]
    public async Task SaveRejectsAStartTimeNotInHhMmSs(string startTime)
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(startTime: startTime));

        await AssertBadRequestNamingAsync(response, nameof(TimeLogSaveRequest.StartTime));
        Assert.Empty(await Factory.TimeLogRowsAsync(UserId));
    }

    [Fact]
    public async Task SaveRequiresAnEndTime()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(endTime: null));

        await AssertBadRequestNamingAsync(response, nameof(TimeLogSaveRequest.EndTime));
    }

    /// <summary>17:00 to 09:00 on a single day runs backwards.</summary>
    [Fact]
    public async Task SaveRejectsAnEntryThatEndsBeforeItStarts()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup());

        var response = await PostSaveAsync(TimeLogEndpointTestData.SaveRequest(
            startTime: "17:00:00", endTime: "09:00:00"));

        await AssertBadRequestNamingAsync(response, nameof(TimeLogSaveRequest.EndTime));
    }
}
