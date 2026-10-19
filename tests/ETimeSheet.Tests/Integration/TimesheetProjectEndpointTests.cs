using System.Net;
using System.Net.Http.Json;
using ETimeSheet.Application.Models;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;
using static ETimeSheet.Tests.TestData.TimesheetProjectTestData;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// The project, project budget and project status endpoints of
/// <c>TimesheetProjectController</c>, end to end against a real SQL Server:
/// the recorded <c>spc_GetTimesheetProject</c>, <c>spc_AddEditTimesheetProject</c>,
/// <c>spc_GetTimesheetProjectBudget</c>, <c>spc_GetTimesheetProjectBudgetByOrgID</c>,
/// <c>spc_AddEditTimesheetProjectBudget</c>, <c>spc_GetTimesheetProjectStatus</c>
/// and <c>spc_AddEditTimesheetProjectStatus</c>, and the real middleware.
/// <para>
/// Ids the database generates are never asserted as numbers: the first row a
/// never-used table receives after the reset's reseed is id 0, so a generated
/// id depends on test order. Tests compare the response with the row instead.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class TimesheetProjectEndpointTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/v1/TimesheetProject";

    private static readonly string ProjectListUrl = $"{BaseUrl}/get-timesheet-project-by-orgID/{OrganizationId}";
    private const string ProjectSaveUrl = BaseUrl + "/add-edit-timesheet-project";
    private const string ProjectRemoveUrl = BaseUrl + "/remove-timesheet-project";

    private const string BudgetSaveUrl = BaseUrl + "/add-edit-timesheet-project-budget";
    private const string BudgetRemoveUrl = BaseUrl + "/remove-timesheet-project-budget";

    private static readonly string StatusListUrl = $"{BaseUrl}/get-timesheet-project-status-by-orgID/{OrganizationId}";
    private const string StatusSaveUrl = BaseUrl + "/add-edit-timesheet-project-status";
    private const string StatusRemoveUrl = BaseUrl + "/remove-timesheet-project-status";

    public TimesheetProjectEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    // The controller is [AllowAnonymous] while authentication is off, so an
    // unauthenticated client is the honest way to call it.
    private HttpClient Client() => Factory.CreateClient();

    private static string BudgetByProjectUrl(int projectId) =>
        $"{BaseUrl}/get-timesheet-project-budget-by-projectID/{projectId}";

    private static string BudgetByOrgUrl(int organizationId) =>
        $"{BaseUrl}/get-timesheet-project-budget-by-orgID/{organizationId}";

    private async Task<T> GetOkAsync<T>(string url)
    {
        var response = await Client().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<T>();
    }

    private async Task<T> PostOkAsync<T>(string url, object payload)
    {
        var response = await Client().PostAsJsonAsync(url, payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadDataAsync<T>();
    }

    /// <summary>A 400 in the envelope, naming every one of <paramref name="fields"/> as "Field: message".</summary>
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

    private static async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False((await response.ReadEnvelopeAsync<object>()).Success);
    }

    /// <summary>The clients a project can name: two live ones of different currencies, one removed.</summary>
    private Task SeedClientsAsync() => Factory.SeedProjectClientsAsync(
        TimesheetProjectTestData.Client(ClientId, "Acme Consulting"),
        TimesheetProjectTestData.Client(RemovedClientId, "Gone Ltd", isDeleted: true),
        TimesheetProjectTestData.Client(SecondClientId, "Globex", currencyId: MasterAdminTestData.UnitedStatesCurrencyId));

    // ====================================================== project: list

    [Fact]
    public async Task ProjectListIsEmptyForAnOrganizationWithNoProjects()
    {
        Assert.Empty(await GetOkAsync<List<TimesheetProjectResponse>>(ProjectListUrl));
    }

    /// <summary>Ordered by name, then id, so two projects with one name always come back the same way.</summary>
    [Fact]
    public async Task ProjectListHoldsOnlyTheOrganizationsProjectsOrderedByNameThenId()
    {
        await Factory.SeedProjectsAsync(
            Project(1, "PR0001", "Zeta"),
            Project(4, "PR0004", "Alpha"),
            Project(2, "PR0002", "Alpha"),
            Project(3, "PR0003", "Aardvark", organizationId: OtherOrganizationId));

        var projects = await GetOkAsync<List<TimesheetProjectResponse>>(ProjectListUrl);

        Assert.Equal(new[] { 2, 4, 1 }, projects.Select(project => project.ProjectId));
        Assert.Equal(new[] { "PR0002", "PR0004", "PR0001" }, projects.Select(project => project.Code));
    }

    [Fact]
    public async Task ProjectListJoinsTheStatusClientCurrencyAndFrequencyNames()
    {
        await SeedClientsAsync();
        await Factory.SeedStatusesAsync(Status(7, "In Progress"));
        await Factory.SeedProjectsAsync(Project(1, "PR0001", "Apollo", status: 7, submissionFrequency: 2));

        var project = Assert.Single(await GetOkAsync<List<TimesheetProjectResponse>>(ProjectListUrl));

        Assert.Equal("Apollo", project.ProjectName);
        Assert.Equal(1000m, project.TotalBudget);
        Assert.Equal(100m, project.TotalHours);
        Assert.Equal(new DateTime(2026, 1, 1), project.StartDate);
        Assert.Equal(new DateTime(2026, 12, 31), project.EndDate);
        Assert.Equal(7, project.Status);
        Assert.Equal("In Progress", project.StatusName);
        Assert.Equal(ClientId, project.ClientId);
        Assert.Equal("Acme Consulting", project.ClientName);
        Assert.Equal(MasterAdminTestData.IndiaCurrencyId, project.CurrencyCode);
        Assert.Equal("₹", project.CurrencySymbol);
        Assert.Equal(2, project.SubmissionFrequency);
        Assert.Equal("Every 15 Days", project.SubmissionFrequencyName);
    }

    /// <summary>
    /// Every join is a LEFT JOIN, so a project naming no status, client or
    /// currency row is still listed - and every key is still written, as null
    /// (CLAUDE.md §5).
    /// </summary>
    [Fact]
    public async Task ProjectListKeepsAProjectWhoseLookupsNameNoRow()
    {
        await Factory.SeedProjectsAsync(
            Project(1, "PR0001", clientId: 77, status: 999, currencyCode: null, submissionFrequency: null));

        var response = await Client().GetAsync(ProjectListUrl);
        var json = await response.Content.ReadAsStringAsync();
        var project = Assert.Single(await response.ReadDataAsync<List<TimesheetProjectResponse>>());

        Assert.Equal(999, project.Status);
        Assert.Null(project.StatusName);
        Assert.Equal(77, project.ClientId);
        Assert.Null(project.ClientName);
        Assert.Null(project.CurrencySymbol);
        Assert.Null(project.SubmissionFrequencyName);

        Assert.Contains("\"statusName\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"clientName\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"currencySymbol\":null", json, StringComparison.Ordinal);
    }

    /// <summary>A removed client's name still shows: the client join is not filtered on IsDeleted.</summary>
    [Fact]
    public async Task ProjectListStillNamesARemovedClient()
    {
        await SeedClientsAsync();
        await Factory.SeedProjectsAsync(Project(1, "PR0001", clientId: RemovedClientId));

        var project = Assert.Single(await GetOkAsync<List<TimesheetProjectResponse>>(ProjectListUrl));

        Assert.Equal("Gone Ltd", project.ClientName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ProjectListRejectsANonPositiveOrganizationId(int organizationId)
    {
        var response = await Client().GetAsync($"{BaseUrl}/get-timesheet-project-by-orgID/{organizationId}");

        await AssertBadRequestNamingAsync(response, "orgID");
    }

    // ================================================ project: add or edit

    [Fact]
    public async Task TheFirstProjectIsPR0001AndIsWrittenAsSent()
    {
        await SeedClientsAsync();
        var request = ProjectRequest();

        var saved = await PostOkAsync<TimesheetProjectResponse>(ProjectSaveUrl, request);

        Assert.Equal("PR0001", saved.Code);
        Assert.Equal("Apollo", saved.ProjectName);
        Assert.Equal("Acme Consulting", saved.ClientName);
        Assert.Equal("Weekly", saved.SubmissionFrequencyName);

        var row = Assert.Single(await Factory.ProjectRowsAsync());
        Assert.Equal(saved.ProjectId, row.ProjectId);
        Assert.Equal("PR0001", row.Code);
        Assert.Equal(OrganizationId, row.OrganizationId);
        Assert.Equal(request.ProjectName, row.ProjectName);
        Assert.Equal(request.TotalBudget, row.TotalBudget);
        Assert.Equal(request.TotalHours, row.TotalHours);
        Assert.Equal(request.StartDate, row.StartDate);
        Assert.Equal(request.EndDate, row.EndDate);
        Assert.Equal(ClientId, row.ClientId);
        Assert.Equal(1, row.SubmissionFrequency);
        Assert.Null(row.Status);
    }

    /// <summary>No currency is sent: the project takes its client's.</summary>
    [Fact]
    public async Task AProjectTakesItsClientsCurrency()
    {
        await SeedClientsAsync();

        var saved = await PostOkAsync<TimesheetProjectResponse>(ProjectSaveUrl, ProjectRequest(clientId: SecondClientId));

        Assert.Equal(MasterAdminTestData.UnitedStatesCurrencyId, saved.CurrencyCode);
        Assert.Equal("$", saved.CurrencySymbol);
        Assert.Equal(MasterAdminTestData.UnitedStatesCurrencyId, Assert.Single(await Factory.ProjectRowsAsync()).CurrencyCode);
    }

    /// <summary>"Next" follows the highest code of every organisation, so a code is unique across the table.</summary>
    [Fact]
    public async Task TheCodeSequenceSpansEveryOrganization()
    {
        await SeedClientsAsync();
        await Factory.SeedProjectsAsync(
            Project(1, "PR0002"),
            Project(2, "PR0007", organizationId: OtherOrganizationId));

        var saved = await PostOkAsync<TimesheetProjectResponse>(ProjectSaveUrl, ProjectRequest(projectId: 0));

        Assert.Equal("PR0008", saved.Code);
        Assert.Equal(3, (await Factory.ProjectRowsAsync()).Count);
    }

    /// <summary>
    /// Longest first, then greatest: PR10000 follows PR9999. A code that is not
    /// PR + digits does not count.
    /// </summary>
    [Fact]
    public async Task TheCodeSequenceGrowsPastFourDigitsAndIgnoresOtherCodes()
    {
        await SeedClientsAsync();
        await Factory.SeedProjectsAsync(
            Project(1, "PR9999"),
            Project(2, "PRX99999"),
            Project(3, "XY99999"));

        var first = await PostOkAsync<TimesheetProjectResponse>(ProjectSaveUrl, ProjectRequest(projectName: "First"));
        var second = await PostOkAsync<TimesheetProjectResponse>(ProjectSaveUrl, ProjectRequest(projectName: "Second"));

        Assert.Equal("PR10000", first.Code);
        Assert.Equal("PR10001", second.Code);
    }

    [Fact]
    public async Task AnEditOverwritesEveryFieldButNeverTheCode()
    {
        await SeedClientsAsync();
        await Factory.SeedStatusesAsync(Status(4, "On Hold"));
        await Factory.SeedProjectsAsync(Project(5, "PR0005", "Old Name", submissionFrequency: 3));

        var request = new TimesheetProjectSaveRequest
        {
            ProjectId = 5,
            OrganizationId = OrganizationId,
            ProjectName = "  New Name  ",
            TotalBudget = null,
            TotalHours = 42.75m,
            StartDate = null,
            EndDate = null,
            Status = 4,
            ClientId = SecondClientId,
            SubmissionFrequency = null,
        };

        var saved = await PostOkAsync<TimesheetProjectResponse>(ProjectSaveUrl, request);

        Assert.Equal(5, saved.ProjectId);
        Assert.Equal("PR0005", saved.Code);
        Assert.Equal("New Name", saved.ProjectName); // trimmed
        Assert.Equal("On Hold", saved.StatusName);
        Assert.Equal("Globex", saved.ClientName);
        Assert.Null(saved.SubmissionFrequencyName);

        var row = Assert.Single(await Factory.ProjectRowsAsync());
        Assert.Equal("PR0005", row.Code);
        Assert.Equal("New Name", row.ProjectName);
        Assert.Null(row.TotalBudget);
        Assert.Equal(42.75m, row.TotalHours);
        Assert.Null(row.StartDate);
        Assert.Null(row.EndDate);
        Assert.Equal(4, row.Status);
        Assert.Equal(SecondClientId, row.ClientId);
        Assert.Equal(MasterAdminTestData.UnitedStatesCurrencyId, row.CurrencyCode);
        Assert.Null(row.SubmissionFrequency);
        Assert.Equal(OrganizationId, row.OrganizationId);
    }

    [Fact]
    public async Task EditingAnotherOrganizationsProjectIs404AndChangesNothing()
    {
        await SeedClientsAsync();
        await Factory.SeedProjectsAsync(Project(5, "PR0005", "Theirs", organizationId: OtherOrganizationId));

        var response = await Client().PostAsJsonAsync(ProjectSaveUrl, ProjectRequest(projectId: 5, projectName: "Mine"));

        await AssertNotFoundAsync(response);

        var row = Assert.Single(await Factory.ProjectRowsAsync());
        Assert.Equal("Theirs", row.ProjectName);
        Assert.Equal(OtherOrganizationId, row.OrganizationId);
    }

    [Fact]
    public async Task EditingAMissingProjectIs404AndAddsNothing()
    {
        await SeedClientsAsync();

        var response = await Client().PostAsJsonAsync(ProjectSaveUrl, ProjectRequest(projectId: 99));

        await AssertNotFoundAsync(response);
        Assert.Empty(await Factory.ProjectRowsAsync());
    }

    [Theory]
    [InlineData(RemovedClientId)]
    [InlineData(99)]
    public async Task AClientThatIsRemovedOrMissingIs400(int clientId)
    {
        await SeedClientsAsync();

        var response = await Client().PostAsJsonAsync(ProjectSaveUrl, ProjectRequest(clientId: clientId));

        await AssertBadRequestNamingAsync(response, nameof(TimesheetProjectSaveRequest.ClientId));
        Assert.Empty(await Factory.ProjectRowsAsync());
    }

    /// <summary>Every failing field is reported in one 400, before anything is read or written.</summary>
    [Fact]
    public async Task AnInvalidProjectPayloadIs400NamingEveryField()
    {
        var request = new TimesheetProjectSaveRequest
        {
            ProjectId = -1,
            OrganizationId = 0,
            ProjectName = new string('x', 101),
            TotalBudget = -1m,
            TotalHours = -1m,
            StartDate = new DateTime(2026, 5, 1),
            EndDate = new DateTime(2026, 4, 30),
            Status = 0,
            SubmissionFrequency = 4,
            ClientId = 0,
        };

        var response = await Client().PostAsJsonAsync(ProjectSaveUrl, request);

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetProjectSaveRequest.ProjectId),
            nameof(TimesheetProjectSaveRequest.OrganizationId),
            nameof(TimesheetProjectSaveRequest.ProjectName),
            nameof(TimesheetProjectSaveRequest.TotalBudget),
            nameof(TimesheetProjectSaveRequest.TotalHours),
            nameof(TimesheetProjectSaveRequest.EndDate),
            nameof(TimesheetProjectSaveRequest.Status),
            nameof(TimesheetProjectSaveRequest.SubmissionFrequency),
            nameof(TimesheetProjectSaveRequest.ClientId));
        Assert.Empty(await Factory.ProjectRowsAsync());
    }

    // ====================================================== project: remove

    /// <summary>A hard delete of the project and every budget row it has - and nobody else's.</summary>
    [Fact]
    public async Task RemoveDeletesTheProjectAndOnlyItsBudgets()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"), Project(2, "PR0002"));
        await Factory.SeedBudgetsAsync(Budget(10, 1), Budget(11, 1), Budget(12, 2));

        var response = await Client().PostAsJsonAsync(
            ProjectRemoveUrl,
            new TimesheetProjectDeleteRequest { ProjectId = 1, OrganizationId = OrganizationId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadEnvelopeAsync<object>()).Success);

        Assert.Equal(new[] { 2 }, (await Factory.ProjectRowsAsync()).Select(project => project.ProjectId));
        Assert.Equal(new[] { 12 }, (await Factory.BudgetRowsAsync()).Select(budget => budget.BudgetId));
    }

    [Fact]
    public async Task RemovingAnotherOrganizationsProjectIs404AndKeepsIt()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001", organizationId: OtherOrganizationId));
        await Factory.SeedBudgetsAsync(Budget(10, 1));

        var response = await Client().PostAsJsonAsync(
            ProjectRemoveUrl,
            new TimesheetProjectDeleteRequest { ProjectId = 1, OrganizationId = OrganizationId });

        await AssertNotFoundAsync(response);
        Assert.Single(await Factory.ProjectRowsAsync());
        Assert.Single(await Factory.BudgetRowsAsync());
    }

    [Fact]
    public async Task RemovingAProjectTwiceIs404TheSecondTime()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"));
        var request = new TimesheetProjectDeleteRequest { ProjectId = 1, OrganizationId = OrganizationId };

        Assert.Equal(HttpStatusCode.OK, (await Client().PostAsJsonAsync(ProjectRemoveUrl, request)).StatusCode);

        await AssertNotFoundAsync(await Client().PostAsJsonAsync(ProjectRemoveUrl, request));
    }

    [Fact]
    public async Task RemoveRejectsNonPositiveIds()
    {
        var response = await Client().PostAsJsonAsync(
            ProjectRemoveUrl,
            new TimesheetProjectDeleteRequest { ProjectId = 0, OrganizationId = -1 });

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetProjectDeleteRequest.ProjectId),
            nameof(TimesheetProjectDeleteRequest.OrganizationId));
    }

    // ============================================= budget: list by project

    [Fact]
    public async Task BudgetListIsEmptyForAProjectWithNoBudget()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"));

        Assert.Empty(await GetOkAsync<List<TimesheetProjectBudgetResponse>>(BudgetByProjectUrl(1)));
        Assert.Empty(await GetOkAsync<List<TimesheetProjectBudgetResponse>>(BudgetByProjectUrl(99)));
    }

    [Fact]
    public async Task BudgetListHoldsOnlyThatProjectsLinesOrderedByPeriodStartThenId()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"), Project(2, "PR0002"));
        await Factory.SeedBudgetsAsync(
            Budget(3, 1, new DateTime(2026, 3, 1)),
            Budget(1, 1, new DateTime(2026, 5, 1)),
            Budget(2, 1, new DateTime(2026, 3, 1)),
            Budget(4, 2, new DateTime(2026, 1, 1)));

        var budgets = await GetOkAsync<List<TimesheetProjectBudgetResponse>>(BudgetByProjectUrl(1));

        Assert.Equal(new[] { 2, 3, 1 }, budgets.Select(budget => budget.BudgetId));
    }

    /// <summary>A deleted category's name is not shown, but the line keeps its category id.</summary>
    [Fact]
    public async Task BudgetListNamesOnlyLiveCategories()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"));
        await Factory.SeedCategoriesAsync(Category(1, "Design"), Category(2, "Retired", isDeleted: true));
        await Factory.SeedBudgetsAsync(
            Budget(1, 1, new DateTime(2026, 1, 1), projectCategoryId: 1, projectCode: "PR0001"),
            Budget(2, 1, new DateTime(2026, 2, 1), projectCategoryId: 2));

        var budgets = await GetOkAsync<List<TimesheetProjectBudgetResponse>>(BudgetByProjectUrl(1));

        Assert.Equal(2, budgets.Count);
        Assert.Equal("Design", budgets[0].CategoryName);
        Assert.Equal("PR0001", budgets[0].ProjectCode);
        Assert.Equal(10m, budgets[0].AllocatedHours);
        Assert.Equal(20m, budgets[0].HourlyRate);
        Assert.Equal(200m, budgets[0].AllocatedAmount);
        Assert.Equal(2, budgets[1].ProjectCategoryId);
        Assert.Null(budgets[1].CategoryName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task BudgetListRejectsANonPositiveProjectId(int projectId)
    {
        await AssertBadRequestNamingAsync(await Client().GetAsync(BudgetByProjectUrl(projectId)), "projectID");
    }

    // ================================================ budget: list by org

    /// <summary>
    /// Ordered by project name, then period start; each line carries its
    /// project. A project with no budget is not listed, nor is another
    /// organisation's.
    /// </summary>
    [Fact]
    public async Task BudgetListByOrganizationHoldsItsProjectsLinesOrderedByProjectNameThenPeriod()
    {
        await Factory.SeedProjectsAsync(
            Project(1, "PR0001", "Zeta"),
            Project(2, "PR0002", "Alpha"),
            Project(3, "PR0003", "No Budget"),
            Project(4, "PR0004", "Aardvark", organizationId: OtherOrganizationId));
        await Factory.SeedCategoriesAsync(Category(1, "Build"));
        await Factory.SeedBudgetsAsync(
            Budget(1, 1, new DateTime(2026, 1, 1), projectCategoryId: 1),
            Budget(2, 2, new DateTime(2026, 6, 1)),
            Budget(3, 2, new DateTime(2026, 2, 1)),
            Budget(4, 4, new DateTime(2026, 1, 1)));

        var budgets = await GetOkAsync<List<TimesheetProjectBudgetByOrgResponse>>(BudgetByOrgUrl(OrganizationId));

        Assert.Equal(new[] { 3, 2, 1 }, budgets.Select(budget => budget.BudgetId));
        Assert.Equal(new[] { 2, 2, 1 }, budgets.Select(budget => budget.ProjectId));
        Assert.Equal(new[] { "PR0002", "PR0002", "PR0001" }, budgets.Select(budget => budget.ProjectCode));
        Assert.Equal(new[] { "Alpha", "Alpha", "Zeta" }, budgets.Select(budget => budget.ProjectName));
        Assert.Equal("Build", budgets[2].CategoryName);
        Assert.Null(budgets[0].CategoryName);
    }

    [Fact]
    public async Task BudgetListByOrganizationIsEmptyWhenItHasNoBudgets()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"));

        Assert.Empty(await GetOkAsync<List<TimesheetProjectBudgetByOrgResponse>>(BudgetByOrgUrl(OrganizationId)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task BudgetListByOrganizationRejectsANonPositiveOrganizationId(int organizationId)
    {
        await AssertBadRequestNamingAsync(await Client().GetAsync(BudgetByOrgUrl(organizationId)), "orgID");
    }

    // ================================================= budget: add or edit

    /// <summary>
    /// The amount is worked out as hours x rate, rounded half away from zero:
    /// 12.5 x 80.25 = 1003.125, saved as 1003.13. The periods keep their date only.
    /// </summary>
    [Fact]
    public async Task AddingABudgetWorksOutTheAmountAndCopiesTheProjectCode()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"));
        await Factory.SeedCategoriesAsync(Category(3, "Testing"));

        var request = BudgetRequest(1, projectCategoryId: 3, allocatedHours: 12.5m, hourlyRate: 80.25m);
        request.PeriodStart = new DateTime(2026, 4, 1, 15, 30, 0);

        var saved = await PostOkAsync<TimesheetProjectBudgetResponse>(BudgetSaveUrl, request);

        Assert.Equal("PR0001", saved.ProjectCode);
        Assert.Equal(3, saved.ProjectCategoryId);
        Assert.Equal("Testing", saved.CategoryName);
        Assert.Equal(1003.13m, saved.AllocatedAmount);
        Assert.Equal(new DateTime(2026, 4, 1), saved.PeriodStart);

        var row = Assert.Single(await Factory.BudgetRowsAsync());
        Assert.Equal(saved.BudgetId, row.BudgetId);
        Assert.Equal(1, row.ProjectId);
        Assert.Equal("PR0001", row.ProjectCode);
        Assert.Equal(12.5m, row.AllocatedHours);
        Assert.Equal(80.25m, row.HourlyRate);
        Assert.Equal(1003.13m, row.AllocatedAmount);
        Assert.Equal(new DateTime(2026, 4, 1), row.PeriodStart);
        Assert.Equal(new DateTime(2026, 6, 30), row.PeriodEnd);
    }

    /// <summary>A project has one budget: saving again updates it rather than adding a second.</summary>
    [Fact]
    public async Task SavingABudgetForAProjectThatHasOneUpdatesIt()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"));
        await Factory.SeedBudgetsAsync(Budget(7, 1, projectCategoryId: null));

        var saved = await PostOkAsync<TimesheetProjectBudgetResponse>(
            BudgetSaveUrl,
            BudgetRequest(1, allocatedHours: 40m, hourlyRate: 25m));

        Assert.Equal(7, saved.BudgetId);

        var row = Assert.Single(await Factory.BudgetRowsAsync());
        Assert.Equal(7, row.BudgetId);
        Assert.Equal(40m, row.AllocatedHours);
        Assert.Equal(25m, row.HourlyRate);
        Assert.Equal(1000m, row.AllocatedAmount);
        Assert.Equal("PR0001", row.ProjectCode);
        Assert.Equal(new DateTime(2026, 4, 1), row.PeriodStart);
    }

    [Fact]
    public async Task TheAmountIsNullWhenTheRateIsLeftOut()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"));

        var saved = await PostOkAsync<TimesheetProjectBudgetResponse>(BudgetSaveUrl, BudgetRequest(1, hourlyRate: null));

        Assert.Null(saved.AllocatedAmount);
        Assert.Null(Assert.Single(await Factory.BudgetRowsAsync()).AllocatedAmount);
    }

    [Fact]
    public async Task ABudgetForAMissingProjectIs404AndAddsNothing()
    {
        var response = await Client().PostAsJsonAsync(BudgetSaveUrl, BudgetRequest(99));

        await AssertNotFoundAsync(response);
        Assert.Empty(await Factory.BudgetRowsAsync());
    }

    /// <summary>The category must be a live one of the project's own organisation.</summary>
    [Theory]
    [InlineData(2)] // another organisation's
    [InlineData(3)] // deleted
    [InlineData(99)] // missing
    public async Task ACategoryOutsideTheProjectsOrganizationIs400(int projectCategoryId)
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"));
        await Factory.SeedCategoriesAsync(
            Category(2, "Theirs", organizationId: OtherOrganizationId),
            Category(3, "Retired", isDeleted: true));

        var response = await Client().PostAsJsonAsync(BudgetSaveUrl, BudgetRequest(1, projectCategoryId));

        await AssertBadRequestNamingAsync(response, nameof(TimesheetProjectBudgetSaveRequest.ProjectCategoryId));
        Assert.Empty(await Factory.BudgetRowsAsync());
    }

    [Fact]
    public async Task AnInvalidBudgetPayloadIs400NamingEveryField()
    {
        var request = new TimesheetProjectBudgetSaveRequest
        {
            ProjectId = 0,
            ProjectCategoryId = 0,
            AllocatedHours = -1m,
            HourlyRate = -1m,
            PeriodStart = new DateTime(2026, 5, 2),
            PeriodEnd = new DateTime(2026, 5, 1),
        };

        var response = await Client().PostAsJsonAsync(BudgetSaveUrl, request);

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetProjectBudgetSaveRequest.ProjectId),
            nameof(TimesheetProjectBudgetSaveRequest.ProjectCategoryId),
            nameof(TimesheetProjectBudgetSaveRequest.AllocatedHours),
            nameof(TimesheetProjectBudgetSaveRequest.HourlyRate),
            nameof(TimesheetProjectBudgetSaveRequest.PeriodEnd));
    }

    // ===================================================== budget: remove

    [Fact]
    public async Task RemovingABudgetDeletesOnlyThatRow()
    {
        await Factory.SeedProjectsAsync(Project(1, "PR0001"));
        await Factory.SeedBudgetsAsync(Budget(1, 1), Budget(2, 1));

        var response = await Client().PostAsJsonAsync(BudgetRemoveUrl, new TimesheetProjectBudgetDeleteRequest { BudgetId = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { 2 }, (await Factory.BudgetRowsAsync()).Select(budget => budget.BudgetId));
        Assert.Single(await Factory.ProjectRowsAsync());
    }

    [Fact]
    public async Task RemovingABudgetTwiceIs404TheSecondTime()
    {
        await Factory.SeedBudgetsAsync(Budget(1, 1));
        var request = new TimesheetProjectBudgetDeleteRequest { BudgetId = 1 };

        Assert.Equal(HttpStatusCode.OK, (await Client().PostAsJsonAsync(BudgetRemoveUrl, request)).StatusCode);

        await AssertNotFoundAsync(await Client().PostAsJsonAsync(BudgetRemoveUrl, request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RemovingABudgetRejectsANonPositiveId(int budgetId)
    {
        var response = await Client().PostAsJsonAsync(BudgetRemoveUrl, new TimesheetProjectBudgetDeleteRequest { BudgetId = budgetId });

        await AssertBadRequestNamingAsync(response, nameof(TimesheetProjectBudgetDeleteRequest.BudgetId));
    }

    // ====================================================== status: list

    [Fact]
    public async Task StatusListIsEmptyForAnOrganizationWithNoStatuses()
    {
        Assert.Empty(await GetOkAsync<List<TimesheetProjectStatusResponse>>(StatusListUrl));
    }

    /// <summary>Ordered by id - the order they were added in - and only live statuses of the organisation.</summary>
    [Fact]
    public async Task StatusListHoldsTheOrganizationsLiveStatusesInIdOrder()
    {
        await Factory.SeedStatusesAsync(
            Status(3, "Zeta", colorCode: "#333333"),
            Status(1, "Open", colorCode: null),
            Status(2, "Gone", isDeleted: true),
            Status(4, "Theirs", organizationId: OtherOrganizationId));

        var statuses = await GetOkAsync<List<TimesheetProjectStatusResponse>>(StatusListUrl);

        Assert.Equal(new[] { 1, 3 }, statuses.Select(status => status.StatusId));
        Assert.Equal(new[] { "Open", "Zeta" }, statuses.Select(status => status.Status));
        Assert.Null(statuses[0].ColorCode);
        Assert.Equal("#333333", statuses[1].ColorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task StatusListRejectsANonPositiveOrganizationId(int organizationId)
    {
        var response = await Client().GetAsync($"{BaseUrl}/get-timesheet-project-status-by-orgID/{organizationId}");

        await AssertBadRequestNamingAsync(response, "orgID");
    }

    // ================================================= status: add or edit

    [Fact]
    public async Task AddingAStatusWritesItStampedWithTheClockAndTheCaller()
    {
        var saved = await PostOkAsync<TimesheetProjectStatusResponse>(
            StatusSaveUrl,
            StatusRequest(status: "  Active  ", colorCode: " #00AA00 "));

        Assert.Equal("Active", saved.Status);
        Assert.Equal("#00AA00", saved.ColorCode);

        var row = Assert.Single(await Factory.StatusRowsAsync());
        Assert.Equal(saved.StatusId, row.StatusId);
        Assert.Equal("Active", row.Status);
        Assert.Equal("#00AA00", row.ColorCode);
        Assert.Equal(OrganizationId, row.OrganizationId);
        Assert.Equal(TimeLogTestData.Now, row.CreateDate);
        Assert.Equal(ActingUserId, row.CreatedBy);
        Assert.Null(row.UpdatedDate);
        Assert.Null(row.UpdatedBy);
        Assert.Null(row.DeletedDate);

        Assert.Equal(saved.StatusId, Assert.Single(await GetOkAsync<List<TimesheetProjectStatusResponse>>(StatusListUrl)).StatusId);
    }

    [Fact]
    public async Task ABlankColorCodeIsSavedAsNull()
    {
        var saved = await PostOkAsync<TimesheetProjectStatusResponse>(StatusSaveUrl, StatusRequest(statusId: 0, colorCode: "   "));

        Assert.Null(saved.ColorCode);
        Assert.Null(Assert.Single(await Factory.StatusRowsAsync()).ColorCode);
    }

    [Fact]
    public async Task EditingAStatusOverwritesItAndStampsTheUpdate()
    {
        await Factory.SeedStatusesAsync(Status(3, "Draft", colorCode: "#000000"));

        var saved = await PostOkAsync<TimesheetProjectStatusResponse>(
            StatusSaveUrl,
            StatusRequest(statusId: 3, status: "Approved", colorCode: null));

        Assert.Equal(3, saved.StatusId);
        Assert.Equal("Approved", saved.Status);
        Assert.Null(saved.ColorCode);

        var row = Assert.Single(await Factory.StatusRowsAsync());
        Assert.Equal("Approved", row.Status);
        Assert.Null(row.ColorCode);
        Assert.Equal(TimeLogTestData.Now, row.UpdatedDate);
        Assert.Equal(ActingUserId, row.UpdatedBy);
        Assert.Equal(SeedDate, row.CreateDate); // the creation stamp is left alone
        Assert.Equal(1, row.CreatedBy);
    }

    /// <summary>An edit only reaches a live status of the organisation it names.</summary>
    [Theory]
    [InlineData(4)] // another organisation's
    [InlineData(5)] // deleted
    [InlineData(99)] // missing
    public async Task EditingAStatusOutsideTheOrganizationIs404AndChangesNothing(int statusId)
    {
        await Factory.SeedStatusesAsync(
            Status(4, "Theirs", organizationId: OtherOrganizationId),
            Status(5, "Gone", isDeleted: true));

        var response = await Client().PostAsJsonAsync(StatusSaveUrl, StatusRequest(statusId: statusId, status: "Hijacked"));

        await AssertNotFoundAsync(response);

        var rows = await Factory.StatusRowsAsync();
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, row => row.Status == "Hijacked");
    }

    [Fact]
    public async Task AnInvalidStatusPayloadIs400NamingEveryField()
    {
        var request = new TimesheetProjectStatusSaveRequest
        {
            StatusId = -1,
            OrganizationId = 0,
            Status = "   ",
            ColorCode = new string('c', 26),
            UserId = 0,
        };

        var response = await Client().PostAsJsonAsync(StatusSaveUrl, request);

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetProjectStatusSaveRequest.StatusId),
            nameof(TimesheetProjectStatusSaveRequest.OrganizationId),
            nameof(TimesheetProjectStatusSaveRequest.Status),
            nameof(TimesheetProjectStatusSaveRequest.ColorCode),
            nameof(TimesheetProjectStatusSaveRequest.UserId));
        Assert.Empty(await Factory.StatusRowsAsync());
    }

    [Fact]
    public async Task AStatusNameLongerThanTheColumnIs400()
    {
        var response = await Client().PostAsJsonAsync(StatusSaveUrl, StatusRequest(status: new string('s', 21)));

        await AssertBadRequestNamingAsync(response, nameof(TimesheetProjectStatusSaveRequest.Status));
        Assert.Empty(await Factory.StatusRowsAsync());
    }

    // ===================================================== status: remove

    /// <summary>A soft delete: the row stays, stamped, and the list stops showing it.</summary>
    [Fact]
    public async Task RemovingAStatusSoftDeletesIt()
    {
        await Factory.SeedStatusesAsync(Status(1, "Open"), Status(2, "Closed"));

        var response = await Client().PostAsJsonAsync(
            StatusRemoveUrl,
            new TimesheetProjectStatusDeleteRequest { StatusId = 1, DeletedBy = ActingUserId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rows = await Factory.StatusRowsAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(TimeLogTestData.Now, rows[0].DeletedDate);
        Assert.Equal(ActingUserId, rows[0].DeletedBy);
        Assert.Null(rows[1].DeletedDate);

        Assert.Equal(new[] { 2 }, (await GetOkAsync<List<TimesheetProjectStatusResponse>>(StatusListUrl)).Select(status => status.StatusId));
    }

    /// <summary>The second remove is a 404 and does not re-stamp the first delete.</summary>
    [Fact]
    public async Task RemovingAStatusTwiceIs404AndKeepsTheFirstStamp()
    {
        await Factory.SeedStatusesAsync(Status(1, "Open"));

        Assert.Equal(
            HttpStatusCode.OK,
            (await Client().PostAsJsonAsync(StatusRemoveUrl, new TimesheetProjectStatusDeleteRequest { StatusId = 1, DeletedBy = ActingUserId })).StatusCode);

        await AssertNotFoundAsync(await Client().PostAsJsonAsync(
            StatusRemoveUrl,
            new TimesheetProjectStatusDeleteRequest { StatusId = 1, DeletedBy = ActingUserId + 1 }));

        Assert.Equal(ActingUserId, Assert.Single(await Factory.StatusRowsAsync()).DeletedBy);
    }

    [Fact]
    public async Task RemovingAMissingStatusIs404()
    {
        await AssertNotFoundAsync(await Client().PostAsJsonAsync(
            StatusRemoveUrl,
            new TimesheetProjectStatusDeleteRequest { StatusId = 99, DeletedBy = ActingUserId }));
    }

    [Fact]
    public async Task RemovingAStatusRejectsNonPositiveIds()
    {
        var response = await Client().PostAsJsonAsync(
            StatusRemoveUrl,
            new TimesheetProjectStatusDeleteRequest { StatusId = 0, DeletedBy = 0 });

        await AssertBadRequestNamingAsync(
            response,
            nameof(TimesheetProjectStatusDeleteRequest.StatusId),
            nameof(TimesheetProjectStatusDeleteRequest.DeletedBy));
    }
}
