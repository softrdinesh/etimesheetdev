using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Services.Implementations;
using ETimeSheet.Shared.Exceptions;
using ETimeSheet.Shared.Utilities;
using ETimeSheet.Tests.Helpers;
using ETimeSheet.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ETimeSheet.Tests.Unit.Services;

/// <summary>
/// The employee-list read on <see cref="AdminService"/>.
/// <para>
/// The interesting behaviour here is the <b>summary</b>, because it is the only
/// part of this endpoint the API computes rather than passes through: everything
/// else in a row is worked out by <c>spc_GetEmployeeListByPOrgID</c>. So these
/// tests feed the service rows as the procedure would return them and check what
/// it counts from them - no database, no container, no SQL.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class AdminServiceEmployeeListTests
{
    private const int FullTime = Constants.TimesheetMasterSetup.ContractType.FullTime;
    private const int PartTime = Constants.TimesheetMasterSetup.ContractType.PartTime;

    private readonly Mock<IAdminRepository> _repository = new(MockBehavior.Strict);

    /// <summary>
    /// The class under test is never mocked - only its collaborators are. The
    /// clock and the logger take no part in this read, so they are the plainest
    /// thing that satisfies the constructor.
    /// </summary>
    private AdminService CreateService() =>
        new(_repository.Object,
            new FixedDateTimeProvider(TimeLogTestData.Now),
            NullLogger<AdminService>.Instance);

    private void RepositoryReturns(params EmployeeListDetail[] rows) =>
        _repository
            .Setup(repository => repository.GetEmployeeListByOrganizationIdAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

    [Fact]
    public async Task ReturnsEveryRowTheProcedureProduced()
    {
        RepositoryReturns(
            EmployeeListTestData.Row(userId: 1, setupId: 10),
            EmployeeListTestData.Row(userId: 2, setupId: 11),
            EmployeeListTestData.Row(userId: 3, setupId: null, contractTypeId: null, contractType: null));

        var result = await CreateService().GetEmployeeListByOrganizationIdAsync(
            EmployeeListTestData.OrganizationId);

        Assert.Equal(3, result.Employees.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result.Employees.Select(employee => employee.UserId));
    }

    [Fact]
    public async Task PassesTheOrganizationIdStraightToTheRepository()
    {
        RepositoryReturns();

        await CreateService().GetEmployeeListByOrganizationIdAsync(4242);

        _repository.Verify(
            repository => repository.GetEmployeeListByOrganizationIdAsync(
                4242,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ForwardsTheCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        RepositoryReturns();

        await CreateService().GetEmployeeListByOrganizationIdAsync(
            EmployeeListTestData.OrganizationId,
            cancellation.Token);

        _repository.Verify(
            repository => repository.GetEmployeeListByOrganizationIdAsync(
                It.IsAny<int>(),
                cancellation.Token),
            Times.Once);
    }

    [Fact]
    public async Task CountsTheHeadcountWithAndWithoutASetup()
    {
        RepositoryReturns(
            EmployeeListTestData.Row(userId: 1, setupId: 10),
            EmployeeListTestData.Row(userId: 2, setupId: 11),
            EmployeeListTestData.Row(userId: 3, setupId: null, contractTypeId: null, contractType: null),
            EmployeeListTestData.Row(userId: 4, setupId: null, contractTypeId: null, contractType: null),
            EmployeeListTestData.Row(userId: 5, setupId: null, contractTypeId: null, contractType: null));

        var summary = (await CreateService().GetEmployeeListByOrganizationIdAsync(
            EmployeeListTestData.OrganizationId)).Summary;

        Assert.Equal(5, summary.TotalEmployees);
        Assert.Equal(2, summary.TotalEmployeesWithSetup);
        Assert.Equal(3, summary.TotalEmployeesWithoutSetup);
    }

    [Fact]
    public async Task CountsFullTimeAndPartTimeSeparately()
    {
        RepositoryReturns(
            EmployeeListTestData.Row(userId: 1, contractTypeId: FullTime, contractType: "Full Time"),
            EmployeeListTestData.Row(userId: 2, contractTypeId: FullTime, contractType: "Full Time"),
            EmployeeListTestData.Row(userId: 3, contractTypeId: FullTime, contractType: "Full Time"),
            EmployeeListTestData.Row(userId: 4, contractTypeId: PartTime, contractType: "Part Time"));

        var summary = (await CreateService().GetEmployeeListByOrganizationIdAsync(
            EmployeeListTestData.OrganizationId)).Summary;

        Assert.Equal(3, summary.TotalFullTime);
        Assert.Equal(1, summary.TotalPartTime);
    }

    /// <summary>
    /// An employee with no setup has no contract, so they belong to neither
    /// count. Defaulting them to full time would report a contract nobody chose.
    /// </summary>
    [Fact]
    public async Task AnEmployeeWithoutASetupIsNeitherFullTimeNorPartTime()
    {
        RepositoryReturns(
            EmployeeListTestData.Row(userId: 1, contractTypeId: FullTime, contractType: "Full Time"),
            EmployeeListTestData.Row(userId: 2, setupId: null, contractTypeId: null, contractType: null));

        var summary = (await CreateService().GetEmployeeListByOrganizationIdAsync(
            EmployeeListTestData.OrganizationId)).Summary;

        Assert.Equal(2, summary.TotalEmployees);
        Assert.Equal(1, summary.TotalFullTime);
        Assert.Equal(0, summary.TotalPartTime);
    }

    /// <summary>
    /// A setup can exist and still name no contract, or name one the procedure
    /// does not spell out. Those rows count towards "with setup" and towards
    /// neither contract - which is why the two pairs of totals are allowed to
    /// disagree.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(99)]
    public async Task ASetupWithAnUnrecognisedContractCountsTowardsNeitherContractTotal(int? contractTypeId)
    {
        RepositoryReturns(
            EmployeeListTestData.Row(userId: 1, setupId: 10, contractTypeId: contractTypeId, contractType: null));

        var summary = (await CreateService().GetEmployeeListByOrganizationIdAsync(
            EmployeeListTestData.OrganizationId)).Summary;

        Assert.Equal(1, summary.TotalEmployees);
        Assert.Equal(1, summary.TotalEmployeesWithSetup);
        Assert.Equal(0, summary.TotalFullTime);
        Assert.Equal(0, summary.TotalPartTime);
    }

    /// <summary>
    /// The contract totals are read off the id, never the text beside it: the
    /// text exists to be displayed and could be reworded in the procedure without
    /// anyone thinking they had changed a total.
    /// </summary>
    [Fact]
    public async Task ContractTotalsFollowTheIdRatherThanTheText()
    {
        RepositoryReturns(
            EmployeeListTestData.Row(userId: 1, contractTypeId: PartTime, contractType: "Full Time"));

        var summary = (await CreateService().GetEmployeeListByOrganizationIdAsync(
            EmployeeListTestData.OrganizationId)).Summary;

        Assert.Equal(0, summary.TotalFullTime);
        Assert.Equal(1, summary.TotalPartTime);
    }

    [Fact]
    public async Task WithAndWithoutASetupAlwaysAddUpToTheHeadcount()
    {
        RepositoryReturns(
            EmployeeListTestData.Row(userId: 1, setupId: 10),
            EmployeeListTestData.Row(userId: 2, setupId: null, contractTypeId: null, contractType: null),
            EmployeeListTestData.Row(userId: 3, setupId: 12),
            EmployeeListTestData.Row(userId: 4, setupId: null, contractTypeId: null, contractType: null));

        var summary = (await CreateService().GetEmployeeListByOrganizationIdAsync(
            EmployeeListTestData.OrganizationId)).Summary;

        Assert.Equal(
            summary.TotalEmployees,
            summary.TotalEmployeesWithSetup + summary.TotalEmployeesWithoutSetup);
    }

    /// <summary>
    /// An organisation with nobody in it is an empty grid and a summary of
    /// zeroes - an answer, not a 404.
    /// </summary>
    [Fact]
    public async Task AnEmptyOrganizationIsAnEmptyGridAndZeroTotals()
    {
        RepositoryReturns();

        var result = await CreateService().GetEmployeeListByOrganizationIdAsync(
            EmployeeListTestData.OrganizationId);

        Assert.Empty(result.Employees);
        Assert.Equal(0, result.Summary.TotalEmployees);
        Assert.Equal(0, result.Summary.TotalEmployeesWithSetup);
        Assert.Equal(0, result.Summary.TotalEmployeesWithoutSetup);
        Assert.Equal(0, result.Summary.TotalFullTime);
        Assert.Equal(0, result.Summary.TotalPartTime);
    }

    /// <summary>
    /// Every computed figure in a row is the procedure's, and the API passes it
    /// through untouched - it must not round, reformat or recompute one, or the
    /// grid and a report run straight off the procedure would disagree.
    /// </summary>
    [Fact]
    public async Task EveryComputedFigureIsPassedThroughUnchanged()
    {
        var row = new EmployeeListDetail
        {
            UserId = 77,
            SetupId = 12,
            Name = "Priya Raman",
            Email = "priya@etimesheet.test",
            ExpectedHoursPerWeek = 37,
            ExpectedMinsPerWeek = 30,
            ExpectedHoursPerWeekText = "37h 30m/week",
            TotalLoggedHoursCurrentWeek = 12,
            TotalLoggedMinsCurrentWeek = 45,
            TotalLoggedHoursCurrentWeekText = "12h 45m",
            ProgressOnThisWeek = 34m,
            ContractTypeId = PartTime,
            ContractType = "Part Time"
        };

        RepositoryReturns(row);

        var employee = Assert.Single(
            (await CreateService().GetEmployeeListByOrganizationIdAsync(
                EmployeeListTestData.OrganizationId)).Employees);

        Assert.Equal(row.UserId, employee.UserId);
        Assert.Equal(row.SetupId, employee.SetupId);
        Assert.Equal(row.Name, employee.Name);
        Assert.Equal(row.Email, employee.Email);
        Assert.Equal(row.ExpectedHoursPerWeek, employee.ExpectedHoursPerWeek);
        Assert.Equal(row.ExpectedMinsPerWeek, employee.ExpectedMinsPerWeek);
        Assert.Equal(row.ExpectedHoursPerWeekText, employee.ExpectedHoursPerWeekText);
        Assert.Equal(row.TotalLoggedHoursCurrentWeek, employee.TotalLoggedHoursCurrentWeek);
        Assert.Equal(row.TotalLoggedMinsCurrentWeek, employee.TotalLoggedMinsCurrentWeek);
        Assert.Equal(row.TotalLoggedHoursCurrentWeekText, employee.TotalLoggedHoursCurrentWeekText);
        Assert.Equal(row.ProgressOnThisWeek, employee.ProgressOnThisWeek);
        Assert.Equal(row.ContractTypeId, employee.ContractTypeId);
        Assert.Equal(row.ContractType, employee.ContractType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task RejectsAnOrganizationIdThatIsNotPositive(int organizationId)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().GetEmployeeListByOrganizationIdAsync(organizationId));

        Assert.True(exception.Failures.ContainsKey("orgID"));

        // Strict mock: the repository is never set up, so this also proves the
        // database is not reached for a request that cannot be answered.
        _repository.VerifyNoOtherCalls();
    }
}
