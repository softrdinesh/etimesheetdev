using ETimeSheet.Application.Models;
using ETimeSheet.Shared.Responses;
using ETimeSheet.Shared.Utilities;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;
using System.Net;

namespace ETimeSheet.Tests.Integration;

/// <summary>
/// <c>GET /api/v1/Admin/get-all-employees-by-orgid/{orgID}</c>, end to end against a real
/// SQL Server: the real <c>dbo.spc_GetEmployeeListByPOrgID</c>, the real
/// mapping, the real middleware.
/// <para>
/// This is where the procedure's arithmetic is actually proven. The unit tests
/// feed <c>AdminService</c> rows by hand and check what it counts from them;
/// only a real database can show that the recorded procedure compiles, that the
/// keyless result type binds to its aliases, and that a contracted week really
/// does come back as 40 hours.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(IntegrationTestCollection.Name)]
public class EmployeeListEndpointTests : IntegrationTestBase
{
    private const int FullTime = Constants.TimesheetMasterSetup.ContractType.FullTime;
    private const int PartTime = Constants.TimesheetMasterSetup.ContractType.PartTime;

    /// <summary>
    /// Monday to Friday at 8 hours a day: (5 - 1 + 1) x 480 minutes = 2400,
    /// which is 40 hours with nothing left over. Chosen so an assertion never
    /// has to reason about a remainder.
    /// </summary>
    private const int ContractedHoursPerWeek = 40;

    public EmployeeListEndpointTests(SqlServerFixture fixture)
        : base(fixture)
    {
    }

    private static string Url(int organizationId) =>
        $"/api/v1/Admin/get-all-employees-by-orgid/{organizationId}";

    private async Task<EmployeeListResponse> GetAsync(int organizationId)
    {
        // The endpoint is [AllowAnonymous] while authentication is off, so an
        // unauthenticated client is the honest way to call it.
        var response = await Factory.CreateClient().GetAsync(Url(organizationId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.ReadDataAsync<EmployeeListResponse>();
    }

    [Fact]
    public async Task ReturnsOnlyTheEmployeesOfTheRequestedOrganization()
    {
        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.FullTimeUserId),
            EmployeeListTestData.Employee(EmployeeListTestData.PartTimeUserId),
            EmployeeListTestData.Employee(
                EmployeeListTestData.OtherOrganizationUserId,
                organizationId: EmployeeListTestData.OtherOrganizationId));

        var result = await GetAsync(EmployeeListTestData.OrganizationId);

        Assert.Equal(
            new[] { EmployeeListTestData.FullTimeUserId, EmployeeListTestData.PartTimeUserId },
            result.Employees.Select(employee => employee.UserId).OrderBy(id => id));
    }

