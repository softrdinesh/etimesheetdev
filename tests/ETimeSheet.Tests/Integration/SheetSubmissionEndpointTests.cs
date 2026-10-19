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
/// The SheetSubmission endpoints, end to end against a real SQL Server: the EF
/// writes to <c>dbo.TimesheetSubmission</c> and
/// <c>dbo.TimeSheetSubmissionAuditLog</c>, the SQL-side minute sum over
/// <c>dbo.TimeLog</c>, the recorded <c>spc_GetSubmittedSheetList</c>, and the
/// real middleware and validation.
/// <para>
/// Submit, review and the current-week read run on the API's frozen clock -
/// Tuesday 2026-03-10 12:00 UTC - so the week they report is fixed: Monday
/// 2026-03-09 for a Monday-anchored setup. The list procedure reads no clock.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class SheetSubmissionEndpointTests : IntegrationTestBase
{
    private const int UserId = SheetSubmissionTestData.UserId;
    private const int SecondUserId = SheetSubmissionTestData.SecondUserId;
    private const string SheetCode = SheetSubmissionTestData.SheetCode;

    private const int Submitted = Constants.TimesheetSubmission.Status.Submitted;
    private const int Approved = Constants.TimesheetSubmission.Status.Approved;
    private const int Rejected = Constants.TimesheetSubmission.Status.Rejected;

    private const string SubmitUrl = "/api/v1/SheetSubmission/submit-timesheet";
    private const string ReviewUrl = "/api/v1/SheetSubmission/review-timesheet";
    private const string ListUrl = "/api/v1/SheetSubmission/get-submitted-sheet-list";

    private static readonly DateTime Today = TimeLogEndpointTestData.Today;
    private static readonly DateTime Monday = TimeLogEndpointTestData.Monday;

    public SheetSubmissionEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // Every SheetSubmission endpoint is [AllowAnonymous] while authentication is off.
    private HttpClient Client() => Factory.CreateClient();

    private static TimeSpan At(int hours, int minutes = 0) => TimeLogEndpointTestData.At(hours, minutes);

    private static string CurrentWeekUrl(int userId, int orgId = SheetSubmissionTestData.OrganizationId) =>
        $"/api/v1/SheetSubmission/get-current-week-sheet-details/{userId}/{orgId}";

    private Task<HttpResponseMessage> PostSubmitAsync(SheetSubmissionRequest request) =>
        Client().PostAsJsonAsync(SubmitUrl, request);

    private Task<HttpResponseMessage> PostReviewAsync(SheetSubmissionReviewRequest request) =>
        Client().PostAsJsonAsync(ReviewUrl, request);

    private Task<HttpResponseMessage> PostListAsync(SubmittedSheetListRequest request) =>
        Client().PostAsJsonAsync(ListUrl, request);

    private async Task<SheetSubmissionResponse> ReviewAsync(SheetSubmissionReviewRequest request)
    {
        var response = await PostReviewAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<SheetSubmissionResponse>();
    }

    private async Task<CurrentWeekSheetDetailResponse> CurrentWeekAsync(int userId = UserId)
    {
        var response = await Client().GetAsync(CurrentWeekUrl(userId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<CurrentWeekSheetDetailResponse>();
    }

    private async Task<List<SubmittedSheetResponse>> ListAsync(int userId = 0)
    {
        var response = await PostListAsync(SheetSubmissionTestData.ListRequest(userId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<List<SubmittedSheetResponse>>();
    }

    private static async Task AssertBadRequestNamingAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<object>();

        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors, error => error.StartsWith(field, StringComparison.OrdinalIgnoreCase));
    }

    // ================================================================ submit-timesheet

    /// <summary>
    /// Only the submitting user's <b>saved</b>, live entries on the sheet count:
    /// 180 + 90 = 270 minutes, 4.50 hours. A draft, a deleted entry and another
    /// user's entry on the same code are all left out.
    /// </summary>
    [Fact]
    public async Task SubmitRecordsTheSheetWithItsSavedTotalAndAnAuditEntry()
    {
        var seeded = await Factory.SeedAsync(
            TimeLogEndpointTestData.Entry(Monday, At(9), At(12), userId: UserId),
            TimeLogEndpointTestData.Entry(Today, At(13), At(14, 30), userId: UserId),
            TimeLogEndpointTestData.Entry(Today, At(15), At(16), userId: UserId, status: TimeLogStatus.Draft),
            TimeLogEndpointTestData.Entry(Today, At(16), At(17), userId: UserId),
            TimeLogEndpointTestData.Entry(Today, At(9), At(17), userId: SecondUserId));

        await Factory.SoftDeleteAsync(seeded[3].SheetId);

        var response = await PostSubmitAsync(SheetSubmissionTestData.SubmitRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<SheetSubmissionResponse>();
        Assert.Equal("Timesheet submitted.", envelope.Message);

        var submission = envelope.Data!;
        Assert.Equal(SheetCode, submission.SheetCode);
        Assert.Equal(4.5m, submission.TotalHours);
        Assert.Equal(270m, submission.TotalMins);
        Assert.Equal(UserId, submission.SubmittedBy);
        Assert.Equal(Factory.Clock.UtcNow, submission.SubmittedDate);
        Assert.Equal(Submitted, submission.StatusId);
        Assert.Null(submission.ApprovedBy);
        Assert.Null(submission.ApprovedDate);
        Assert.Null(submission.RejectedBy);
        Assert.Null(submission.RejectedDate);

        var row = Assert.Single(await Factory.SubmissionRowsAsync());
        Assert.Equal(SheetCode, row.Timesheetcode);
        Assert.Equal(4.5m, row.TotalHours);
        Assert.Equal(270m, row.TotalMins);
        Assert.Equal(UserId, row.SubmittedBy);
        Assert.Equal(Factory.Clock.UtcNow, row.SubmittedDate);
        Assert.Equal(Submitted, row.StatusId);

        var audit = Assert.Single(await Factory.AuditRowsAsync());
        Assert.Equal(SheetCode, audit.TimeSheetSubmissionId);
        Assert.Equal(Submitted, audit.StatusId);
        Assert.Equal(UserId, audit.CreatedBy);
        Assert.Equal(Factory.Clock.UtcNow, audit.CreateDate);
    }

    /// <summary>22:00 Monday to 06:00 Tuesday is one day plus -960 minutes: 480.</summary>
    [Fact]
    public async Task SubmitCountsAnOvernightEntryAcrossBothDates()
    {
        await Factory.SeedAsync(
            TimeLogEndpointTestData.Entry(Monday, At(22), At(6), userId: UserId, endDate: Today));

        var response = await PostSubmitAsync(SheetSubmissionTestData.SubmitRequest());
        var submission = await response.ReadDataAsync<SheetSubmissionResponse>();

        Assert.Equal(480m, submission.TotalMins);
        Assert.Equal(8m, submission.TotalHours);
    }

    [Fact]
    public async Task SubmitTrimsTheSheetCode()
    {
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(Monday, At(9), At(10), userId: UserId));

        var response = await PostSubmitAsync(SheetSubmissionTestData.SubmitRequest(sheetCode: "  SHT0001 "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SheetCode, Assert.Single(await Factory.SubmissionRowsAsync()).Timesheetcode);
    }

    /// <summary>A sheet is submitted once; the second attempt changes nothing.</summary>
    [Fact]
    public async Task SubmittingTheSameSheetTwiceIsAConflict()
    {
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(Monday, At(9), At(10), userId: UserId));

        Assert.Equal(HttpStatusCode.OK, (await PostSubmitAsync(SheetSubmissionTestData.SubmitRequest())).StatusCode);

        var second = await PostSubmitAsync(SheetSubmissionTestData.SubmitRequest());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Single(await Factory.SubmissionRowsAsync());
        Assert.Single(await Factory.AuditRowsAsync());
    }

    /// <summary>Drafts are not saved time, so a sheet holding only drafts cannot be submitted.</summary>
    [Fact]
    public async Task SubmitIsRefusedWhenTheSheetHasOnlyDrafts()
    {
        await Factory.SeedAsync(
            TimeLogEndpointTestData.Entry(Monday, At(9), At(10), userId: UserId, status: TimeLogStatus.Draft));

        var response = await PostSubmitAsync(SheetSubmissionTestData.SubmitRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await Factory.SubmissionRowsAsync());
        Assert.Empty(await Factory.AuditRowsAsync());
    }

    /// <summary>The total is the submitting user's own time; someone else's entries on the code do not count.</summary>
    [Fact]
    public async Task SubmitIsRefusedWhenOnlyAnotherUserHasTimeOnTheSheet()
    {
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(Monday, At(9), At(10), userId: UserId));

        var response = await PostSubmitAsync(SheetSubmissionTestData.SubmitRequest(userId: SecondUserId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await Factory.SubmissionRowsAsync());
    }

    [Fact]
    public async Task SubmitRequiresASheetCode()
    {
        var response = await PostSubmitAsync(SheetSubmissionTestData.SubmitRequest(sheetCode: null));

        await AssertBadRequestNamingAsync(response, nameof(SheetSubmissionRequest.SheetCode));
    }

    /// <summary><c>Timesheetcode</c> is <c>varchar(15)</c>.</summary>
    [Fact]
    public async Task SubmitRejectsASheetCodeLongerThanFifteenCharacters()
    {
        var response = await PostSubmitAsync(SheetSubmissionTestData.SubmitRequest(sheetCode: new string('S', 16)));

        await AssertBadRequestNamingAsync(response, nameof(SheetSubmissionRequest.SheetCode));
    }

    [Fact]
    public async Task SubmitRequiresAUser()
    {
        var response = await PostSubmitAsync(SheetSubmissionTestData.SubmitRequest(userId: 0));

        await AssertBadRequestNamingAsync(response, nameof(SheetSubmissionRequest.UserId));
    }

    // ================================================================ review-timesheet

    [Fact]
    public async Task ApprovingRecordsTheAdminAndTheTimeAndAnAuditEntry()
    {
        await Factory.SeedSubmissionsAsync(SheetSubmissionTestData.Submission(totalMinutes: 2100));

        var response = await PostReviewAsync(SheetSubmissionTestData.ReviewRequest(Approved));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<SheetSubmissionResponse>();
        Assert.Equal("Timesheet approved.", envelope.Message);

        var reviewed = envelope.Data!;
        Assert.Equal(SheetCode, reviewed.SheetCode);
        Assert.Equal(Approved, reviewed.StatusId);
        Assert.Equal(SheetSubmissionTestData.AdminId, reviewed.ApprovedBy);
        Assert.Equal(Factory.Clock.UtcNow, reviewed.ApprovedDate);
        Assert.Null(reviewed.RejectedBy);
        Assert.Null(reviewed.RejectedDate);

        // The submission itself is untouched by a review.
        Assert.Equal(UserId, reviewed.SubmittedBy);
        Assert.Equal(2100m, reviewed.TotalMins);
        Assert.Equal(35m, reviewed.TotalHours);

        var row = Assert.Single(await Factory.SubmissionRowsAsync());
        Assert.Equal(Approved, row.StatusId);
        Assert.Equal(SheetSubmissionTestData.AdminId, row.ApprovedBy);
        Assert.Equal(Factory.Clock.UtcNow, row.ApprovedDate);

        var audit = Assert.Single(await Factory.AuditRowsAsync());
        Assert.Equal(SheetCode, audit.TimeSheetSubmissionId);
        Assert.Equal(Approved, audit.StatusId);
        Assert.Equal(SheetSubmissionTestData.AdminId, audit.CreatedBy);
        Assert.Equal(Factory.Clock.UtcNow, audit.CreateDate);
    }

    [Fact]
    public async Task RejectingRecordsTheAdminAndTheTime()
    {
        await Factory.SeedSubmissionsAsync(SheetSubmissionTestData.Submission());

        var response = await PostReviewAsync(SheetSubmissionTestData.ReviewRequest(Rejected));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<SheetSubmissionResponse>();
        Assert.Equal("Timesheet rejected.", envelope.Message);
        Assert.Equal(Rejected, envelope.Data!.StatusId);
        Assert.Equal(SheetSubmissionTestData.AdminId, envelope.Data.RejectedBy);
        Assert.Equal(Factory.Clock.UtcNow, envelope.Data.RejectedDate);
        Assert.Null(envelope.Data.ApprovedBy);

        var row = Assert.Single(await Factory.SubmissionRowsAsync());
        Assert.Equal(Rejected, row.StatusId);
        Assert.Equal(SheetSubmissionTestData.AdminId, row.RejectedBy);
        Assert.Null(row.ApprovedBy);
    }

    /// <summary>
    /// A sheet can be reviewed again. The row shows only the latest decision -
    /// the rejection is cleared once it is approved - while the audit log keeps
    /// both, in order.
    /// </summary>
    [Fact]
    public async Task ARejectedSheetCanBeApprovedLaterAndTheAuditKeepsBothDecisions()
    {
        await Factory.SeedSubmissionsAsync(SheetSubmissionTestData.Submission());

        await ReviewAsync(SheetSubmissionTestData.ReviewRequest(Rejected, adminId: SheetSubmissionTestData.SecondAdminId));
        var approved = await ReviewAsync(SheetSubmissionTestData.ReviewRequest(Approved));

        Assert.Equal(Approved, approved.StatusId);
        Assert.Equal(SheetSubmissionTestData.AdminId, approved.ApprovedBy);
        Assert.Null(approved.RejectedBy);
        Assert.Null(approved.RejectedDate);

        var row = Assert.Single(await Factory.SubmissionRowsAsync());
        Assert.Equal(Approved, row.StatusId);
        Assert.Null(row.RejectedBy);
        Assert.Null(row.RejectedDate);

        var audit = await Factory.AuditRowsAsync();
        Assert.Equal(new[] { Rejected, Approved }, audit.Select(entry => entry.StatusId));
        Assert.Equal(
            new[] { SheetSubmissionTestData.SecondAdminId, SheetSubmissionTestData.AdminId },
            audit.Select(entry => entry.CreatedBy));
    }

    [Fact]
    public async Task ReviewingASheetThatWasNeverSubmittedIsNotFound()
    {
        var response = await PostReviewAsync(SheetSubmissionTestData.ReviewRequest(Approved));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await Factory.AuditRowsAsync());
    }

    /// <summary>A review decides; it never puts a sheet back to Submitted (1) or invents a status.</summary>
    [Theory]
    [InlineData(Submitted)]
    [InlineData(0)]
    [InlineData(4)]
    public async Task ReviewRejectsAStatusOtherThanApprovedOrRejected(int status)
    {
        await Factory.SeedSubmissionsAsync(SheetSubmissionTestData.Submission());

        var response = await PostReviewAsync(SheetSubmissionTestData.ReviewRequest(status));

        await AssertBadRequestNamingAsync(response, nameof(SheetSubmissionReviewRequest.Status));
        Assert.Equal(Submitted, Assert.Single(await Factory.SubmissionRowsAsync()).StatusId);
    }

    [Fact]
    public async Task ReviewRequiresAnAdmin()
    {
        var response = await PostReviewAsync(SheetSubmissionTestData.ReviewRequest(Approved, adminId: 0));

        await AssertBadRequestNamingAsync(response, nameof(SheetSubmissionReviewRequest.AdminId));
    }

    [Fact]
    public async Task ReviewRequiresASheetCode()
    {
        var response = await PostReviewAsync(SheetSubmissionTestData.ReviewRequest(Approved, sheetCode: ""));

        await AssertBadRequestNamingAsync(response, nameof(SheetSubmissionReviewRequest.SheetCode));
    }

    // ================================================================ get-current-week-sheet-details

    /// <summary>
    /// No setup: a Monday week shown as its full seven days, no code, nothing
    /// worked, and null expected and drift.
    /// </summary>
    [Fact]
    public async Task CurrentWeekWithoutASetupIsAMondayWeekWithNothingExpected()
    {
        var week = await CurrentWeekAsync();

        Assert.Equal(UserId, week.UserId);
        Assert.Null(week.SheetCode);
        Assert.Equal(Monday, week.WeekStartDate);
        Assert.Equal(Monday.AddDays(6), week.WeekEndDate);
        Assert.Null(week.StartDay);
        Assert.Null(week.StartDayName);
        Assert.Null(week.EndDay);
        Assert.Null(week.EndDayName);
        Assert.Equal(0m, week.TotalHoursWorked);
        Assert.Null(week.TotalHoursExpected);
        Assert.Null(week.TotalHoursDrift);
    }

    /// <summary>
    /// Monday to Friday at 8 hours: 40 expected. Worked counts only saved, live
    /// entries on this week's sheet - 8 hours on Monday - so the drift is -32.
    /// </summary>
    [Fact]
    public async Task CurrentWeekReportsTheSheetAndHoursWorkedExpectedAndDrift()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(userId: UserId));

        var seeded = await Factory.SeedAsync(
            TimeLogEndpointTestData.Entry(Monday, At(9), At(17), userId: UserId, sheetCode: SheetCode),
            TimeLogEndpointTestData.Entry(Today, At(9), At(10), userId: UserId, sheetCode: SheetCode, status: TimeLogStatus.Draft),
            TimeLogEndpointTestData.Entry(Today, At(10), At(11), userId: UserId, sheetCode: SheetCode),
            TimeLogEndpointTestData.Entry(TimeLogEndpointTestData.PreviousFriday, At(9), At(10), userId: UserId, sheetCode: "SHT0009"));

        await Factory.SoftDeleteAsync(seeded[2].SheetId);

        var week = await CurrentWeekAsync();

        Assert.Equal(SheetCode, week.SheetCode);
        Assert.Equal(Monday, week.WeekStartDate);
        Assert.Equal(Monday.AddDays(4), week.WeekEndDate);
        Assert.Equal(Constants.DayMaster.DayId.Monday, week.StartDay);
        Assert.Equal("Monday", week.StartDayName);
        Assert.Equal(Constants.DayMaster.DayId.Friday, week.EndDay);
        Assert.Equal("Friday", week.EndDayName);
        Assert.Equal(8m, week.TotalHoursWorked);
        Assert.Equal(40m, week.TotalHoursExpected);
        Assert.Equal(-32m, week.TotalHoursDrift);
    }

    /// <summary>Nothing logged this week: no code, nothing worked, the whole expectation as drift.</summary>
    [Fact]
    public async Task CurrentWeekWithNothingLoggedHasNoSheetCode()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(userId: UserId));
        await Factory.SeedAsync(TimeLogEndpointTestData.Entry(
            TimeLogEndpointTestData.PreviousFriday, At(9), At(17), userId: UserId, sheetCode: "SHT0009"));

        var week = await CurrentWeekAsync();

        Assert.Null(week.SheetCode);
        Assert.Equal(0m, week.TotalHoursWorked);
        Assert.Equal(40m, week.TotalHoursExpected);
        Assert.Equal(-40m, week.TotalHoursDrift);
    }

    /// <summary>
    /// Wednesday to Tuesday wraps past Sunday: on Tuesday 2026-03-10 the week
    /// began Wednesday 2026-03-04 and is seven working days - 56 expected hours.
    /// </summary>
    [Fact]
    public async Task CurrentWeekIsAnchoredOnTheSetupsStartDay()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(
            userId: UserId,
            startDay: Constants.DayMaster.DayId.Wednesday,
            endDay: Constants.DayMaster.DayId.Tuesday));

        var week = await CurrentWeekAsync();

        Assert.Equal(new DateTime(2026, 3, 4), week.WeekStartDate);
        Assert.Equal(new DateTime(2026, 3, 10), week.WeekEndDate);
        Assert.Equal("Wednesday", week.StartDayName);
        Assert.Equal("Tuesday", week.EndDayName);
        Assert.Equal(56m, week.TotalHoursExpected);
    }

    /// <summary>
    /// "Current" is today in the user's zone. 12:00 UTC on Tuesday is 02:00 on
    /// Wednesday in Pacific/Kiritimati (UTC+14), so a Wednesday-anchored week
    /// there has just begun on 2026-03-11 - where in UTC it began on 2026-03-04.
    /// </summary>
    [Fact]
    public async Task CurrentWeekIsJudgedInTheUsersTimeZone()
    {
        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(
            userId: UserId,
            startDay: Constants.DayMaster.DayId.Wednesday,
            endDay: Constants.DayMaster.DayId.Tuesday,
            timeZone: "Pacific/Kiritimati"));

        var week = await CurrentWeekAsync();

        Assert.Equal(new DateTime(2026, 3, 11), week.WeekStartDate);
        Assert.Equal(new DateTime(2026, 3, 17), week.WeekEndDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CurrentWeekRejectsANonPositiveUserIdAtTheRoute(int userId)
    {
        var response = await Client().GetAsync(CurrentWeekUrl(userId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ================================================================ get-submitted-sheet-list

    /// <summary>
    /// Arranges four submitters. Two belong to the organisation under test: the
    /// first with a Monday-to-Friday setup, the second with none. The third is in
    /// another organisation and the fourth is soft-deleted in
    /// <c>dbo.Signup</c>; the procedure must return neither.
    /// </summary>
    private async Task ArrangeSubmittedSheetsAsync()
    {
        await Factory.SeedEmployeesAsync(
            SheetSubmissionTestData.Person(UserId),
            SheetSubmissionTestData.Person(SecondUserId),
            SheetSubmissionTestData.Person(
                SheetSubmissionTestData.OtherOrganizationUserId,
                organizationId: SheetSubmissionTestData.OtherOrganizationId),
            SheetSubmissionTestData.Person(SheetSubmissionTestData.DeletedUserId, isDelete: true));

        await Factory.SeedSetupsAsync(TimeLogEndpointTestData.Setup(userId: UserId));

        var seeded = await Factory.SeedAsync(
            // SHT0001's earliest LIVE entry is Tuesday 2026-03-03, so its week
            // starts Monday 2026-03-02. The deleted entry the week before must
            // not move it.
            TimeLogEndpointTestData.Entry(new DateTime(2026, 3, 4), At(9), At(17), userId: UserId, sheetCode: "SHT0001"),
            TimeLogEndpointTestData.Entry(new DateTime(2026, 3, 3), At(9), At(17), userId: UserId, sheetCode: "SHT0001"),
            TimeLogEndpointTestData.Entry(new DateTime(2026, 2, 26), At(9), At(17), userId: UserId, sheetCode: "SHT0001"),
            // SHT0002: Thursday 2026-03-05, no setup - so a Monday week of seven days.
            TimeLogEndpointTestData.Entry(new DateTime(2026, 3, 5), At(9), At(17), userId: SecondUserId, sheetCode: "SHT0002"));

        await Factory.SoftDeleteAsync(seeded[2].SheetId);

        await Factory.SeedSubmissionsAsync(
            SheetSubmissionTestData.Submission("SHT0001", UserId, totalMinutes: 2100,
                submittedDate: new DateTime(2026, 3, 7, 10, 0, 0)),
            SheetSubmissionTestData.Submission("SHT0002", SecondUserId, totalMinutes: 1200,
                submittedDate: new DateTime(2026, 3, 8, 10, 0, 0), statusId: Approved),
            SheetSubmissionTestData.Submission("SHT0003", SheetSubmissionTestData.OtherOrganizationUserId,
                submittedDate: new DateTime(2026, 3, 9, 10, 0, 0)),
            SheetSubmissionTestData.Submission("SHT0004", SheetSubmissionTestData.DeletedUserId,
                submittedDate: new DateTime(2026, 3, 9, 11, 0, 0)));
    }

    [Fact]
    public async Task SubmittedListIsEmptyWhenNothingHasBeenSubmitted()
    {
        Assert.Empty(await ListAsync());
    }

    /// <summary>
    /// UserId 0 lists every live user of the organisation, newest submission
    /// first; another organisation's and a deleted user's sheets are left out.
    /// </summary>
    [Fact]
    public async Task SubmittedListForEveryUserIsTheOrganisationsSheetsNewestFirst()
    {
        await ArrangeSubmittedSheetsAsync();

        Assert.Equal(new[] { "SHT0002", "SHT0001" }, (await ListAsync()).Select(sheet => sheet.SheetCode));
    }

    [Fact]
    public async Task SubmittedListForOneUserHoldsOnlyTheirSheets()
    {
        await ArrangeSubmittedSheetsAsync();

        var sheet = Assert.Single(await ListAsync(userId: UserId));
        Assert.Equal("SHT0001", sheet.SheetCode);
    }

    /// <summary>
    /// Worked is the submitted figure (2100 minutes, 35.00 h); expected is five
    /// days at eight hours (40.00 h); drift is (2100 - 2400) / 60 = -5.00 h.
    /// </summary>
    [Fact]
    public async Task SubmittedListCarriesTheSubmitterTheWeekTheHoursAndTheStatus()
    {
        await ArrangeSubmittedSheetsAsync();

        var sheet = Assert.Single(await ListAsync(), row => row.SheetCode == "SHT0001");

        Assert.Equal(UserId, sheet.UserId);
        Assert.Equal($"Submitter {UserId}", sheet.Name);
        Assert.Equal($"submitter{UserId}@etimesheet.test", sheet.Email);
        Assert.Equal(new DateTime(2026, 3, 2), sheet.WeekStartDate);
        Assert.Equal(new DateTime(2026, 3, 6), sheet.WeekEndDate);
        Assert.Equal(Constants.DayMaster.DayId.Monday, sheet.StartDay);
        Assert.Equal("Monday", sheet.StartDayName);
        Assert.Equal(Constants.DayMaster.DayId.Friday, sheet.EndDay);
        Assert.Equal("Friday", sheet.EndDayName);
        Assert.Equal(35m, sheet.TotalHoursWorked);
        Assert.Equal(40m, sheet.TotalHoursExpected);
        Assert.Equal(-5m, sheet.TotalHoursDrift);
        Assert.Equal(Submitted, sheet.Status);
        Assert.Equal("Submitted", sheet.StatusText);
        Assert.Equal(new DateTime(2026, 3, 7, 10, 0, 0), sheet.SubmittedDate);
    }

    /// <summary>
    /// No setup: a Monday week of seven days, nothing expected and no drift.
    /// StatusText names only status 1 so far, so an approved sheet's is null.
    /// </summary>
    [Fact]
    public async Task SubmittedListForAUserWithNoSetupHasNoExpectationAndOnlyStatusOneHasText()
    {
        await ArrangeSubmittedSheetsAsync();

        var sheet = Assert.Single(await ListAsync(), row => row.SheetCode == "SHT0002");

        Assert.Equal(new DateTime(2026, 3, 2), sheet.WeekStartDate);
        Assert.Equal(new DateTime(2026, 3, 8), sheet.WeekEndDate);
        Assert.Null(sheet.StartDay);
        Assert.Null(sheet.StartDayName);
        Assert.Null(sheet.EndDay);
        Assert.Null(sheet.EndDayName);
        Assert.Equal(20m, sheet.TotalHoursWorked);
        Assert.Null(sheet.TotalHoursExpected);
        Assert.Null(sheet.TotalHoursDrift);
        Assert.Equal(Approved, sheet.Status);
        Assert.Null(sheet.StatusText);
    }

    /// <summary>A sheet with no live entry left has nothing to date its week by.</summary>
    [Fact]
    public async Task SubmittedListGivesASheetWithNoLiveEntriesNoWeek()
    {
        await Factory.SeedEmployeesAsync(SheetSubmissionTestData.Person(UserId));
        await Factory.SeedSubmissionsAsync(SheetSubmissionTestData.Submission("SHT0005", UserId));

        var sheet = Assert.Single(await ListAsync());

        Assert.Equal("SHT0005", sheet.SheetCode);
        Assert.Null(sheet.WeekStartDate);
        Assert.Null(sheet.WeekEndDate);
    }

    [Fact]
    public async Task SubmittedListRequiresAnAdmin()
    {
        var response = await PostListAsync(SheetSubmissionTestData.ListRequest(adminId: 0));

        await AssertBadRequestNamingAsync(response, nameof(SubmittedSheetListRequest.AdminId));
    }

    [Fact]
    public async Task SubmittedListRequiresAnOrganisation()
    {
        var response = await PostListAsync(SheetSubmissionTestData.ListRequest(orgId: 0));

        await AssertBadRequestNamingAsync(response, nameof(SubmittedSheetListRequest.OrgId));
    }

    /// <summary>Zero means every user, so only a negative user id is malformed.</summary>
    [Fact]
    public async Task SubmittedListRejectsANegativeUserId()
    {
        var response = await PostListAsync(SheetSubmissionTestData.ListRequest(userId: -1));

        await AssertBadRequestNamingAsync(response, nameof(SubmittedSheetListRequest.UserId));
    }
}
