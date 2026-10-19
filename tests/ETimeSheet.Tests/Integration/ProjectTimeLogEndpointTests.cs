using System.Net;
using System.Net.Http.Json;
using ETimeSheet.Application.Models;
using ETimeSheet.Shared.Enums;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;
using static ETimeSheet.Tests.TestData.ProjectTimeLogTestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// The project time log endpoints of <c>TimesheetProjectController</c>, end to
/// end against a real SQL Server: <c>get-project-time-log</c>,
/// <c>add-edit-project-time-log</c> and <c>remove-project-time-log</c>, over
/// the recorded <c>dbo.TimesheetProjectTimeLog</c> and
/// <c>dbo.TimesheetProjectSubmission</c>.
/// <para>
/// This is where the parts that only exist as rows are proven: the
/// <c>PSH-{user}-0001</c> sheet sequence, the sheet a new entry joins, the
/// lock a submitted sheet puts on its entries, the soft delete, and the
/// duration the service works out from the clock times.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class ProjectTimeLogEndpointTests : IntegrationTestBase
{
    private const string ListUrl = BaseUrl + "/get-project-time-log";
    private const string SaveUrl = BaseUrl + "/add-edit-project-time-log";
    private const string RemoveUrl = BaseUrl + "/remove-project-time-log";

    public ProjectTimeLogEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // The controller is [AllowAnonymous] while authentication is off, so an
    // unauthenticated client is the honest way to call it.
    private HttpClient Client() => Factory.CreateClient();

    private Task<HttpResponseMessage> PostSaveAsync(TimesheetProjectTimeLogSaveRequest request) =>
        Client().PostAsJsonAsync(SaveUrl, request);

    private async Task<TimesheetProjectTimeLogResponse> SaveAsync(TimesheetProjectTimeLogSaveRequest request)
    {
        var response = await PostSaveAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<TimesheetProjectTimeLogResponse>();
    }

    private Task<HttpResponseMessage> PostRemoveAsync(int timesheetProjectTimeLogId, int deletedBy = ReviewerId) =>
        Client().PostAsJsonAsync(RemoveUrl, new TimesheetProjectTimeLogDeleteRequest
        {
            TimesheetProjectTimeLogId = timesheetProjectTimeLogId,
            DeletedBy = deletedBy,
        });

    private async Task<IReadOnlyList<TimesheetProjectTimeLogResponse>> ListAsync(string query)
    {
        var response = await Client().GetAsync($"{ListUrl}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<List<TimesheetProjectTimeLogResponse>>();
    }

    // ================================================================ get-project-time-log

    /// <summary>
    /// The whole table is never returned: without a user, project or sheet
    /// filter the call is refused - and a 0, or a date range on its own, is
    /// no filter at all.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("?userID=0&projectID=0")]
    [InlineData("?fromDate=2026-03-01&toDate=2026-03-31")]
    [InlineData("?sheetSubmissionCode=%20%20")]
    public async Task ListRefusesACallWithNoUserProjectOrSheetFilter(string query)
    {
        var response = await Client().GetAsync(ListUrl + query);

        await response.AssertTimeLogValidationFailureAsync("userID");
    }

    [Fact]
    public async Task ListRefusesNegativeIdsNamingEachOne()
    {
        var response = await Client().GetAsync($"{ListUrl}?userID=-1&projectID=-2");

        await response.AssertTimeLogValidationFailureAsync("userID", "projectID");
    }

    [Fact]
    public async Task ListRefusesAToDateBeforeTheFromDate()
    {
        var response = await Client().GetAsync($"{ListUrl}?userID={UserId}&fromDate=2026-03-09&toDate=2026-03-08");

        await response.AssertTimeLogValidationFailureAsync("toDate");
    }

    [Fact]
    public async Task ListIsEmptyWhenNothingMatches()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        Assert.Empty(await ListAsync($"userID={UserId}"));
    }

    /// <summary>
    /// The user filter returns only that user's entries, ordered by day, then
    /// start time, then id - and each one comes back with the user and the
    /// project named from <c>dbo.Signup</c> and <c>dbo.TimesheetProject</c>.
    /// </summary>
    [Fact]
    public async Task ListByUserReturnsTheirEntriesInOrderWithNamesJoined()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var code = SheetCode(UserId, 1);

        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, code, new DateTime(2026, 3, 9), startTime: "14:00:00", projectId: BetaProjectId),
            Entry(42, code, new DateTime(2026, 3, 9), startTime: "08:00:00", durationMinutes: 45, status: 2),
            Entry(43, code, new DateTime(2026, 3, 2), startTime: "16:00:00"),
            Entry(44, SheetCode(OtherUserId, 1), new DateTime(2026, 3, 9), userId: OtherUserId));

        var entries = await ListAsync($"userID={UserId}");

        Assert.Equal(new[] { 43, 42, 41 }, entries.Select(entry => entry.TimesheetProjectTimeLogId));

        var draft = entries[1];
        Assert.Equal(UserId, draft.UserId);
        Assert.Equal(UserName, draft.UserName);
        Assert.Equal(AlphaProjectId, draft.ProjectId);
        Assert.Equal(AlphaProjectCode, draft.ProjectCode);
        Assert.Equal(AlphaProjectName, draft.ProjectName);
        Assert.Equal(code, draft.SheetSubmissionCode);
        Assert.Equal(new DateTime(2026, 3, 9), draft.LogDate);
        Assert.Equal(new TimeSpan(8, 0, 0), draft.StartTime);
        Assert.Equal(new TimeSpan(8, 45, 0), draft.EndTime);
        Assert.Equal(45, draft.DurationMinutes);
        Assert.Equal("Seeded work", draft.Description);
        Assert.Equal(TimeLogStatus.Draft, draft.Status);
        Assert.Equal("Draft", draft.StatusName);
        Assert.False(draft.IsSubmitted);
        Assert.Equal(SeededCreateDate, draft.CreateDate);
        Assert.Equal(UserId, draft.CreatedBy);
        Assert.Null(draft.UpdateDate);
        Assert.Null(draft.UpdatedBy);

        Assert.Equal(BetaProjectName, entries[2].ProjectName);
        Assert.Equal("Save", entries[2].StatusName);
    }

    [Fact]
    public async Task ListByProjectReturnsEveryUsersEntriesOnIt()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, SheetCode(UserId, 1), new DateTime(2026, 3, 2)),
            Entry(42, SheetCode(UserId, 1), new DateTime(2026, 3, 3), projectId: BetaProjectId),
            Entry(43, SheetCode(OtherUserId, 1), new DateTime(2026, 3, 4), userId: OtherUserId));

        var entries = await ListAsync($"projectID={AlphaProjectId}");

        Assert.Equal(new[] { 41, 43 }, entries.Select(entry => entry.TimesheetProjectTimeLogId));
        Assert.Equal(new[] { UserName, OtherUserName }, entries.Select(entry => entry.UserName));
    }

    [Fact]
    public async Task ListBySheetCodeReturnsOnlyThatSheet()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, SheetCode(UserId, 1), new DateTime(2026, 3, 2)),
            Entry(42, SheetCode(UserId, 2), new DateTime(2026, 3, 3)),
            Entry(43, SheetCode(UserId, 2), new DateTime(2026, 3, 4), projectId: BetaProjectId));

        var entries = await ListAsync($"sheetSubmissionCode={SheetCode(UserId, 2)}");

        Assert.Equal(new[] { 42, 43 }, entries.Select(entry => entry.TimesheetProjectTimeLogId));
    }

    [Fact]
    public async Task ListCombinesTheUserAndProjectFilters()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, SheetCode(UserId, 1), new DateTime(2026, 3, 2)),
            Entry(42, SheetCode(UserId, 1), new DateTime(2026, 3, 3), projectId: BetaProjectId),
            Entry(43, SheetCode(OtherUserId, 1), new DateTime(2026, 3, 4), userId: OtherUserId));

        var entry = Assert.Single(await ListAsync($"userID={UserId}&projectID={BetaProjectId}"));

        Assert.Equal(42, entry.TimesheetProjectTimeLogId);
    }

    /// <summary>
    /// Both ends of the range are included, and any time part sent is
    /// dropped - otherwise a <c>toDate</c> with a time would exclude the
    /// whole of that day's <c>date</c> column, and a <c>fromDate</c> with one
    /// would exclude its own day.
    /// </summary>
    [Fact]
    public async Task ListDateRangeIsInclusiveAndIgnoresTheTimePart()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, SheetCode(UserId, 1), new DateTime(2026, 3, 4)),
            Entry(42, SheetCode(UserId, 1), new DateTime(2026, 3, 5)),
            Entry(43, SheetCode(UserId, 1), new DateTime(2026, 3, 9)),
            Entry(44, SheetCode(UserId, 1), new DateTime(2026, 3, 10)));

        var entries = await ListAsync(
            $"userID={UserId}&fromDate=2026-03-05T18:00:00&toDate=2026-03-09T15:30:00");

        Assert.Equal(new[] { 42, 43 }, entries.Select(entry => entry.TimesheetProjectTimeLogId));
    }

    [Fact]
    public async Task ListLeavesOutDeletedEntries()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, SheetCode(UserId, 1)),
            Entry(42, SheetCode(UserId, 1), isDeleted: true));

        var entry = Assert.Single(await ListAsync($"userID={UserId}"));

        Assert.Equal(41, entry.TimesheetProjectTimeLogId);
    }

    /// <summary>
    /// <c>isSubmitted</c> is true only while the entry's sheet is handed in
    /// and not rejected: a rejected sheet is open again, a removed one no
    /// longer exists, and a sheet with no submission row is still open.
    /// </summary>
    [Fact]
    public async Task ListMarksEntriesOnASubmittedOrApprovedSheetAsSubmitted()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, "PSH-501-0001", new DateTime(2026, 3, 1)),
            Entry(42, "PSH-501-0002", new DateTime(2026, 3, 2)),
            Entry(43, "PSH-501-0003", new DateTime(2026, 3, 3)),
            Entry(44, "PSH-501-0004", new DateTime(2026, 3, 4)),
            Entry(45, "PSH-501-0005", new DateTime(2026, 3, 5)));

        await Factory.SeedTimeLogSubmissionsAsync(
            Sheet(1, "PSH-501-0001", statusId: 1),
            Sheet(2, "PSH-501-0002", statusId: 2),
            Sheet(3, "PSH-501-0003", statusId: 3),
            Sheet(4, "PSH-501-0004", statusId: 1, isDeleted: true));

        var entries = await ListAsync($"userID={UserId}");

        Assert.Equal(
            new[] { true, true, false, false, false },
            entries.Select(entry => entry.IsSubmitted));
    }

    /// <summary>
    /// No foreign key protects <c>UserID</c> or <c>ProjectID</c>, so an entry
    /// can name a user or project that does not exist. The LEFT JOINs keep it
    /// listed, with null names, rather than letting it vanish.
    /// </summary>
    [Fact]
    public async Task ListStillReturnsAnEntryWhoseUserAndProjectDoNotExist()
    {
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-999-0001", userId: 999, projectId: 799));

        var entry = Assert.Single(await ListAsync("userID=999"));

        Assert.Equal(799, entry.ProjectId);
        Assert.Null(entry.UserName);
        Assert.Null(entry.ProjectCode);
        Assert.Null(entry.ProjectName);
    }

    // ================================================================ add-edit-project-time-log: add

    /// <summary>
    /// A user's very first entry opens their first sheet,
    /// <c>PSH-{user}-0001</c>. The duration is worked out from the clock
    /// times, the description is trimmed, the time part of the day is
    /// dropped, and the row is stamped live with the frozen clock.
    /// </summary>
    [Fact]
    public async Task AddLogsTheEntryOnTheUsersFirstSheet()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var response = await PostSaveAsync(SaveRequest(
            logDate: new DateTime(2026, 3, 9, 17, 45, 0),
            startTime: "09:00:00",
            endTime: "12:30:00",
            description: "  Wrote the importer  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<TimesheetProjectTimeLogResponse>();
        Assert.True(envelope.Success);
        Assert.Equal("Time log entry saved.", envelope.Message);

        var saved = envelope.Data!;
        Assert.Equal(UserId, saved.UserId);
        Assert.Equal(UserName, saved.UserName);
        Assert.Equal(AlphaProjectId, saved.ProjectId);
        Assert.Equal(AlphaProjectCode, saved.ProjectCode);
        Assert.Equal(AlphaProjectName, saved.ProjectName);
        Assert.Equal("PSH-501-0001", saved.SheetSubmissionCode);
        Assert.Equal(new DateTime(2026, 3, 9), saved.LogDate);
        Assert.Equal(new TimeSpan(9, 0, 0), saved.StartTime);
        Assert.Equal(new TimeSpan(12, 30, 0), saved.EndTime);
        Assert.Equal(210, saved.DurationMinutes);
        Assert.Equal("Wrote the importer", saved.Description);
        Assert.Equal(TimeLogStatus.Save, saved.Status);
        Assert.Equal("Save", saved.StatusName);
        Assert.False(saved.IsSubmitted);
        Assert.Equal(TimeLogTestData.Now, saved.CreateDate);
        Assert.Equal(UserId, saved.CreatedBy);
        Assert.Null(saved.UpdateDate);
        Assert.Null(saved.UpdatedBy);

        var row = Assert.Single(await Factory.ReadTimeLogEntriesAsync());
        Assert.Equal(saved.TimesheetProjectTimeLogId, row.TimesheetProjectTimeLogId);
        Assert.Equal("PSH-501-0001", row.SheetSubmissionCode);
        Assert.Equal(new DateTime(2026, 3, 9), row.LogDate);
        Assert.Equal(210, row.DurationMinutes);
        Assert.Equal("Wrote the importer", row.Description);
        Assert.Equal(1, row.Status);
        Assert.Equal(0, row.IsDelete);
        Assert.Equal(TimeLogTestData.Now, row.CreateDate);
        Assert.Equal(UserId, row.CreatedBy);
        Assert.Null(row.DeletedDate);
        Assert.Null(row.DeletedBy);
    }

    /// <summary>
    /// An end before the start is read as running past midnight, not as a
    /// negative duration: 22:00 to 02:00 is four hours.
    /// </summary>
    [Theory]
    [InlineData("22:00:00", "02:00:00", 240)]
    [InlineData("23:59:00", "00:00:00", 1)]
    [InlineData("00:00:00", "23:59:59", 1439)]
    [InlineData("08:15:00", "08:45:30", 30)]
    public async Task AddWorksOutTheDurationFromTheClockTimes(string start, string end, int expectedMinutes)
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var saved = await SaveAsync(SaveRequest(startTime: start, endTime: end));

        Assert.Equal(expectedMinutes, saved.DurationMinutes);
        Assert.Equal(expectedMinutes, (await Factory.ReadTimeLogEntryAsync(saved.TimesheetProjectTimeLogId))!.DurationMinutes);
    }

    [Fact]
    public async Task AddSavesADraftAndAnEmptyDescriptionAsNull()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var saved = await SaveAsync(SaveRequest(description: "   ", status: TimeLogStatus.Draft));

        Assert.Equal(TimeLogStatus.Draft, saved.Status);
        Assert.Null(saved.Description);

        var row = (await Factory.ReadTimeLogEntryAsync(saved.TimesheetProjectTimeLogId))!;
        Assert.Equal(2, row.Status);
        Assert.Null(row.Description);
    }

    /// <summary>
    /// One sheet covers everything a user logs until they submit, across
    /// every project - and the sequence is per user, so two users each start
    /// at 0001.
    /// </summary>
    [Fact]
    public async Task AddPutsEveryEntryBeforeASubmissionOnOneSheetPerUser()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var first = await SaveAsync(SaveRequest());
        var otherProject = await SaveAsync(SaveRequest(projectId: BetaProjectId, logDate: new DateTime(2026, 3, 10)));
        var otherUser = await SaveAsync(SaveRequest(userId: OtherUserId));

        Assert.Equal("PSH-501-0001", first.SheetSubmissionCode);
        Assert.Equal("PSH-501-0001", otherProject.SheetSubmissionCode);
        Assert.Equal("PSH-502-0001", otherUser.SheetSubmissionCode);

        Assert.Equal(
            new[] { "PSH-501-0001", "PSH-501-0001", "PSH-502-0001" },
            (await Factory.ReadTimeLogEntriesAsync()).Select(row => row.SheetSubmissionCode));
    }

    /// <summary>
    /// Submitting is what ends a sheet: once it is in - awaiting review or
    /// approved - the next entry starts the next code.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AddStartsTheNextSheetOnceTheLastOneIsSubmitted(int statusId)
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001"));
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(1, "PSH-501-0001", statusId: statusId, totalMinutes: 60));

        var saved = await SaveAsync(SaveRequest());

        Assert.Equal("PSH-501-0002", saved.SheetSubmissionCode);
    }

    /// <summary>
    /// A rejected sheet is open again, so the correction is logged back onto
    /// the sheet it was rejected on rather than onto a new one.
    /// </summary>
    [Fact]
    public async Task AddJoinsARejectedSheet()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001"));
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(1, "PSH-501-0001", statusId: 3, totalMinutes: 60));

        var saved = await SaveAsync(SaveRequest());

        Assert.Equal("PSH-501-0001", saved.SheetSubmissionCode);
    }

    /// <summary>
    /// An empty sheet has no entries, so its code lives only on the
    /// submission. It is still spent: the next entry must not reuse it.
    /// </summary>
    [Fact]
    public async Task AddDoesNotReuseTheCodeOfAnEmptySheetAlreadySubmitted()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(1, "PSH-501-0001", statusId: 1, submittedBy: UserId));

        var saved = await SaveAsync(SaveRequest());

        Assert.Equal("PSH-501-0002", saved.SheetSubmissionCode);
    }

    /// <summary>
    /// A deleted entry still owns its code, so a sheet whose only entry was
    /// removed is still the user's open sheet and is not handed out afresh.
    /// </summary>
    [Fact]
    public async Task AddJoinsTheOpenSheetEvenWhenAllItsEntriesWereDeleted()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, "PSH-501-0001"),
            Entry(42, "PSH-501-0002", isDeleted: true));
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(1, "PSH-501-0001", statusId: 2, totalMinutes: 60));

        var saved = await SaveAsync(SaveRequest());

        Assert.Equal("PSH-501-0002", saved.SheetSubmissionCode);
    }

    /// <summary>
    /// The next code follows the highest one the user has reached, and a
    /// code not of the <c>PSH-{user}-</c> shape - written by hand, or by an
    /// older rule - is ignored rather than stalling the sequence.
    /// </summary>
    [Fact]
    public async Task AddContinuesFromTheHighestSequenceAndIgnoresForeignCodes()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, "PSH-501-0007"),
            Entry(42, "LEGACY-77"));
        await Factory.SeedTimeLogSubmissionsAsync(
            Sheet(1, "PSH-501-0007", statusId: 2, totalMinutes: 60),
            Sheet(2, "LEGACY-77", statusId: 2, totalMinutes: 60));

        var saved = await SaveAsync(SaveRequest());

        Assert.Equal("PSH-501-0008", saved.SheetSubmissionCode);
    }

    [Fact]
    public async Task AddTreatsAnIdOfZeroAsANewEntry()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001", description: "Untouched"));

        var saved = await SaveAsync(SaveRequest(timesheetProjectTimeLogId: 0));

        Assert.NotEqual(41, saved.TimesheetProjectTimeLogId);

        var rows = await Factory.ReadTimeLogEntriesAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal("Untouched", rows.Single(row => row.TimesheetProjectTimeLogId == 41).Description);
    }

    /// <summary>
    /// No foreign key ties an entry to an assignment, so the service checks:
    /// a user may only log time to a project they are assigned to, and a
    /// removed assignment does not count.
    /// </summary>
    [Fact]
    public async Task AddRefusesAProjectTheUserIsNotAssignedTo()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var response = await PostSaveAsync(SaveRequest(projectId: UnassignedProjectId));

        await response.AssertTimeLogValidationFailureAsync("UserId");
        Assert.Empty(await Factory.ReadTimeLogEntriesAsync());
    }

    [Fact]
    public async Task AddRefusesAnAssignmentThatHasBeenRemoved()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogAssignmentAsync(OtherUserId, BetaProjectId, isDeleted: true);

        var response = await PostSaveAsync(SaveRequest(userId: OtherUserId, projectId: BetaProjectId));

        await response.AssertTimeLogValidationFailureAsync("UserId");
        Assert.Empty(await Factory.ReadTimeLogEntriesAsync());
    }

    // ================================================================ add-edit-project-time-log: validation

    /// <summary>
    /// Every failing field is reported together, not just the first.
    /// </summary>
    [Fact]
    public async Task SaveReportsEveryMissingFieldTogether()
    {
        var response = await PostSaveAsync(new TimesheetProjectTimeLogSaveRequest());

        var errors = await response.AssertTimeLogValidationFailureAsync(
            "UserId", "ProjectId", "LogDate", "StartTime", "EndTime", "Status", "AddedBy");

        Assert.Equal(7, errors.Count);
    }

    [Fact]
    public async Task SaveRefusesANegativeId()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var response = await PostSaveAsync(SaveRequest(timesheetProjectTimeLogId: -1));

        await response.AssertTimeLogValidationFailureAsync("TimesheetProjectTimeLogId");
    }

    /// <summary>
    /// Times of day are exactly <c>hh:mm:ss</c> inside one day: no missing
    /// seconds, no day component, no 24:00, no negative times, no words.
    /// </summary>
    [Theory]
    [InlineData("09:00")]
    [InlineData("24:00:00")]
    [InlineData("1.06:00:00")]
    [InlineData("-04:00:00")]
    [InlineData("nine")]
    public async Task SaveRefusesAMalformedTimeOfDay(string value)
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var response = await PostSaveAsync(SaveRequest(startTime: value, endTime: value));

        await response.AssertTimeLogValidationFailureAsync("StartTime", "EndTime");
        Assert.Empty(await Factory.ReadTimeLogEntriesAsync());
    }

    [Fact]
    public async Task SaveRefusesAnEntryThatEndsWhenItStarts()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var response = await PostSaveAsync(SaveRequest(startTime: "09:00:00", endTime: "09:00:00"));

        var errors = await response.AssertTimeLogValidationFailureAsync("EndTime");
        Assert.DoesNotContain(errors, error => error.StartsWith("StartTime:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveRefusesAStatusOtherThanSaveOrDraft()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var response = await PostSaveAsync(SaveRequest(status: (TimeLogStatus)3));

        await response.AssertTimeLogValidationFailureAsync("Status");
    }

    // ================================================================ add-edit-project-time-log: edit

    /// <summary>
    /// An edit overwrites the entry as sent - a description left out is saved
    /// as null - and stamps who changed it and when, while the creation stamp
    /// and the sheet stay as they were.
    /// </summary>
    [Fact]
    public async Task EditOverwritesTheEntryAndKeepsItsSheet()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001"));

        var saved = await SaveAsync(SaveRequest(
            timesheetProjectTimeLogId: 41,
            projectId: BetaProjectId,
            logDate: new DateTime(2026, 3, 6),
            startTime: "13:00:00",
            endTime: "14:15:00",
            description: null,
            status: TimeLogStatus.Draft,
            addedBy: ReviewerId));

        Assert.Equal(41, saved.TimesheetProjectTimeLogId);
        Assert.Equal("PSH-501-0001", saved.SheetSubmissionCode);
        Assert.Equal(BetaProjectName, saved.ProjectName);
        Assert.Equal(75, saved.DurationMinutes);
        Assert.Null(saved.Description);
        Assert.Equal(TimeLogStatus.Draft, saved.Status);
        Assert.Equal(TimeLogTestData.Now, saved.UpdateDate);
        Assert.Equal(ReviewerId, saved.UpdatedBy);
        Assert.Equal(SeededCreateDate, saved.CreateDate);
        Assert.Equal(UserId, saved.CreatedBy);

        var row = Assert.Single(await Factory.ReadTimeLogEntriesAsync());
        Assert.Equal(41, row.TimesheetProjectTimeLogId);
        Assert.Equal(BetaProjectId, row.ProjectId);
        Assert.Equal(new DateTime(2026, 3, 6), row.LogDate);
        Assert.Equal(new TimeSpan(13, 0, 0), row.StartTime);
        Assert.Equal(new TimeSpan(14, 15, 0), row.EndTime);
        Assert.Equal(75, row.DurationMinutes);
        Assert.Null(row.Description);
        Assert.Equal(2, row.Status);
        Assert.Equal(TimeLogTestData.Now, row.UpdateDate);
        Assert.Equal(ReviewerId, row.UpdatedBy);
        Assert.Equal(SeededCreateDate, row.CreateDate);
    }

    /// <summary>
    /// An entry never moves between sheets: editing one on a rejected sheet
    /// leaves it there, even though the user's newest open sheet is another.
    /// </summary>
    [Fact]
    public async Task EditNeverMovesAnEntryToTheUsersNewerSheet()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, "PSH-501-0001"),
            Entry(42, "PSH-501-0002"));
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(1, "PSH-501-0001", statusId: 3, totalMinutes: 60));

        var saved = await SaveAsync(SaveRequest(timesheetProjectTimeLogId: 41, startTime: "10:00:00", endTime: "11:00:00"));

        Assert.Equal("PSH-501-0001", saved.SheetSubmissionCode);
        Assert.Equal("PSH-501-0001", (await Factory.ReadTimeLogEntryAsync(41))!.SheetSubmissionCode);
    }

    /// <summary>
    /// A sheet handed in - awaiting review or approved - freezes its entries.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EditIsRefusedWhileTheSheetIsSubmitted(int statusId)
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001", description: "Frozen"));
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(1, "PSH-501-0001", statusId: statusId, totalMinutes: 60));

        var response = await PostSaveAsync(SaveRequest(timesheetProjectTimeLogId: 41, description: "Changed"));

        await response.AssertTimeLogFailureAsync(HttpStatusCode.Conflict);

        var row = (await Factory.ReadTimeLogEntryAsync(41))!;
        Assert.Equal("Frozen", row.Description);
        Assert.Null(row.UpdateDate);
    }

    [Fact]
    public async Task EditIsAllowedOnceTheSheetIsRejected()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001"));
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(1, "PSH-501-0001", statusId: 3, totalMinutes: 60));

        var saved = await SaveAsync(SaveRequest(timesheetProjectTimeLogId: 41, description: "Corrected"));

        Assert.Equal("Corrected", saved.Description);
        Assert.Equal("Corrected", (await Factory.ReadTimeLogEntryAsync(41))!.Description);
    }

    [Fact]
    public async Task EditOfAnUnknownEntryIsNotFound()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();

        var response = await PostSaveAsync(SaveRequest(timesheetProjectTimeLogId: 999));

        await response.AssertTimeLogFailureAsync(HttpStatusCode.NotFound);
        Assert.Empty(await Factory.ReadTimeLogEntriesAsync());
    }

    [Fact]
    public async Task EditOfADeletedEntryIsNotFound()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001", isDeleted: true));

        var response = await PostSaveAsync(SaveRequest(timesheetProjectTimeLogId: 41));

        await response.AssertTimeLogFailureAsync(HttpStatusCode.NotFound);
        Assert.Equal(1, (await Factory.ReadTimeLogEntryAsync(41))!.IsDelete);
    }

    /// <summary>
    /// The assignment check runs on an edit too: an entry cannot be moved to
    /// a project its user is not assigned to.
    /// </summary>
    [Fact]
    public async Task EditRefusesMovingTheEntryToAnUnassignedProject()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001"));

        var response = await PostSaveAsync(SaveRequest(timesheetProjectTimeLogId: 41, projectId: UnassignedProjectId));

        await response.AssertTimeLogValidationFailureAsync("UserId");
        Assert.Equal(AlphaProjectId, (await Factory.ReadTimeLogEntryAsync(41))!.ProjectId);
    }

    // ================================================================ remove-project-time-log

    /// <summary>
    /// A remove is a soft delete: the row stays, stamped with who removed it
    /// and when, and drops out of every read.
    /// </summary>
    [Fact]
    public async Task RemoveSoftDeletesTheEntry()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(
            Entry(41, "PSH-501-0001"),
            Entry(42, "PSH-501-0001", new DateTime(2026, 3, 8)));

        var response = await PostRemoveAsync(41, deletedBy: ReviewerId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.ReadEnvelopeAsync<object>();
        Assert.True(envelope.Success);
        Assert.Equal("Time log entry removed.", envelope.Message);

        var row = (await Factory.ReadTimeLogEntryAsync(41))!;
        Assert.Equal(1, row.IsDelete);
        Assert.Equal(TimeLogTestData.Now, row.DeletedDate);
        Assert.Equal(ReviewerId, row.DeletedBy);

        Assert.Equal(0, (await Factory.ReadTimeLogEntryAsync(42))!.IsDelete);

        var listed = Assert.Single(await ListAsync($"userID={UserId}"));
        Assert.Equal(42, listed.TimesheetProjectTimeLogId);
    }

    [Fact]
    public async Task RemovingTheSameEntryTwiceIsNotFound()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001"));

        Assert.Equal(HttpStatusCode.OK, (await PostRemoveAsync(41)).StatusCode);

        await (await PostRemoveAsync(41)).AssertTimeLogFailureAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveOfAnUnknownEntryIsNotFound()
    {
        await (await PostRemoveAsync(999)).AssertTimeLogFailureAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveRefusesMissingIdsNamingBoth()
    {
        var response = await PostRemoveAsync(0, deletedBy: 0);

        await response.AssertTimeLogValidationFailureAsync("TimesheetProjectTimeLogId", "DeletedBy");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RemoveIsRefusedWhileTheSheetIsSubmitted(int statusId)
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001"));
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(1, "PSH-501-0001", statusId: statusId, totalMinutes: 60));

        await (await PostRemoveAsync(41)).AssertTimeLogFailureAsync(HttpStatusCode.Conflict);

        var row = (await Factory.ReadTimeLogEntryAsync(41))!;
        Assert.Equal(0, row.IsDelete);
        Assert.Null(row.DeletedBy);
    }

    [Fact]
    public async Task RemoveIsAllowedOnceTheSheetIsRejected()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0001"));
        await Factory.SeedTimeLogSubmissionsAsync(Sheet(1, "PSH-501-0001", statusId: 3, totalMinutes: 60));

        Assert.Equal(HttpStatusCode.OK, (await PostRemoveAsync(41)).StatusCode);
        Assert.Equal(1, (await Factory.ReadTimeLogEntryAsync(41))!.IsDelete);
    }

    /// <summary>
    /// Removing the only entry on the open sheet does not free its code: the
    /// next entry still lands on the same sheet.
    /// </summary>
    [Fact]
    public async Task RemovingTheLastEntryKeepsTheSheetCodeForTheNextOne()
    {
        await Factory.SeedTimeLogUsersAndProjectsAsync();
        await Factory.SeedTimeLogEntriesAsync(Entry(41, "PSH-501-0003"));

        Assert.Equal(HttpStatusCode.OK, (await PostRemoveAsync(41)).StatusCode);

        var saved = await SaveAsync(SaveRequest());

        Assert.Equal("PSH-501-0003", saved.SheetSubmissionCode);
    }
}
