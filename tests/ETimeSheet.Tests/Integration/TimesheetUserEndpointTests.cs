using System.Net;
using System.Net.Http.Json;
using ETimeSheet.Application.Models;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;
using static ETimeSheet.Tests.TestData.TimesheetProjectTestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// The timesheet user endpoints of <c>TimesheetProjectController</c> -
/// create, edit, remove and list a user's project assignments - end to end
/// against a real SQL Server: <c>dbo.Signup</c>,
/// <c>dbo.Timesheet_Users_Project_setup</c>, the recorded
/// <c>spc_GetTimesheetUserListByOrgID</c>, and the real middleware.
/// <para>
/// The arrangement every test starts from: projects 1 "Alpha" and 2 "Beta" in
/// <see cref="TimesheetProjectTestData.OrganizationId"/>, project 3 in
/// another organisation; role 5 "Developer" and a deleted role 7 in the
/// organisation, role 6 in the other one.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class TimesheetUserEndpointTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/v1/TimesheetProject";
    private const string CreateUrl = BaseUrl + "/create-timesheet-user";
    private const string EditUrl = BaseUrl + "/edit-timesheet-user";
    private const string RemoveUrl = BaseUrl + "/remove-timesheet-user";
    private static readonly string ListUrl = $"{BaseUrl}/get-timesheet-user-by-orgID/{OrganizationId}";

    private const int PriyaUserId = 101;
    private const string PriyaEmail = "priya.shah@etimesheet.test";

    public TimesheetUserEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // The controller is [AllowAnonymous] while authentication is off, so an
    // unauthenticated client is the honest way to call it.
    private HttpClient Client() => Factory.CreateClient();

    /// <summary>The projects and roles described on the class.</summary>
    private async Task SeedProjectsAndRolesAsync()
    {
        await Factory.SeedProjectsAsync(
            Project(1, "PR0001", "Alpha"),
            Project(2, "PR0002", "Beta"),
            Project(3, "PR0003", "Theirs", organizationId: OtherOrganizationId));

        await Factory.SeedUserRolesAsync(
            Role(5, "Developer"),
            Role(6, "Their Role", organizationId: OtherOrganizationId),
            Role(7, "Retired Role", isDeleted: true));
    }

    /// <summary>Priya, a live user of the organisation, on project 1 through assignment 11.</summary>
    private async Task SeedPriyaOnProjectOneAsync()
    {
        await SeedProjectsAndRolesAsync();
        await Factory.SeedEmployeesAsync(User(PriyaUserId, "Priya Shah", PriyaEmail));
        await Factory.SeedAssignmentsAsync(Assignment(11, PriyaUserId, projectId: 1));
    }

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

    private static async Task AssertBadRequestNotNamingAsync(HttpResponseMessage response, string field)
    {
        var envelope = await response.ReadEnvelopeAsync<object>();
        Assert.DoesNotContain(envelope.Errors, error => error.StartsWith($"{field}:", StringComparison.Ordinal));
    }

    private static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.False((await response.ReadEnvelopeAsync<object>()).Success);
    }

    // ============================================================ create

    /// <summary>
    /// The intended happy path: a new email creates the user and reports the
    /// welcome email as not sent, because the test host points the email
    /// service at a closed port.
    /// </summary>
    [Fact(Skip = "spc_Signup_timesheet_user cannot exist in the test container: it inserts dbo.Signup.Pwd and " +
                 "reads dbo.Organization.OrganizationSize, neither of which the EF-built tables have, and it " +
                 "writes dbo.Login, which is not recorded. Unskip once those tables are recorded in docs/database.")]
    public async Task CreatingANewUserAddsThemAndReportsTheWelcomeEmailAsNotSent()
    {
        await SeedProjectsAndRolesAsync();
        await Factory.SeedOrganizationsAsync(OrganizationId);

        var response = await Client().PostAsJsonAsync(CreateUrl, CreateUserRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.ReadDataAsync<TimesheetUserCreateResponse>();

        Assert.True(created.IsNewUser);
        Assert.False(created.WelcomeEmailSent);
        Assert.Equal("NEHA.KAPOOR@ETIMESHEET.TEST", created.Email); // stored upper case
        Assert.Equal("Neha Kapoor", created.Name);
        Assert.Equal(OrganizationId, created.OrganizationId);
        Assert.Equal(1, created.ProjectId);

        var assignment = Assert.Single(await Factory.AssignmentRowsAsync());
        Assert.Equal(created.UserId, assignment.UserId);
        Assert.Equal(1, assignment.ProjectId);
        Assert.Equal(5, assignment.ProjectRoleId);
        Assert.Equal(8, assignment.MaxLogHoursPerDay);
        Assert.Equal(ActingUserId, assignment.CreatedBy);
    }

    /// <summary>Every shape rule is reported in one 400, before the database is asked anything.</summary>
    [Fact]
    public async Task AnInvalidCreatePayloadIs400NamingEveryField()
    {
        var request = new TimesheetUserCreateRequest
        {
            FullName = "   ",
            Email = "not-an-email",
            CountryId = 0,
            OrganizationId = 0,
            ProjectId = 0,
            ProjectRoleId = 0,
            JobType = 0,
            CostRate = null,
            BillRate = -1m,
            WorkLocationRange = -1,
            ProjectLocationLatitude = "91",
            MaxLogHoursPerDay = 25,
            CreatedBy = 0,
        };

        var response = await Client().PostAsJsonAsync(CreateUrl, request);

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetUserCreateRequest.FullName),
            nameof(TimesheetUserCreateRequest.Email),
            nameof(TimesheetUserCreateRequest.CountryId),
            nameof(TimesheetUserCreateRequest.OrganizationId),
            nameof(TimesheetUserCreateRequest.ProjectId),
            nameof(TimesheetUserCreateRequest.ProjectRoleId),
            nameof(TimesheetUserCreateRequest.JobType),
            nameof(TimesheetUserCreateRequest.CostRate),
            nameof(TimesheetUserCreateRequest.BillRate),
            nameof(TimesheetUserCreateRequest.WorkLocationRange),
            nameof(TimesheetUserCreateRequest.ProjectLocationLatitude),
            nameof(TimesheetUserCreateRequest.MaxLogHoursPerDay),
            nameof(TimesheetUserCreateRequest.CreatedBy));
        Assert.Equal(0, await Factory.SignupCountAsync());
    }

    /// <summary>
    /// No foreign key protects these columns, so the service checks each one -
    /// a country that does not exist, a project or role of another
    /// organisation - and reports them together.
    /// </summary>
    [Fact]
    public async Task NamingACountryProjectOrRoleThatIsNotTheOrganizationsIs400()
    {
        await SeedProjectsAndRolesAsync();
        await Factory.SeedOrganizationsAsync(OrganizationId, OtherOrganizationId);

        var response = await Client().PostAsJsonAsync(
            CreateUrl,
            CreateUserRequest(projectId: 3, projectRoleId: 6, countryId: 999));

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetUserCreateRequest.CountryId),
            nameof(TimesheetUserCreateRequest.ProjectId),
            nameof(TimesheetUserCreateRequest.ProjectRoleId));
        await AssertBadRequestNotNamingAsync(response, nameof(TimesheetUserCreateRequest.OrganizationId));
        Assert.Empty(await Factory.AssignmentRowsAsync());
        Assert.Equal(0, await Factory.SignupCountAsync());
    }

    [Fact]
    public async Task AnOrganizationThatDoesNotExistIs400()
    {
        await SeedProjectsAndRolesAsync();

        var response = await Client().PostAsJsonAsync(CreateUrl, CreateUserRequest());

        await AssertBadRequestNamingAsync(response, nameof(TimesheetUserCreateRequest.OrganizationId));
        Assert.Empty(await Factory.AssignmentRowsAsync());
    }

    [Theory]
    [InlineData(7)] // deleted
    [InlineData(99)] // missing
    public async Task ARoleThatIsDeletedOrMissingIs400(int projectRoleId)
    {
        await SeedProjectsAndRolesAsync();
        await Factory.SeedOrganizationsAsync(OrganizationId);

        var response = await Client().PostAsJsonAsync(CreateUrl, CreateUserRequest(projectRoleId: projectRoleId));

        await AssertBadRequestNamingAsync(response, nameof(TimesheetUserCreateRequest.ProjectRoleId));
        Assert.Empty(await Factory.AssignmentRowsAsync());
    }

    /// <summary>
    /// The email is matched as the procedure matches it - ignoring case and
    /// surrounding spaces - and a removed user is never quietly re-assigned.
    /// </summary>
    [Fact]
    public async Task TheEmailOfARemovedUserIs409AndAssignsNothing()
    {
        await SeedProjectsAndRolesAsync();
        await Factory.SeedOrganizationsAsync(OrganizationId);
        await Factory.SeedEmployeesAsync(User(PriyaUserId, "Priya Shah", PriyaEmail, isDeleted: true));

        var response = await Client().PostAsJsonAsync(CreateUrl, CreateUserRequest(email: "  PRIYA.Shah@etimesheet.test "));

        await AssertStatusAsync(response, HttpStatusCode.Conflict);
        Assert.Empty(await Factory.AssignmentRowsAsync());
        Assert.Equal(1, await Factory.SignupCountAsync());
    }

    [Fact]
    public async Task TheEmailOfAnotherOrganizationsUserIs409AndAssignsNothing()
    {
        await SeedProjectsAndRolesAsync();
        await Factory.SeedOrganizationsAsync(OrganizationId);
        await Factory.SeedEmployeesAsync(User(PriyaUserId, "Priya Shah", PriyaEmail, organizationId: OtherOrganizationId));

        var response = await Client().PostAsJsonAsync(CreateUrl, CreateUserRequest(email: PriyaEmail.ToUpperInvariant()));

        await AssertStatusAsync(response, HttpStatusCode.Conflict);
        Assert.Empty(await Factory.AssignmentRowsAsync());
    }

    // ============================================================== edit

    /// <summary>
    /// A changed name and email go to <c>dbo.Signup</c> - the email upper case,
    /// as the signup procedure stores it - and everything else to the
    /// assignment, in one save.
    /// </summary>
    [Fact]
    public async Task EditingSavesTheAssignmentAndTheChangedNameAndEmail()
    {
        await SeedPriyaOnProjectOneAsync();
        var request = UpdateUserRequest(11, "  Priya Rao  ", " priya.rao@etimesheet.test ", projectId: 2);

        var response = await Client().PostAsJsonAsync(EditUrl, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.ReadDataAsync<TimesheetUserResponse>();

        Assert.Equal(11, saved.TimesheetUserProjectSetupId);
        Assert.Equal(PriyaUserId, saved.UserId);
        Assert.Equal("Priya Rao", saved.Name);
        Assert.Equal("PRIYA.RAO@ETIMESHEET.TEST", saved.Email);
        Assert.Equal(OrganizationId, saved.OrganizationId);
        Assert.Equal(2, saved.ProjectId);
        Assert.Equal(5, saved.ProjectRoleId);
        Assert.Equal(2, saved.JobType);
        Assert.Equal(45.50m, saved.CostRate);
        Assert.Equal(90.25m, saved.BillRate);
        Assert.Equal(250, saved.WorkLocationRange);
        Assert.Equal("23.0225", saved.ProjectLocationLatitude);
        Assert.Equal("72.5714", saved.ProjectLocationLongitude);
        Assert.Equal(9, saved.MaxLogHoursPerDay);

        var assignment = Assert.Single(await Factory.AssignmentRowsAsync());
        Assert.Equal(2, assignment.ProjectId);
        Assert.Equal(2, assignment.JobType);
        Assert.Equal(45.50m, assignment.CostRate);
        Assert.Equal(90.25m, assignment.BillRate);
        Assert.Equal(250, assignment.WorkLocationRange);
        Assert.Equal("23.0225", assignment.ProjectLocationLatitude);
        Assert.Equal("72.5714", assignment.ProjectLocationLongitude);
        Assert.Equal(9, assignment.MaxLogHoursPerDay);
        Assert.Equal(0, assignment.IsDelete);

        var user = await Factory.SignupRowAsync(PriyaUserId);
        Assert.NotNull(user);
        Assert.Equal("Priya Rao", user!.Name);
        Assert.Equal("PRIYA.RAO@ETIMESHEET.TEST", user.Email);
        Assert.Equal(new DateTimeOffset(TimeLogTestData.Now), user.UpdatedDate);
        Assert.Equal(ActingUserId, user.UpdateBy);
    }

    /// <summary>
    /// The same name, and the same email in another case, are not a change:
    /// <c>dbo.Signup</c> is not touched, so it carries no update stamp.
    /// </summary>
    [Fact]
    public async Task EditingWithAnUnchangedNameAndEmailLeavesTheUserAlone()
    {
        await SeedPriyaOnProjectOneAsync();

        var response = await Client().PostAsJsonAsync(
            EditUrl,
            UpdateUserRequest(11, "Priya Shah", PriyaEmail.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PriyaEmail, (await response.ReadDataAsync<TimesheetUserResponse>()).Email);

        var user = await Factory.SignupRowAsync(PriyaUserId);
        Assert.Equal(PriyaEmail, user!.Email);
        Assert.Null(user.UpdatedDate);
        Assert.Null(user.UpdateBy);

        Assert.Equal(9, Assert.Single(await Factory.AssignmentRowsAsync()).MaxLogHoursPerDay);
    }

    [Theory]
    [InlineData(99)] // missing
    [InlineData(12)] // deleted
    public async Task EditingAnAssignmentThatIsMissingOrDeletedIs404(int setupId)
    {
        await SeedPriyaOnProjectOneAsync();
        await Factory.SeedAssignmentsAsync(Assignment(12, PriyaUserId, projectId: 2, isDeleted: true));

        var response = await Client().PostAsJsonAsync(EditUrl, UpdateUserRequest(setupId, "Priya Shah", PriyaEmail));

        await AssertStatusAsync(response, HttpStatusCode.NotFound);
    }

    /// <summary>An assignment whose user is removed, or belongs to another organisation, is a 404 - and nothing is written.</summary>
    [Theory]
    [InlineData(13)] // removed user
    [InlineData(14)] // user of another organisation
    public async Task EditingAnAssignmentOfAUserOutsideTheOrganizationIs404(int setupId)
    {
        await SeedProjectsAndRolesAsync();
        await Factory.SeedEmployeesAsync(
            User(102, "Removed User", "removed@etimesheet.test", isDeleted: true),
            User(103, "Other Org User", "other@etimesheet.test", organizationId: OtherOrganizationId));
        await Factory.SeedAssignmentsAsync(
            Assignment(13, 102, projectId: 1),
            Assignment(14, 103, projectId: 1));

        var response = await Client().PostAsJsonAsync(EditUrl, UpdateUserRequest(setupId, "New Name", "new@etimesheet.test", projectId: 2));

        await AssertStatusAsync(response, HttpStatusCode.NotFound);
        Assert.All(await Factory.AssignmentRowsAsync(), row => Assert.Equal(1, row.ProjectId));
    }

    [Fact]
    public async Task EditingOntoAProjectOrRoleOfAnotherOrganizationIs400()
    {
        await SeedPriyaOnProjectOneAsync();

        var response = await Client().PostAsJsonAsync(
            EditUrl,
            UpdateUserRequest(11, "Priya Shah", PriyaEmail, projectId: 3, projectRoleId: 6));

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetUserUpdateRequest.ProjectId),
            nameof(TimesheetUserUpdateRequest.ProjectRoleId));
        Assert.Equal(1, Assert.Single(await Factory.AssignmentRowsAsync()).ProjectId);
    }

    [Fact]
    public async Task EditingOntoAProjectTheUserIsAlreadyOnIs409()
    {
        await SeedPriyaOnProjectOneAsync();
        await Factory.SeedAssignmentsAsync(Assignment(15, PriyaUserId, projectId: 2));

        var response = await Client().PostAsJsonAsync(EditUrl, UpdateUserRequest(11, "Priya Shah", PriyaEmail, projectId: 2));

        await AssertStatusAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(new int?[] { 1, 2 }, (await Factory.AssignmentRowsAsync()).Select(row => row.ProjectId));
    }

    /// <summary>A deleted assignment to the target project does not count as being on it.</summary>
    [Fact]
    public async Task ADeletedAssignmentToTheTargetProjectIsNoConflict()
    {
        await SeedPriyaOnProjectOneAsync();
        await Factory.SeedAssignmentsAsync(Assignment(15, PriyaUserId, projectId: 2, isDeleted: true));

        var response = await Client().PostAsJsonAsync(EditUrl, UpdateUserRequest(11, "Priya Shah", PriyaEmail, projectId: 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await Factory.AssignmentRowsAsync()).Single(row => row.TimesheetUserProjectSetupId == 11).ProjectId);
    }

    /// <summary>Two users may never share an email - a removed user's included.</summary>
    [Fact]
    public async Task EditingToAnotherUsersEmailIs409AndChangesNothing()
    {
        await SeedPriyaOnProjectOneAsync();
        await Factory.SeedEmployeesAsync(User(104, "Taken", "taken@etimesheet.test", isDeleted: true));

        var response = await Client().PostAsJsonAsync(EditUrl, UpdateUserRequest(11, "Priya Shah", "TAKEN@etimesheet.test", projectId: 2));

        await AssertStatusAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(PriyaEmail, (await Factory.SignupRowAsync(PriyaUserId))!.Email);
        Assert.Equal(1, Assert.Single(await Factory.AssignmentRowsAsync()).ProjectId);
    }

    [Fact]
    public async Task AnInvalidEditPayloadIs400NamingEveryField()
    {
        var request = new TimesheetUserUpdateRequest
        {
            TimesheetUserProjectSetupId = 0,
            OrganizationId = 0,
            FullName = null,
            Email = "Priya <priya@etimesheet.test>",
            ProjectId = 0,
            ProjectRoleId = 0,
            JobType = 0,
            CostRate = -0.01m,
            BillRate = null,
            WorkLocationRange = -5,
            ProjectLocationLatitude = "-90.5",
            ProjectLocationLongitude = "east",
            MaxLogHoursPerDay = -1,
            UpdatedBy = 0,
        };

        var response = await Client().PostAsJsonAsync(EditUrl, request);

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetUserUpdateRequest.TimesheetUserProjectSetupId),
            nameof(TimesheetUserUpdateRequest.OrganizationId),
            nameof(TimesheetUserUpdateRequest.FullName),
            nameof(TimesheetUserUpdateRequest.Email),
            nameof(TimesheetUserUpdateRequest.ProjectId),
            nameof(TimesheetUserUpdateRequest.ProjectRoleId),
            nameof(TimesheetUserUpdateRequest.JobType),
            nameof(TimesheetUserUpdateRequest.CostRate),
            nameof(TimesheetUserUpdateRequest.BillRate),
            nameof(TimesheetUserUpdateRequest.WorkLocationRange),
            nameof(TimesheetUserUpdateRequest.ProjectLocationLatitude),
            nameof(TimesheetUserUpdateRequest.ProjectLocationLongitude),
            nameof(TimesheetUserUpdateRequest.MaxLogHoursPerDay),
            nameof(TimesheetUserUpdateRequest.UpdatedBy));
    }

    // ============================================================ remove

    /// <summary>
    /// A soft delete of every live assignment of the user to the project,
    /// duplicates included. The user's other projects, other users, and
    /// <c>dbo.Signup</c> are untouched.
    /// </summary>
    [Fact]
    public async Task RemovingSoftDeletesEveryAssignmentOfTheUserToTheProject()
    {
        await SeedPriyaOnProjectOneAsync();
        await Factory.SeedEmployeesAsync(User(105, "Aarav Mehta", "aarav@etimesheet.test"));
        await Factory.SeedAssignmentsAsync(
            Assignment(16, PriyaUserId, projectId: 1),
            Assignment(17, PriyaUserId, projectId: 2),
            Assignment(18, 105, projectId: 1));

        var response = await Client().PostAsJsonAsync(
            RemoveUrl,
            new TimesheetUserDeleteRequest { UserId = PriyaUserId, ProjectId = 1, DeletedBy = ActingUserId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rows = (await Factory.AssignmentRowsAsync()).ToDictionary(row => row.TimesheetUserProjectSetupId);

        foreach (var removed in new[] { 11, 16 })
        {
            Assert.Equal(1, rows[removed].IsDelete);
            Assert.Equal(TimeLogTestData.Now, rows[removed].DeletedDate);
            Assert.Equal(ActingUserId, rows[removed].DeletedBy);
        }

        foreach (var kept in new[] { 17, 18 })
        {
            Assert.Equal(0, rows[kept].IsDelete);
            Assert.Null(rows[kept].DeletedDate);
        }

        Assert.Equal(0, (await Factory.SignupRowAsync(PriyaUserId))!.IsDelete);
    }

    /// <summary>The second remove is a 404 and does not re-stamp the first.</summary>
    [Fact]
    public async Task RemovingTwiceIs404AndKeepsTheFirstStamp()
    {
        await SeedPriyaOnProjectOneAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            (await Client().PostAsJsonAsync(RemoveUrl, new TimesheetUserDeleteRequest { UserId = PriyaUserId, ProjectId = 1, DeletedBy = ActingUserId })).StatusCode);

        var second = await Client().PostAsJsonAsync(
            RemoveUrl,
            new TimesheetUserDeleteRequest { UserId = PriyaUserId, ProjectId = 1, DeletedBy = ActingUserId + 1 });

        await AssertStatusAsync(second, HttpStatusCode.NotFound);
        Assert.Equal(ActingUserId, Assert.Single(await Factory.AssignmentRowsAsync()).DeletedBy);
    }

    [Fact]
    public async Task RemovingAUserFromAProjectTheyAreNotOnIs404()
    {
        await SeedPriyaOnProjectOneAsync();

        var response = await Client().PostAsJsonAsync(
            RemoveUrl,
            new TimesheetUserDeleteRequest { UserId = PriyaUserId, ProjectId = 2, DeletedBy = ActingUserId });

        await AssertStatusAsync(response, HttpStatusCode.NotFound);
        Assert.Equal(0, Assert.Single(await Factory.AssignmentRowsAsync()).IsDelete);
    }

    [Fact]
    public async Task RemovingRejectsNonPositiveIds()
    {
        var response = await Client().PostAsJsonAsync(
            RemoveUrl,
            new TimesheetUserDeleteRequest { UserId = 0, ProjectId = -1, DeletedBy = 0 });

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetUserDeleteRequest.UserId),
            nameof(TimesheetUserDeleteRequest.ProjectId),
            nameof(TimesheetUserDeleteRequest.DeletedBy));
    }

    // ============================================================== list

    [Fact]
    public async Task TheUserListIsEmptyForAnOrganizationWithNoUsers()
    {
        var response = await Client().GetAsync(ListUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.ReadDataAsync<List<TimesheetUserListResponse>>());
    }

    /// <summary>
    /// One row per live assignment of a live user of the organisation, ordered
    /// by user name, then project name. Removed users, deleted assignments and
    /// other organisations' users are left out.
    /// </summary>
    [Fact]
    public async Task TheUserListHoldsLiveAssignmentsOfLiveUsersOrderedByNameThenProject()
    {
        await SeedProjectsAndRolesAsync();
        await Factory.SeedEmployeesAsync(
            User(PriyaUserId, "Priya Shah", PriyaEmail),
            User(105, "Aarav Mehta", "aarav@etimesheet.test"),
            User(102, "Removed User", "removed@etimesheet.test", isDeleted: true),
            User(103, "Other Org User", "other@etimesheet.test", organizationId: OtherOrganizationId));
        await Factory.SeedAssignmentsAsync(
            Assignment(21, PriyaUserId, projectId: 2),
            Assignment(22, PriyaUserId, projectId: 1),
            Assignment(23, 105, projectId: 1),
            Assignment(24, PriyaUserId, projectId: 1, isDeleted: true),
            Assignment(25, 102, projectId: 1),
            Assignment(26, 103, projectId: 3));

        var response = await Client().GetAsync(ListUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.ReadDataAsync<List<TimesheetUserListResponse>>();

        Assert.Equal(new[] { 23, 22, 21 }, users.Select(user => user.TimesheetUserProjectSetupId));
        Assert.Equal(new[] { "Aarav Mehta", "Priya Shah", "Priya Shah" }, users.Select(user => user.Name));
        Assert.Equal(new[] { "Alpha", "Alpha", "Beta" }, users.Select(user => user.ProjectName));
    }

    [Fact]
    public async Task EachUserRowCarriesTheUserProjectAndRole()
    {
        await SeedPriyaOnProjectOneAsync();

        var response = await Client().GetAsync(ListUrl);
        var user = Assert.Single(await response.ReadDataAsync<List<TimesheetUserListResponse>>());

        Assert.Equal(11, user.TimesheetUserProjectSetupId);
        Assert.Equal(PriyaUserId, user.UserId);
        Assert.Equal("Priya Shah", user.Name);
        Assert.Equal(PriyaEmail, user.Email);
        Assert.Equal(OrganizationId, user.OrganizationId);
        Assert.Equal(1, user.ProjectId);
        Assert.Equal("PR0001", user.ProjectCode);
        Assert.Equal("Alpha", user.ProjectName);
        Assert.Equal(5, user.ProjectRoleId);
        Assert.Equal("Developer", user.ProjectRoleName);
        Assert.Equal(1, user.JobType);
        Assert.Equal(30m, user.CostRate);
        Assert.Equal(60m, user.BillRate);
        Assert.Equal(50, user.WorkLocationRange);
        Assert.Equal("19.0760", user.ProjectLocationLatitude);
        Assert.Equal("72.8777", user.ProjectLocationLongitude);
        Assert.Equal(8, user.MaxLogHoursPerDay);
    }

    /// <summary>A deleted role's name is not shown; the assignment keeps its role id, and stays listed.</summary>
    [Fact]
    public async Task AUserRowWithADeletedRoleHasNoRoleName()
    {
        await SeedProjectsAndRolesAsync();
        await Factory.SeedEmployeesAsync(User(PriyaUserId, "Priya Shah", PriyaEmail));
        await Factory.SeedAssignmentsAsync(Assignment(11, PriyaUserId, projectId: 1, projectRoleId: 7));

        var json = await (await Client().GetAsync(ListUrl)).Content.ReadAsStringAsync();
        var user = Assert.Single(await (await Client().GetAsync(ListUrl)).ReadDataAsync<List<TimesheetUserListResponse>>());

        Assert.Equal(7, user.ProjectRoleId);
        Assert.Null(user.ProjectRoleName);
        Assert.Contains("\"projectRoleName\":null", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TheUserListRejectsANonPositiveOrganizationId(int organizationId)
    {
        var response = await Client().GetAsync($"{BaseUrl}/get-timesheet-user-by-orgID/{organizationId}");

        await AssertBadRequestNamingAsync(response, "orgID");
    }
}
