using System.Net;
using System.Net.Http.Json;
using ETimeSheet.Application.Models;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;
using static ETimeSheet.Tests.TestData.ProjectTimeLogTestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// The project sheet endpoints of <c>TimesheetProjectController</c>, end to
/// end against a real SQL Server: <c>get-current-project-timesheet</c>,
/// <c>submit-project-timesheet</c>, <c>update-project-timesheet-status</c> and
/// <c>remove-project-timesheet</c>, over the recorded
/// <c>dbo.TimesheetProjectTimeLog</c>, <c>dbo.TimesheetProjectSubmission</c> and
/// <c>dbo.TimesheetProjectSubmissionAuditLog</c>.
/// <para>
/// What is proven here: which sheet is "current", the totals built from the
/// entries, the lock a submission puts on its entries and the ways it comes
/// off again (rejection, removal), the review stamps that describe only the
/// current status, and the append-only audit trail.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class ProjectTimesheetSubmissionEndpointTests : IntegrationTestBase
{
    private const string CurrentUrl = BaseUrl + "/get-current-project-timesheet";
    private const string SubmitUrl = BaseUrl + "/submit-project-timesheet";
    private const string StatusUrl = BaseUrl + "/update-project-timesheet-status";
    private const string RemoveUrl = BaseUrl + "/remove-project-timesheet";
    private const string SaveEntryUrl = BaseUrl + "/add-edit-project-time-log";

    public ProjectTimesheetSubmissionEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // The controller is [AllowAnonymous] while authentication is off, so an
    // unauthenticated client is the honest way to call it.
    private HttpClient Client() => Factory.CreateClient();

    private async Task<ProjectTimesheetCurrentResponse> CurrentAsync(int userId = UserId)
    {
        var response = await Client().GetAsync($"{CurrentUrl}?userID={userId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<ProjectTimesheetCurrentResponse>();
    }

    private Task<HttpResponseMessage> PostSubmitAsync(int userId = UserId, int? submittedBy = null) =>
        Client().PostAsJsonAsync(SubmitUrl, new ProjectTimesheetSubmitRequest
        {
            UserId = userId,
            SubmittedBy = submittedBy ?? userId,
        });

    private async Task<ProjectTimesheetSubmissionResponse> SubmitAsync(int userId = UserId, int? submittedBy = null)
    {
        var response = await PostSubmitAsync(userId, submittedBy);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<ProjectTimesheetSubmissionResponse>();
    }

    private Task<HttpResponseMessage> PostStatusAsync(
        int timesheetProjectSubmissionId,
        int statusId,
        string? rejectionReason = null,
        int reviewedBy = ReviewerId) =>
        Client().PostAsJsonAsync(StatusUrl, new ProjectTimesheetStatusUpdateRequest
        {
            TimesheetProjectSubmissionId = timesheetProjectSubmissionId,
            StatusId = statusId,
            RejectionReason = rejectionReason,
            ReviewedBy = reviewedBy,
        });

    private async Task<ProjectTimesheetSubmissionResponse> UpdateStatusAsync(
        int timesheetProjectSubmissionId,
        int statusId,
        string? rejectionReason = null)
    {
        var response = await PostStatusAsync(timesheetProjectSubmissionId, statusId, rejectionReason);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<ProjectTimesheetSubmissionResponse>();
    }

    private Task<HttpResponseMessage> PostRemoveAsync(int timesheetProjectSubmissionId, int deletedBy = ReviewerId) =>
        Client().PostAsJsonAsync(RemoveUrl, new ProjectTimesheetDeleteRequest
        {
            TimesheetProjectSubmissionId = timesheetProjectSubmissionId,
            DeletedBy = deletedBy,
        });

    /// <summary>Edits seeded entry <paramref name="entryId"/> through the API, to probe whether its sheet is locked.</summary>
    private Task<HttpResponseMessage> PostEditEntryAsync(int entryId, string description) =>
        Client().PostAsJsonAsync(SaveEntryUrl, SaveRequest(timesheetProjectTimeLogId: entryId, description: description));

    /// <summary>
    /// The usual arrangement: <see cref="ProjectTimeLogTestData.UserId"/> has
    /// 60 minutes on Alpha and 100 on Beta on their first sheet - 160 minutes,
    /// 2.67 hours - plus a deleted entry and another user's entry on the same
    /// code, neither of which counts.
    /// </summary>
    private async Task SeedFirstSheetAsync()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, "PSH-501-0001", new DateTime(2026, 3, 3), durationMinutes: 60),
            Entry(42, "PSH-501-0001", new DateTime(2026, 3, 5), durationMinutes: 100, projectId: BetaProjectId),
            Entry(43, "PSH-501-0001", new DateTime(2026, 3, 1), durationMinutes: 30, isDeleted: true),
            Entry(44, "PSH-501-0001", new DateTime(2026, 3, 2), durationMinutes: 500, userId: OtherUserId));
    }

    // ================================================================ get-current-project-timesheet

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CurrentRefusesAUserIdThatIsNotPositive(int userId)
    {
        var response = await Client().GetAsync($"{CurrentUrl}?userID={userId}");

        await response.AssertTimeLogValidationFailureAsync("userID");
    }

    /// <summary>
    /// A user who has never logged anything still gets a sheet: the code
    /// their first one would take, empty, and submittable.
    /// </summary>
    [Fact]
    public async Task CurrentForAUserWithNoHistoryIsAnEmptySubmittableFirstSheet()
    {
        var sheet = await CurrentAsync();

        Assert.Equal(UserId, sheet.UserId);
        Assert.Equal("PSH-501-0001", sheet.SheetSubmissionCode);
        Assert.Null(sheet.PeriodStart);
        Assert.Null(sheet.PeriodEnd);
        Assert.Equal(0m, sheet.TotalMinutes);
        Assert.Equal(0m, sheet.TotalHours);
        Assert.Equal(0, sheet.EntryCount);
        Assert.True(sheet.CanSubmit);
        Assert.Null(sheet.Submission);
        Assert.Empty(sheet.Projects);
        Assert.Empty(sheet.Entries);
    }

    /// <summary>
    /// The open sheet: its span, its totals, one line per project ordered by
    /// name, and every entry - only the user's own, and only live ones.
    /// </summary>
    [Fact]
    public async Task CurrentTotalsTheOpenSheetPerProject()
    {
        await SeedFirstSheetAsync();
        await Factory.SeedTimeLogEntriesAsync(
            Entry(45, "PSH-501-0001", new DateTime(2026, 3, 4), durationMinutes: 90));

        var sheet = await CurrentAsync();

        Assert.Equal("PSH-501-0001", sheet.SheetSubmissionCode);
        Assert.Equal(new DateTime(2026, 3, 3), sheet.PeriodStart);
        Assert.Equal(new DateTime(2026, 3, 5), sheet.PeriodEnd);
        Assert.Equal(250m, sheet.TotalMinutes);
        Assert.Equal(4.17m, sheet.TotalHours);
        Assert.Equal(3, sheet.EntryCount);
        Assert.True(sheet.CanSubmit);
        Assert.Null(sheet.Submission);

        Assert.Equal(new[] { 41, 45, 42 }, sheet.Entries.Select(entry => entry.TimesheetProjectTimeLogId));
        Assert.All(sheet.Entries, entry => Assert.False(entry.IsSubmitted));
        Assert.All(sheet.Entries, entry => Assert.Equal("Save", entry.StatusName));

        Assert.Collection(
            sheet.Projects,
            alpha =>
            {
                Assert.Equal(AlphaProjectId, alpha.ProjectId);
                Assert.Equal(AlphaProjectCode, alpha.ProjectCode);
                Assert.Equal(AlphaProjectName, alpha.ProjectName);
                Assert.Equal(150m, alpha.TotalMinutes);
                Assert.Equal(2.5m, alpha.TotalHours);
                Assert.Equal(2, alpha.EntryCount);
            },
            beta =>
            {
                Assert.Equal(BetaProjectId, beta.ProjectId);
                Assert.Equal(BetaProjectName, beta.ProjectName);
                Assert.Equal(100m, beta.TotalMinutes);
                Assert.Equal(1.67m, beta.TotalHours);
                Assert.Equal(1, beta.EntryCount);
            });
    }

    /// <summary>
    /// Straight after a submission there is no open sheet. The screen shows
    /// the sheet just handed in, with its status, and no Submit button -
    /// rather than a fresh empty sheet the submit endpoint would refuse.
    /// </summary>
    [Theory]
    [InlineData(1, "Submitted")]
    [InlineData(2, "Approved")]
    public async Task CurrentShowsTheSheetJustHandedInAsNotSubmittable(int statusId, string statusName)
    {
        await SeedFirstSheetAsync();
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: statusId, totalMinutes: 160));

        var sheet = await CurrentAsync();

        Assert.Equal("PSH-501-0001", sheet.SheetSubmissionCode);
        Assert.False(sheet.CanSubmit);
        Assert.Equal(2, sheet.EntryCount);
        Assert.All(sheet.Entries, entry => Assert.True(entry.IsSubmitted));

        Assert.NotNull(sheet.Submission);
        Assert.Equal(7, sheet.Submission!.TimesheetProjectSubmissionId);
        Assert.Equal(statusId, sheet.Submission.StatusId);
        Assert.Equal(statusName, sheet.Submission.StatusName);
    }

    /// <summary>
    /// A rejected sheet is the user's open sheet again, carrying the reason
    /// they have to correct, and can be submitted again.
    /// </summary>
    [Fact]
    public async Task CurrentShowsARejectedSheetWithItsReasonAndAllowsResubmission()
    {
        await SeedFirstSheetAsync();
        await Factory.SeedTimeLogSubmissionsAsync(
            Sheet(7, "PSH-501-0001", statusId: 3, totalMinutes: 160, rejectionReason: "Friday is missing"));

        var sheet = await CurrentAsync();

        Assert.Equal("PSH-501-0001", sheet.SheetSubmissionCode);
        Assert.True(sheet.CanSubmit);
        Assert.All(sheet.Entries, entry => Assert.False(entry.IsSubmitted));
        Assert.Equal("Rejected", sheet.Submission!.StatusName);
        Assert.Equal("Friday is missing", sheet.Submission.RejectionReason);
        Assert.Equal(ReviewerId, sheet.Submission.RejectedBy);
    }

    [Fact]
    public async Task CurrentMovesToTheNextSheetOnceTimeIsLoggedAfterASubmission()
    {
        await SeedFirstSheetAsync();
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, totalMinutes: 160));
        await Factory.SeedTimeLogEntriesAsync(
            Entry(50, "PSH-501-0002", new DateTime(2026, 3, 9), durationMinutes: 45));

        var sheet = await CurrentAsync();

        Assert.Equal("PSH-501-0002", sheet.SheetSubmissionCode);
        Assert.True(sheet.CanSubmit);
        Assert.Null(sheet.Submission);
        Assert.Equal(45m, sheet.TotalMinutes);
        Assert.Equal(0.75m, sheet.TotalHours);
        Assert.Equal(50, Assert.Single(sheet.Entries).TimesheetProjectTimeLogId);
    }

    [Fact]
    public async Task CurrentShowsAnEmptySheetTheUserHasHandedIn()
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, submittedBy: UserId));

        var sheet = await CurrentAsync();

        Assert.Equal("PSH-501-0001", sheet.SheetSubmissionCode);
        Assert.Equal(0, sheet.EntryCount);
        Assert.False(sheet.CanSubmit);
        Assert.Equal(7, sheet.Submission!.TimesheetProjectSubmissionId);
    }

    // ================================================================ submit-project-timesheet

    [Fact]
    public async Task SubmitRefusesMissingIdsNamingBoth()
    {
        var response = await PostSubmitAsync(userId: 0, submittedBy: 0);

        await response.AssertTimeLogValidationFailureAsync("UserId", "SubmittedBy");
        Assert.Empty(await Factory.ReadTimeLogSubmissionsAsync());
    }

    /// <summary>
    /// Submitting totals the user's live entries on their open sheet, writes
    /// the sheet as Submitted, and appends the first row of its audit trail.
    /// </summary>
    [Fact]
    public async Task SubmitWritesTheSheetWithItsTotalsAndAnAuditRow()
    {
        await SeedFirstSheetAsync();

        var response = await PostSubmitAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.ReadEnvelopeAsync<ProjectTimesheetSubmissionResponse>();
        Assert.Equal("Timesheet submitted.", envelope.Message);

        var submitted = envelope.Data!;
        Assert.Equal("PSH-501-0001", submitted.SheetSubmissionCode);
        Assert.Equal(160m, submitted.TotalMinutes);
        Assert.Equal(2.67m, submitted.TotalHours);
        Assert.Equal(1, submitted.StatusId);
        Assert.Equal("Submitted", submitted.StatusName);
        Assert.Equal(UserId, submitted.SubmittedBy);
        Assert.Equal(TimeLogTestData.Now, submitted.SubmittedDate);
        Assert.Null(submitted.ApprovedBy);
        Assert.Null(submitted.ApprovedDate);
        Assert.Null(submitted.RejectedBy);
        Assert.Null(submitted.RejectedDate);
        Assert.Null(submitted.RejectionReason);

        var row = Assert.Single(await Factory.ReadTimeLogSubmissionsAsync());
        Assert.Equal(submitted.TimesheetProjectSubmissionId, row.TimesheetProjectSubmissionId);
        Assert.Equal("PSH-501-0001", row.SheetSubmissionCode);
        Assert.Equal(160m, row.TotalMinutes);
        Assert.Equal(2.67m, row.TotalHours);
        Assert.Equal(1, row.StatusId);
        Assert.Equal(UserId, row.SubmittedBy);
        Assert.Equal(TimeLogTestData.Now, row.SubmittedDate);
        Assert.Equal(0, row.IsDelete);

        var audit = Assert.Single(await Factory.ReadTimeLogSubmissionAuditsAsync());
        Assert.Equal(row.TimesheetProjectSubmissionId, audit.TimesheetProjectSubmissionId);
        Assert.Equal(1, audit.StatusId);
        Assert.Null(audit.Remark);
        Assert.Equal(UserId, audit.CreatedBy);
        Assert.Equal(TimeLogTestData.Now, audit.CreateDate);
    }

    [Fact]
    public async Task SubmitByAnotherUserRecordsWhoHandedItIn()
    {
        await SeedFirstSheetAsync();

        var submitted = await SubmitAsync(UserId, submittedBy: ReviewerId);

        Assert.Equal("PSH-501-0001", submitted.SheetSubmissionCode);
        Assert.Equal(ReviewerId, submitted.SubmittedBy);
        Assert.Equal(ReviewerId, Assert.Single(await Factory.ReadTimeLogSubmissionAuditsAsync()).CreatedBy);
    }

    /// <summary>
    /// Submitting ends the sheet: its entries are frozen from then on, and the
    /// next entry the user logs starts the next code.
    /// </summary>
    [Fact]
    public async Task SubmitLocksTheEntriesAndTheNextEntryStartsTheNextSheet()
    {
        await SeedFirstSheetAsync();

        await SubmitAsync();

        await (await PostEditEntryAsync(41, "Too late")).AssertTimeLogFailureAsync(HttpStatusCode.Conflict);
        Assert.Equal("Seeded work", (await Factory.ReadTimeLogEntryAsync(41))!.Description);

        var response = await Client().PostAsJsonAsync(SaveEntryUrl, SaveRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "PSH-501-0002",
            (await response.ReadDataAsync<TimesheetProjectTimeLogResponse>()).SheetSubmissionCode);
    }

    /// <summary>
    /// With the last sheet in and nothing logged since there is nothing to
    /// submit - so a double-clicked Submit does not leave a junk empty sheet.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SubmitWithNothingLoggedSinceTheLastSheetIsAConflict(int statusId)
    {
        await SeedFirstSheetAsync();
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: statusId, totalMinutes: 160));

        await (await PostSubmitAsync()).AssertTimeLogFailureAsync(HttpStatusCode.Conflict);

        Assert.Single(await Factory.ReadTimeLogSubmissionsAsync());
        Assert.Empty(await Factory.ReadTimeLogSubmissionAuditsAsync());
    }

    [Fact]
    public async Task SubmitTwiceInARowIsAConflictTheSecondTime()
    {
        await SeedFirstSheetAsync();

        await SubmitAsync();

        await (await PostSubmitAsync()).AssertTimeLogFailureAsync(HttpStatusCode.Conflict);
        Assert.Single(await Factory.ReadTimeLogSubmissionsAsync());
        Assert.Single(await Factory.ReadTimeLogSubmissionAuditsAsync());
    }

    /// <summary>
    /// A user with nothing logged may hand in an empty first sheet - and the
    /// audit row says so. Its code is then spent, so submitting again is
    /// refused rather than handing in a second empty sheet.
    /// </summary>
    [Fact]
    public async Task SubmitOfAnEmptyFirstSheetByItsOwnerIsAllowedOnce()
    {
        var submitted = await SubmitAsync();

        Assert.Equal("PSH-501-0001", submitted.SheetSubmissionCode);
        Assert.Equal(0m, submitted.TotalMinutes);
        Assert.Equal(0m, submitted.TotalHours);
        Assert.Equal(UserId, submitted.SubmittedBy);

        var audit = Assert.Single(await Factory.ReadTimeLogSubmissionAuditsAsync());
        Assert.Equal("Submitted with no time logged.", audit.Remark);

        await (await PostSubmitAsync()).AssertTimeLogFailureAsync(HttpStatusCode.Conflict);
        Assert.Single(await Factory.ReadTimeLogSubmissionsAsync());
    }

    /// <summary>
    /// The submission row has no UserID column, so an empty sheet submitted
    /// by somebody else could never be tied back to its owner. Refused, and
    /// nothing is written.
    /// </summary>
    [Fact]
    public async Task SubmitOfAnEmptySheetBySomebodyElseIsRefused()
    {
        var response = await PostSubmitAsync(UserId, submittedBy: ReviewerId);

        await response.AssertTimeLogValidationFailureAsync("SubmittedBy");
        Assert.Empty(await Factory.ReadTimeLogSubmissionsAsync());
        Assert.Empty(await Factory.ReadTimeLogSubmissionAuditsAsync());
    }

    /// <summary>
    /// A rejected sheet is resubmitted in place: the same row, the totals
    /// worked out afresh from the corrected entries, the rejection stamps
    /// cleared - and the trail gains a row rather than losing one.
    /// </summary>
    [Fact]
    public async Task SubmitOfARejectedSheetResubmitsTheSameRow()
    {
        await SeedFirstSheetAsync();
        await Factory.SeedTimeLogSubmissionsAsync(
            Sheet(7, "PSH-501-0001", statusId: 3, totalMinutes: 999, rejectionReason: "Friday is missing"));
        await Factory.SeedTimeLogSubmissionAuditAsync(7, statusId: 3);

        var submitted = await SubmitAsync();

        Assert.Equal(7, submitted.TimesheetProjectSubmissionId);
        Assert.Equal(1, submitted.StatusId);
        Assert.Equal(160m, submitted.TotalMinutes);
        Assert.Equal(2.67m, submitted.TotalHours);
        Assert.Null(submitted.RejectedBy);
        Assert.Null(submitted.RejectedDate);
        Assert.Null(submitted.RejectionReason);

        var row = Assert.Single(await Factory.ReadTimeLogSubmissionsAsync());
        Assert.Equal(7, row.TimesheetProjectSubmissionId);
        Assert.Equal(1, row.StatusId);
        Assert.Equal(160m, row.TotalMinutes);
        Assert.Equal(TimeLogTestData.Now, row.SubmittedDate);
        Assert.Null(row.RejectedBy);
        Assert.Null(row.RejectionReason);

        var audits = await Factory.ReadTimeLogSubmissionAuditsAsync();
        Assert.Equal(new[] { 3, 1 }, audits.Select(audit => audit.StatusId));
        Assert.All(audits, audit => Assert.Equal(7, audit.TimesheetProjectSubmissionId));
    }

    // ================================================================ update-project-timesheet-status

    [Fact]
    public async Task ApproveStampsTheApprovalAndAppendsToTheTrail()
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, totalMinutes: 160));

        var response = await PostStatusAsync(7, statusId: 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.ReadEnvelopeAsync<ProjectTimesheetSubmissionResponse>();
        Assert.Equal("Timesheet status updated.", envelope.Message);

        var reviewed = envelope.Data!;
        Assert.Equal(7, reviewed.TimesheetProjectSubmissionId);
        Assert.Equal(2, reviewed.StatusId);
        Assert.Equal("Approved", reviewed.StatusName);
        Assert.Equal(ReviewerId, reviewed.ApprovedBy);
        Assert.Equal(TimeLogTestData.Now, reviewed.ApprovedDate);
        Assert.Null(reviewed.RejectedBy);
        Assert.Null(reviewed.RejectionReason);
        Assert.Equal(160m, reviewed.TotalMinutes);

        var row = (await Factory.ReadTimeLogSubmissionAsync(7))!;
        Assert.Equal(2, row.StatusId);
        Assert.Equal(ReviewerId, row.ApprovedBy);
        Assert.Equal(TimeLogTestData.Now, row.ApprovedDate);
        Assert.Null(row.RejectedBy);
        Assert.Null(row.RejectedDate);

        var audit = Assert.Single(await Factory.ReadTimeLogSubmissionAuditsAsync());
        Assert.Equal(7, audit.TimesheetProjectSubmissionId);
        Assert.Equal(2, audit.StatusId);
        Assert.Null(audit.Remark);
        Assert.Equal(ReviewerId, audit.CreatedBy);
        Assert.Equal(TimeLogTestData.Now, audit.CreateDate);
    }

    [Fact]
    public async Task RejectStampsTheRejectionWithTheTrimmedReason()
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, totalMinutes: 160));

        var reviewed = await UpdateStatusAsync(7, statusId: 3, rejectionReason: "  Friday is missing  ");

        Assert.Equal(3, reviewed.StatusId);
        Assert.Equal("Rejected", reviewed.StatusName);
        Assert.Equal(ReviewerId, reviewed.RejectedBy);
        Assert.Equal(TimeLogTestData.Now, reviewed.RejectedDate);
        Assert.Equal("Friday is missing", reviewed.RejectionReason);
        Assert.Null(reviewed.ApprovedBy);
        Assert.Null(reviewed.ApprovedDate);

        var row = (await Factory.ReadTimeLogSubmissionAsync(7))!;
        Assert.Equal(3, row.StatusId);
        Assert.Equal("Friday is missing", row.RejectionReason);
        Assert.Equal(ReviewerId, row.RejectedBy);

        var audit = Assert.Single(await Factory.ReadTimeLogSubmissionAuditsAsync());
        Assert.Equal(3, audit.StatusId);
        Assert.Equal("Friday is missing", audit.Remark);
    }

    /// <summary>
    /// A review is repeatable, and the sheet's columns describe only its
    /// CURRENT status: each change clears the stamps that no longer apply,
    /// while the audit trail keeps every step, reason included.
    /// </summary>
    [Fact]
    public async Task ReviewIsRepeatableAndClearsTheStampsThatNoLongerApply()
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, totalMinutes: 160));

        await UpdateStatusAsync(7, statusId: 2);

        var rejected = await UpdateStatusAsync(7, statusId: 3, rejectionReason: "Wrong project");
        Assert.Null(rejected.ApprovedBy);
        Assert.Null(rejected.ApprovedDate);
        Assert.Equal("Wrong project", rejected.RejectionReason);

        var approved = await UpdateStatusAsync(7, statusId: 2);
        Assert.Equal(ReviewerId, approved.ApprovedBy);
        Assert.Null(approved.RejectedBy);
        Assert.Null(approved.RejectedDate);
        Assert.Null(approved.RejectionReason);

        var row = (await Factory.ReadTimeLogSubmissionAsync(7))!;
        Assert.Equal(2, row.StatusId);
        Assert.Null(row.RejectionReason);
        Assert.Null(row.RejectedBy);

        var audits = await Factory.ReadTimeLogSubmissionAuditsAsync();
        Assert.Equal(new[] { 2, 3, 2 }, audits.Select(audit => audit.StatusId));
        Assert.Equal(new[] { null, "Wrong project", null }, audits.Select(audit => audit.Remark));
    }

    /// <summary>
    /// Rejecting a sheet reopens its entries, so the user can correct them.
    /// </summary>
    [Fact]
    public async Task RejectReopensTheSheetsEntries()
    {
        await SeedFirstSheetAsync();
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, totalMinutes: 160));

        await (await PostEditEntryAsync(41, "Before")).AssertTimeLogFailureAsync(HttpStatusCode.Conflict);

        await UpdateStatusAsync(7, statusId: 3, rejectionReason: "Friday is missing");

        Assert.Equal(HttpStatusCode.OK, (await PostEditEntryAsync(41, "Corrected")).StatusCode);
        Assert.Equal("Corrected", (await Factory.ReadTimeLogEntryAsync(41))!.Description);
    }

    [Fact]
    public async Task StatusUpdateReportsEveryInvalidFieldTogether()
    {
        var response = await PostStatusAsync(0, statusId: 1, reviewedBy: 0);

        var errors = await response.AssertTimeLogValidationFailureAsync(
            "TimesheetProjectSubmissionId", "StatusId", "ReviewedBy");

        Assert.Equal(3, errors.Count);
    }

    /// <summary>
    /// A sheet reaches 1 (Submitted) only by being submitted; review accepts
    /// 2 and 3 alone.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public async Task StatusUpdateRefusesAStatusOtherThanApprovedOrRejected(int statusId)
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, totalMinutes: 160));

        await (await PostStatusAsync(7, statusId)).AssertTimeLogValidationFailureAsync("StatusId");

        Assert.Equal(1, (await Factory.ReadTimeLogSubmissionAsync(7))!.StatusId);
        Assert.Empty(await Factory.ReadTimeLogSubmissionAuditsAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectWithoutAReasonIsRefused(string? reason)
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, totalMinutes: 160));

        await (await PostStatusAsync(7, statusId: 3, rejectionReason: reason))
            .AssertTimeLogValidationFailureAsync("RejectionReason");

        Assert.Equal(1, (await Factory.ReadTimeLogSubmissionAsync(7))!.StatusId);
    }

    /// <summary><c>RejectionReason</c> and the trail's <c>Remark</c> are <c>nvarchar(500)</c>.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task StatusUpdateRefusesAReasonLongerThan500Characters(int statusId)
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, totalMinutes: 160));

        await (await PostStatusAsync(7, statusId, rejectionReason: new string('x', 501)))
            .AssertTimeLogValidationFailureAsync("RejectionReason");
    }

    [Fact]
    public async Task RejectAcceptsAReasonOfExactly500Characters()
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, totalMinutes: 160));

        var reason = new string('x', 500);
        var reviewed = await UpdateStatusAsync(7, statusId: 3, rejectionReason: reason);

        Assert.Equal(reason, reviewed.RejectionReason);
        Assert.Equal(reason, (await Factory.ReadTimeLogSubmissionAsync(7))!.RejectionReason);
    }

    [Fact]
    public async Task StatusUpdateOfAnUnknownSheetIsNotFound()
    {
        await (await PostStatusAsync(999, statusId: 2)).AssertTimeLogFailureAsync(HttpStatusCode.NotFound);

        Assert.Empty(await Factory.ReadTimeLogSubmissionAuditsAsync());
    }

    [Fact]
    public async Task StatusUpdateOfARemovedSheetIsNotFound()
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, isDeleted: true));

        await (await PostStatusAsync(7, statusId: 2)).AssertTimeLogFailureAsync(HttpStatusCode.NotFound);

        Assert.Equal(1, (await Factory.ReadTimeLogSubmissionAsync(7))!.StatusId);
    }

    // ================================================================ remove-project-timesheet

    /// <summary>
    /// A remove is a soft delete of the sheet only: the row stays with
    /// <c>IsDelete</c> = 1 and everything else as it was, and the audit trail
    /// is not touched.
    /// </summary>
    [Fact]
    public async Task RemoveSoftDeletesTheSheetAndLeavesTheTrailAlone()
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 2, totalMinutes: 160));
        await Factory.SeedTimeLogSubmissionAuditAsync(7, statusId: 1);

        var response = await PostRemoveAsync(7);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.ReadEnvelopeAsync<object>();
        Assert.True(envelope.Success);
        Assert.Equal("Timesheet removed.", envelope.Message);

        var row = (await Factory.ReadTimeLogSubmissionAsync(7))!;
        Assert.Equal(1, row.IsDelete);
        Assert.Equal(2, row.StatusId);
        Assert.Equal(160m, row.TotalMinutes);
        Assert.Equal(ReviewerId, row.ApprovedBy);

        var audit = Assert.Single(await Factory.ReadTimeLogSubmissionAuditsAsync());
        Assert.Equal(1, audit.StatusId);
    }

    [Fact]
    public async Task RemovingTheSameSheetTwiceIsNotFound()
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1));

        Assert.Equal(HttpStatusCode.OK, (await PostRemoveAsync(7)).StatusCode);

        await (await PostRemoveAsync(7)).AssertTimeLogFailureAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveOfAnUnknownSheetIsNotFound()
    {
        await (await PostRemoveAsync(999)).AssertTimeLogFailureAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveRefusesMissingIdsNamingBoth()
    {
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1));

        await (await PostRemoveAsync(0, deletedBy: 0))
            .AssertTimeLogValidationFailureAsync("TimesheetProjectSubmissionId", "DeletedBy");

        Assert.Equal(0, (await Factory.ReadTimeLogSubmissionAsync(7))!.IsDelete);
    }

    /// <summary>
    /// The submission was the only thing holding the entries shut: once it is
    /// removed they can be edited again, the sheet is the user's open one
    /// again, and it can be submitted again under the same code - as a new
    /// row, the removed one staying where it is.
    /// </summary>
    [Fact]
    public async Task RemoveReopensTheSheetForEditingAndResubmission()
    {
        await SeedFirstSheetAsync();
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(7, "PSH-501-0001", statusId: 1, totalMinutes: 160));

        await (await PostEditEntryAsync(41, "Before")).AssertTimeLogFailureAsync(HttpStatusCode.Conflict);

        Assert.Equal(HttpStatusCode.OK, (await PostRemoveAsync(7)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await PostEditEntryAsync(41, "Corrected")).StatusCode);

        var current = await CurrentAsync();
        Assert.Equal("PSH-501-0001", current.SheetSubmissionCode);
        Assert.True(current.CanSubmit);
        Assert.Null(current.Submission);

        var resubmitted = await SubmitAsync();
        Assert.Equal("PSH-501-0001", resubmitted.SheetSubmissionCode);
        Assert.NotEqual(7, resubmitted.TimesheetProjectSubmissionId);

        var rows = await Factory.ReadTimeLogSubmissionsAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(1, rows.Single(row => row.TimesheetProjectSubmissionId == 7).IsDelete);
        Assert.Equal(0, rows.Single(row => row.TimesheetProjectSubmissionId == resubmitted.TimesheetProjectSubmissionId).IsDelete);
    }
}
