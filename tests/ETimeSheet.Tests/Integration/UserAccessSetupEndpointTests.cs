using System.Net;
using System.Net.Http.Json;
using ETimeSheet.Application.Models;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;
using static ETimeSheet.Tests.TestData.UserAccessTestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// The user access setup endpoints of <c>TimesheetProjectController</c> - the
/// module assignments in <c>dbo.Timesheet_role_assignment</c> - end to end
/// against a real SQL Server: the recorded <c>spc_GetUserAccessSetup</c> and
/// <c>spc_AddEditUserAccessSetup</c>, the EF hard delete, and the real
/// middleware.
/// <para>
/// The table has no organisation column, so the organisation filter goes
/// through the user's <c>dbo.Signup</c> row; only a database can prove that
/// join, its "live users only" rule, and the one-assignment-per-user check.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class UserAccessSetupEndpointTests : IntegrationTestBase
{
    private const string ListUrl = "/api/v1/TimesheetProject/get-user-access-setup";
    private const string SaveUrl = "/api/v1/TimesheetProject/add-edit-user-access-setup";
    private const string RemoveUrl = "/api/v1/TimesheetProject/remove-user-access-setup";

    public UserAccessSetupEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // The controller is [AllowAnonymous] while authentication is off, so an
    // unauthenticated client is the honest way to call it.
    private HttpClient Client() => Factory.CreateClient();

    private async Task<List<TimesheetRoleAssignmentResponse>> ListAsync(string query)
    {
        var response = await Client().GetAsync(ListUrl + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<List<TimesheetRoleAssignmentResponse>>();
    }

    private async Task<TimesheetRoleAssignmentResponse> SaveAsync(TimesheetRoleAssignmentSaveRequest request)
    {
        var response = await Client().PostAsJsonAsync(SaveUrl, request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<TimesheetRoleAssignmentResponse>();
    }

    private Task<HttpResponseMessage> RemoveAsync(int assignmentId) =>
        Client().PostAsJsonAsync(RemoveUrl, new TimesheetRoleAssignmentDeleteRequest { AssignmentId = assignmentId });

    /// <summary>A 400 in the error envelope, with one <c>"Field: message"</c> entry per named field.</summary>
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

    // ================================================================ list

    [Theory]
    [InlineData("")]
    [InlineData("?userID=0")]
    [InlineData("?orgID=0&userID=0")]
    public async Task ListWithNoFilterIsABadRequestNamingOrgId(string query)
    {
        var response = await Client().GetAsync(ListUrl + query);

        await AssertBadRequestNamingAsync(response, "orgID");
    }

    [Fact]
    public async Task ListRejectsNegativeFiltersNamingEach()
    {
        var response = await Client().GetAsync($"{ListUrl}?orgID=-1&userID=-1");

        await AssertBadRequestNamingAsync(response, "orgID", "userID");
    }

    [Fact]
    public async Task ListRejectsANonNumericUserId()
    {
        var response = await Client().GetAsync($"{ListUrl}?userID=abc");

        await AssertBadRequestNamingAsync(response, "userID");
    }

    [Fact]
    public async Task ListIsEmptyWhenNothingMatches()
    {
        await Factory.SeedAccessUsersAsync();

        Assert.Empty(await ListAsync($"?orgID={OrganizationId}"));
    }

    /// <summary>
    /// <c>orgID</c> alone: the assignments of the organisation's live users,
    /// ordered by user name rather than id. A removed user's assignment,
    /// another organisation's, and one whose user has no <c>dbo.Signup</c>
    /// row at all are left out.
    /// </summary>
    [Fact]
    public async Task ListByOrganisationReturnsLiveUsersAssignmentsOrderedByName()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(
            Assignment(101, CharlieUserId),
            Assignment(102, AliceUserId),
            Assignment(103, OtherOrganizationUserId),
            Assignment(104, RemovedUserId),
            Assignment(105, UnknownUserId));

        var assignments = await ListAsync($"?orgID={OrganizationId}");

        Assert.Equal(new[] { 102, 101 }, assignments.Select(assignment => assignment.AssignmentId));
    }

    [Fact]
    public async Task ListByUserReturnsThatUsersAssignmentWhateverTheOrganisation()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(
            Assignment(101, CharlieUserId),
            Assignment(103, OtherOrganizationUserId));

        var assignment = Assert.Single(await ListAsync($"?userID={OtherOrganizationUserId}"));

        Assert.Equal(103, assignment.AssignmentId);
        Assert.Equal(OtherOrganizationId, assignment.OrganizationId);
    }

    [Fact]
    public async Task ListWithAUserOutsideTheGivenOrganisationIsEmpty()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(Assignment(103, OtherOrganizationUserId));

        Assert.Empty(await ListAsync($"?orgID={OrganizationId}&userID={OtherOrganizationUserId}"));
    }

    [Fact]
    public async Task ListByUserLeavesOutARemovedUser()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(Assignment(104, RemovedUserId));

        Assert.Empty(await ListAsync($"?userID={RemovedUserId}"));
    }

    [Fact]
    public async Task ListCarriesTheUserTheModuleFlagsAndTheAuditColumns()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(Assignment(101, CharlieUserId, isProject: true));

        var assignment = Assert.Single(await ListAsync($"?orgID={OrganizationId}&userID={CharlieUserId}"));

        Assert.Equal(CharlieUserId, assignment.UserId);
        Assert.Equal(CharlieName, assignment.UserName);
        Assert.Equal(EmailOf(CharlieUserId), assignment.Email);
        Assert.Equal(OrganizationId, assignment.OrganizationId);
        Assert.Equal(true, assignment.IsClientSetup);
        Assert.Equal(true, assignment.IsProject);
        Assert.Equal(false, assignment.IsProjectBudget);
        Assert.Equal(false, assignment.IsProjectCategory);
        Assert.Equal(false, assignment.IsStatusSetup);
        Assert.Equal(false, assignment.IsRoleSetup);
        Assert.Equal(false, assignment.IsUserSetup);
        Assert.Equal(true, assignment.IsTimeLog);
        Assert.Equal(SeededCreateDate, assignment.CreateDate);
        Assert.Equal(SeedCreatorUserId, assignment.CreatedBy);
        Assert.Null(assignment.UpdatedDate);
        Assert.Null(assignment.UpdatedBy);
    }

    // ================================================================ add

    [Fact]
    public async Task AddingAnAssignmentStoresItAndAnswersWithTheUser()
    {
        await Factory.SeedAccessUsersAsync();

        var saved = await SaveAsync(AccessRequest());

        Assert.Equal(CharlieUserId, saved.UserId);
        Assert.Equal(CharlieName, saved.UserName);
        Assert.Equal(EmailOf(CharlieUserId), saved.Email);
        Assert.Equal(OrganizationId, saved.OrganizationId);
        Assert.Equal(false, saved.IsClientSetup);
        Assert.Equal(true, saved.IsProject);
        Assert.Equal(true, saved.IsProjectBudget);
        Assert.Equal(true, saved.IsStatusSetup);
        Assert.Equal(false, saved.IsTimeLog);
        Assert.Equal(TimeLogTestData.Now, saved.CreateDate);
        Assert.Equal(LoginUserId, saved.CreatedBy);
        Assert.Null(saved.UpdatedDate);
        Assert.Null(saved.UpdatedBy);

        // And it is what landed in the table, not only what was answered.
        var row = Assert.Single(await Factory.AllAccessAssignmentsAsync());
        Assert.Equal(saved.AssignmentId, row.AssignmentId);
        Assert.Equal(CharlieUserId, row.UserId);
        Assert.Equal(false, row.IsClientSetup);
        Assert.Equal(true, row.IsProject);
        Assert.Equal(true, row.IsProjectBudget);
        Assert.Equal(false, row.IsProjectCategory);
        Assert.Equal(true, row.IsStatusSetup);
        Assert.Equal(false, row.IsRoleSetup);
        Assert.Equal(false, row.IsUserSetup);
        Assert.Equal(false, row.IsTimeLog);
        Assert.Equal(TimeLogTestData.Now, row.CreateDate);
        Assert.Equal(LoginUserId, row.CreatedBy);
        Assert.Null(row.UpdatedDate);
        Assert.Null(row.UpdatedBy);
    }

    [Fact]
    public async Task AddingAMalformedAssignmentIsABadRequestNamingEveryFieldAndSavesNothing()
    {
        var request = new TimesheetRoleAssignmentSaveRequest
        {
            AssignmentId = -1,
            OrganizationId = 0,
            UserId = 0,
            LoginUserId = 0,
        };

        var response = await Client().PostAsJsonAsync(SaveUrl, request);

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetRoleAssignmentSaveRequest.AssignmentId),
            nameof(TimesheetRoleAssignmentSaveRequest.OrganizationId),
            nameof(TimesheetRoleAssignmentSaveRequest.UserId),
            nameof(TimesheetRoleAssignmentSaveRequest.LoginUserId));
        Assert.Empty(await Factory.AllAccessAssignmentsAsync());
    }

    /// <summary>
    /// No foreign key protects <c>UserID</c>: the user must be a live user of
    /// the organisation sent - not another organisation's, not removed, not unknown.
    /// </summary>
    [Theory]
    [InlineData(OtherOrganizationUserId)]
    [InlineData(RemovedUserId)]
    [InlineData(UnknownUserId)]
    public async Task AddingForAUserWhoIsNotALiveUserOfTheOrganisationIsABadRequest(int userId)
    {
        await Factory.SeedAccessUsersAsync();

        var response = await Client().PostAsJsonAsync(SaveUrl, AccessRequest(userId: userId));

        await AssertBadRequestNamingAsync(response, nameof(TimesheetRoleAssignmentSaveRequest.UserId));
        Assert.Empty(await Factory.AllAccessAssignmentsAsync());
    }

    [Fact]
    public async Task AddingASecondAssignmentForTheSameUserIsAConflict()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(Assignment(101, CharlieUserId));

        var response = await Client().PostAsJsonAsync(SaveUrl, AccessRequest());

        await AssertFailureAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(101, Assert.Single(await Factory.AllAccessAssignmentsAsync()).AssignmentId);
    }

    // ================================================================ edit

    /// <summary>
    /// An edit that keeps its user is not a conflict with itself. It
    /// overwrites every module flag, stamps who updated it and when, and
    /// leaves the creator alone.
    /// </summary>
    [Fact]
    public async Task EditingAnAssignmentOverwritesTheFlagsAndStampsTheUpdate()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(Assignment(101, CharlieUserId));

        var saved = await SaveAsync(AccessRequest(assignmentId: 101));

        Assert.Equal(101, saved.AssignmentId);
        Assert.Equal(false, saved.IsClientSetup);
        Assert.Equal(true, saved.IsProject);
        Assert.Equal(TimeLogTestData.Now, saved.UpdatedDate);
        Assert.Equal(LoginUserId, saved.UpdatedBy);
        Assert.Equal(SeededCreateDate, saved.CreateDate);
        Assert.Equal(SeedCreatorUserId, saved.CreatedBy);

        var row = Assert.Single(await Factory.AllAccessAssignmentsAsync());
        Assert.Equal(false, row.IsClientSetup);
        Assert.Equal(true, row.IsProject);
        Assert.Equal(true, row.IsProjectBudget);
        Assert.Equal(true, row.IsStatusSetup);
        Assert.Equal(false, row.IsTimeLog);
        Assert.Equal(TimeLogTestData.Now, row.UpdatedDate);
        Assert.Equal(LoginUserId, row.UpdatedBy);
        Assert.Equal(SeededCreateDate, row.CreateDate);
        Assert.Equal(SeedCreatorUserId, row.CreatedBy);
    }

    [Fact]
    public async Task EditingCanMoveAnAssignmentToAUserWhoHasNone()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(Assignment(101, CharlieUserId));

        var saved = await SaveAsync(AccessRequest(assignmentId: 101, userId: AliceUserId));

        Assert.Equal(101, saved.AssignmentId);
        Assert.Equal(AliceUserId, saved.UserId);
        Assert.Equal(AliceName, saved.UserName);
        Assert.Equal(AliceUserId, (await Factory.FindAccessAssignmentAsync(101))!.UserId);
    }

    [Fact]
    public async Task EditingAnAssignmentOntoAUserWhoAlreadyHasOneIsAConflict()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(
            Assignment(101, CharlieUserId),
            Assignment(102, AliceUserId));

        var response = await Client().PostAsJsonAsync(SaveUrl, AccessRequest(assignmentId: 102, userId: CharlieUserId));

        await AssertFailureAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(AliceUserId, (await Factory.FindAccessAssignmentAsync(102))!.UserId);
    }

    /// <summary>An unknown id is a 404 - it does not quietly become an add.</summary>
    [Fact]
    public async Task EditingAnUnknownAssignmentIsNotFoundAndCreatesNothing()
    {
        await Factory.SeedAccessUsersAsync();

        var response = await Client().PostAsJsonAsync(SaveUrl, AccessRequest(assignmentId: 555));

        await AssertFailureAsync(response, HttpStatusCode.NotFound);
        Assert.Empty(await Factory.AllAccessAssignmentsAsync());
    }

    // ================================================================ remove

    /// <summary>A hard delete: the table has no soft-delete column, so the row is gone.</summary>
    [Fact]
    public async Task RemovingAnAssignmentDeletesTheRow()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(
            Assignment(101, CharlieUserId),
            Assignment(102, AliceUserId));

        var response = await RemoveAsync(101);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadEnvelopeAsync<object>()).Success);
        Assert.Null(await Factory.FindAccessAssignmentAsync(101));
        Assert.Equal(102, Assert.Single(await ListAsync($"?orgID={OrganizationId}")).AssignmentId);
    }

    [Fact]
    public async Task RemovingAnAssignmentTwiceIsNotFoundTheSecondTime()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(Assignment(101, CharlieUserId));

        Assert.Equal(HttpStatusCode.OK, (await RemoveAsync(101)).StatusCode);

        await AssertFailureAsync(await RemoveAsync(101), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemovingAnUnknownAssignmentIsNotFound()
    {
        await AssertFailureAsync(await RemoveAsync(555), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task RemovingWithANonPositiveIdIsABadRequestNamingIt(int assignmentId)
    {
        await AssertBadRequestNamingAsync(
            await RemoveAsync(assignmentId),
            nameof(TimesheetRoleAssignmentDeleteRequest.AssignmentId));
    }

    [Fact]
    public async Task AfterRemovalTheUserMayBeGivenANewAssignment()
    {
        await Factory.SeedAccessUsersAsync();
        await Factory.SeedAccessAssignmentsAsync(Assignment(101, CharlieUserId));

        Assert.Equal(HttpStatusCode.OK, (await RemoveAsync(101)).StatusCode);

        var saved = await SaveAsync(AccessRequest());

        Assert.Equal(CharlieUserId, saved.UserId);
        Assert.NotEqual(101, saved.AssignmentId);
        Assert.Equal(saved.AssignmentId, Assert.Single(await Factory.AllAccessAssignmentsAsync()).AssignmentId);
    }
}