    /// <summary>
    /// The procedure decides what an employee is - <c>Signup.RoleID = 2</c> -
    /// and anybody else in the same organisation is not in this list.
    /// </summary>
    [Fact]
    public async Task ExcludesPeopleWhoAreNotInTheEmployeeRole()
    {
        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.FullTimeUserId),
            EmployeeListTestData.Employee(
                EmployeeListTestData.NonEmployeeUserId,
                roleId: EmployeeListTestData.SignupNonEmployeeRoleId));

        var result = await GetAsync(EmployeeListTestData.OrganizationId);

        var employee = Assert.Single(result.Employees);
        Assert.Equal(EmployeeListTestData.FullTimeUserId, employee.UserId);
    }

    [Fact]
    public async Task CarriesTheNameAndEmailFromTheSignupRow()
    {
        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.FullTimeUserId, name: "Priya Raman"));

        var employee = Assert.Single((await GetAsync(EmployeeListTestData.OrganizationId)).Employees);

        Assert.Equal("Priya Raman", employee.Name);
        Assert.Equal($"employee{EmployeeListTestData.FullTimeUserId}@etimesheet.test", employee.Email);
    }

    /// <summary>
    /// The LEFT JOIN is the point: an employee nobody has set up still appears,
    /// because finding them is a reason to open this screen.
    /// </summary>
    [Fact]
    public async Task IncludesAnEmployeeWhoHasNoTimesheetSetup()
    {
        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.NoSetupUserId));

        var employee = Assert.Single((await GetAsync(EmployeeListTestData.OrganizationId)).Employees);

        Assert.Null(employee.SetupId);
        Assert.Null(employee.ExpectedHoursPerWeek);
        Assert.Null(employee.ExpectedMinsPerWeek);
        Assert.Null(employee.ExpectedHoursPerWeekText);
        Assert.Null(employee.ContractTypeId);
        Assert.Null(employee.ContractType);
        Assert.Equal(0m, employee.ProgressOnThisWeek);
    }

    [Fact]
    public async Task ComputesTheContractedWeekFromTheSetup()
    {
        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.FullTimeUserId));

        await Factory.SeedSetupsAsync(
            EmployeeListTestData.Setup(EmployeeListTestData.FullTimeUserId, FullTime));

        var employee = Assert.Single((await GetAsync(EmployeeListTestData.OrganizationId)).Employees);

        Assert.NotNull(employee.SetupId);
        Assert.Equal(ContractedHoursPerWeek, employee.ExpectedHoursPerWeek);
        Assert.Equal(0, employee.ExpectedMinsPerWeek);
        Assert.Equal("40h/week", employee.ExpectedHoursPerWeekText);
        Assert.Equal(FullTime, employee.ContractTypeId);
        Assert.Equal("Full Time", employee.ContractType);
    }

    [Fact]
    public async Task SpellsOutAPartTimeContract()
    {
        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.PartTimeUserId));

        await Factory.SeedSetupsAsync(
            EmployeeListTestData.Setup(EmployeeListTestData.PartTimeUserId, PartTime));

        var employee = Assert.Single((await GetAsync(EmployeeListTestData.OrganizationId)).Employees);

        Assert.Equal(PartTime, employee.ContractTypeId);
        Assert.Equal("Part Time", employee.ContractType);
    }

    /// <summary>
    /// Eight of the contracted forty hours logged this week: 8h 0m, and 20%.
    /// The week comes from the server, because the procedure reads GETDATE().
    /// </summary>
    [Fact]
    public async Task CountsTimeLoggedInsideTheCurrentWeek()
    {
        var weekStart = await Factory.CurrentWeekStartAsync();

        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.FullTimeUserId));

        await Factory.SeedSetupsAsync(
            EmployeeListTestData.Setup(EmployeeListTestData.FullTimeUserId, FullTime));

        await Factory.SeedAsync(TimeLogTestData.Existing(
            userId: EmployeeListTestData.FullTimeUserId,
            startDate: weekStart,
            startTime: TimeSpan.FromHours(9),
            durationMinutes: 480));

        var employee = Assert.Single((await GetAsync(EmployeeListTestData.OrganizationId)).Employees);

        Assert.Equal(8, employee.TotalLoggedHoursCurrentWeek);
        Assert.Equal(0, employee.TotalLoggedMinsCurrentWeek);
        Assert.Equal("8h", employee.TotalLoggedHoursCurrentWeekText);

        // 480 of 2400 minutes.
        Assert.Equal(20m, employee.ProgressOnThisWeek);
    }

    [Fact]
    public async Task ReportsTheLeftoverMinutesSeparatelyFromTheHours()
    {
        var weekStart = await Factory.CurrentWeekStartAsync();

        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.FullTimeUserId));

        await Factory.SeedAsync(TimeLogTestData.Existing(
            userId: EmployeeListTestData.FullTimeUserId,
            startDate: weekStart,
            startTime: TimeSpan.FromHours(9),
            durationMinutes: 165));

        var employee = Assert.Single((await GetAsync(EmployeeListTestData.OrganizationId)).Employees);

        Assert.Equal(2, employee.TotalLoggedHoursCurrentWeek);
        Assert.Equal(45, employee.TotalLoggedMinsCurrentWeek);
        Assert.Equal("2h 45m", employee.TotalLoggedHoursCurrentWeekText);
    }

    /// <summary>
    /// The window has to actually exclude something, or the previous test would
    /// pass against a procedure that summed the whole table.
    /// </summary>
    [Fact]
    public async Task IgnoresTimeLoggedBeforeTheCurrentWeek()
    {
        var weekStart = await Factory.CurrentWeekStartAsync();

        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.FullTimeUserId));

        await Factory.SeedSetupsAsync(
            EmployeeListTestData.Setup(EmployeeListTestData.FullTimeUserId, FullTime));

        await Factory.SeedAsync(TimeLogTestData.Existing(
            userId: EmployeeListTestData.FullTimeUserId,
            startDate: weekStart.AddDays(-3),
            startTime: TimeSpan.FromHours(9),
            durationMinutes: 480));

        var employee = Assert.Single((await GetAsync(EmployeeListTestData.OrganizationId)).Employees);

        Assert.Equal(0, employee.TotalLoggedHoursCurrentWeek);
        Assert.Equal(0, employee.TotalLoggedMinsCurrentWeek);
        Assert.Equal("0h", employee.TotalLoggedHoursCurrentWeekText);
        Assert.Equal(0m, employee.ProgressOnThisWeek);
    }

    /// <summary>
    /// Logging more than the contract does not push the bar past its end - the
    /// procedure clamps it, so a client can draw it without clamping again.
    /// </summary>
    [Fact]
    public async Task ProgressIsCappedAtOneHundred()
    {
        var weekStart = await Factory.CurrentWeekStartAsync();

        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.FullTimeUserId));

        await Factory.SeedSetupsAsync(
            EmployeeListTestData.Setup(EmployeeListTestData.FullTimeUserId, FullTime));

        // Six 10-hour days: 3600 minutes against a 2400-minute contract.
        var entries = Enumerable
            .Range(0, 6)
            .Select(day => TimeLogTestData.Existing(
                userId: EmployeeListTestData.FullTimeUserId,
                startDate: weekStart.AddDays(day),
                startTime: TimeSpan.FromHours(8),
                durationMinutes: 600))
            .ToArray();

        await Factory.SeedAsync(entries);

        var employee = Assert.Single((await GetAsync(EmployeeListTestData.OrganizationId)).Employees);

        Assert.Equal(60, employee.TotalLoggedHoursCurrentWeek);
        Assert.Equal(100m, employee.ProgressOnThisWeek);
    }

    [Fact]
    public async Task SummaryCountsTheRowsItReturned()
    {
        await Factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(EmployeeListTestData.FullTimeUserId),
            EmployeeListTestData.Employee(EmployeeListTestData.PartTimeUserId),
            EmployeeListTestData.Employee(EmployeeListTestData.NoSetupUserId),
            EmployeeListTestData.Employee(
                EmployeeListTestData.OtherOrganizationUserId,
                organizationId: EmployeeListTestData.OtherOrganizationId));

        await Factory.SeedSetupsAsync(
            EmployeeListTestData.Setup(EmployeeListTestData.FullTimeUserId, FullTime),
            EmployeeListTestData.Setup(EmployeeListTestData.PartTimeUserId, PartTime));

        var result = await GetAsync(EmployeeListTestData.OrganizationId);

        Assert.Equal(3, result.Summary.TotalEmployees);
        Assert.Equal(2, result.Summary.TotalEmployeesWithSetup);
        Assert.Equal(1, result.Summary.TotalEmployeesWithoutSetup);
        Assert.Equal(1, result.Summary.TotalFullTime);
        Assert.Equal(1, result.Summary.TotalPartTime);

        // The summary describes exactly the rows underneath it - the whole
        // reason it is counted from them rather than queried separately.
        Assert.Equal(result.Employees.Count, result.Summary.TotalEmployees);
    }

    [Fact]
    public async Task AnOrganizationWithNobodyInItIsAnEmptyGridRatherThanAFourOhFour()
    {
        var result = await GetAsync(EmployeeListTestData.OrganizationId);

        Assert.Empty(result.Employees);
        Assert.Equal(0, result.Summary.TotalEmployees);
        Assert.Equal(0, result.Summary.TotalEmployeesWithSetup);
        Assert.Equal(0, result.Summary.TotalEmployeesWithoutSetup);
        Assert.Equal(0, result.Summary.TotalFullTime);
        Assert.Equal(0, result.Summary.TotalPartTime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task RejectsAnOrganizationIdThatIsNotPositive(int organizationId)
    {
        var response = await Factory.CreateClient().GetAsync(Url(organizationId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<EmployeeListResponse>();

        Assert.False(envelope.Success);
        Assert.Contains(envelope.Errors, error => error.StartsWith("orgID:", StringComparison.Ordinal));
    }

    /// <summary>
    /// The organisation id is a route segment, so leaving it off is not a
    /// request for every organisation - it is a different URL, and there is
    /// nothing there. The caller is never handed a grid for an organisation
    /// they did not name.
    /// </summary>
    [Fact]
    public async Task RejectsARequestWithNoOrganizationIdAtAll()
    {
        var response = await Factory.CreateClient()
            .GetAsync("/api/v1/Admin/get-all-employees-by-orgid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IsAnonymousWhileAuthenticationIsOff()
    {
        var response = await Factory.CreateClient()
            .GetAsync(Url(EmployeeListTestData.OrganizationId));

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
