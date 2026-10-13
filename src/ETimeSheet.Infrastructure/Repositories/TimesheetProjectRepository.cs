using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;
using ETimeSheet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core data access for the TimesheetProject module, over the
/// existing <c>dbo.TimesheetProject</c> table.
/// <para>
/// Queries, filters and saves only. No validation and no add-or-update
/// decision - those are <c>TimesheetProjectService</c>'s.
/// </para>
/// </summary>
public class TimesheetProjectRepository : ITimesheetProjectRepository
{
    private readonly Context _db;

    public TimesheetProjectRepository(Context db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TimesheetProjectDetail>> GetListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the join and the ordering; no LINQ is composed on
        // top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetProject.FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetProject @POrgID = {organizationId}").ToListAsync(cancellationToken);

    public async Task<TimesheetProjectSaveDetail?> SaveAsync(
        int? projectId,
        int organizationId,
        string? projectName,
        decimal? totalBudget,
        decimal? totalHours,
        DateTime? startDate,
        DateTime? endDate,
        int? currencyCode,
        int? status,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditTimesheetProject
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditTimesheetProject
                @PProjectID = {projectId},
                @POrgID = {organizationId},
                @PProjectName = {projectName},
                @PTotalBudget = {totalBudget},
                @PTotalHours = {totalHours},
                @PStartDate = {startDate},
                @PEndDate = {endDate},
                @PCurrencyCode = {currencyCode},
                @PStatus = {status}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<TimesheetProject?> GetForUpdateAsync(
        int projectId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The organisation is part
        // of the match, so another organisation's project is not found.
        await _db.TimesheetProject
            .FirstOrDefaultAsync(
                project => project.ProjectId == projectId && project.OrganizationId == organizationId,
                cancellationToken);

    public async Task DeleteAsync(
        TimesheetProject project,
        IReadOnlyList<TimesheetProjectBudget> budgets,
        CancellationToken cancellationToken = default)
    {
        // One SaveChanges, so the budget rows and the project go in one
        // transaction: a failure leaves both, never a budget without its project.
        _db.TimesheetProjectBudget.RemoveRange(budgets);
        _db.TimesheetProject.Remove(project);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimesheetProjectStatusDetail>> GetStatusListAsync(
        CancellationToken cancellationToken = default) =>
        // The procedure does the ordering; no LINQ is composed on top, because
        // SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetProjectStatus.FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetProjectStatus").ToListAsync(cancellationToken);

    public async Task<TimesheetProject?> GetByIdAsync(
        int projectId,
        CancellationToken cancellationToken = default) =>
        await _db.TimesheetProject
            .AsNoTracking()
            .FirstOrDefaultAsync(project => project.ProjectId == projectId, cancellationToken);

    public async Task<bool> ProjectCategoryExistsAsync(
        int projectCategoryId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The category's query filter hides deleted categories, so a deleted
        // one does not count.
        await _db.TimesheetProjectCategory
            .AnyAsync(
                category => category.CategoryId == projectCategoryId && category.OrganizationId == organizationId,
                cancellationToken);

    public async Task<IReadOnlyList<TimesheetProjectBudgetDetail>> GetBudgetListByProjectIdAsync(
        int projectId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the join and the ordering; no LINQ is composed on
        // top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetProjectBudget.FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetProjectBudget @PProjectID = {projectId}").ToListAsync(cancellationToken);

    public async Task<TimesheetProjectBudgetSaveDetail?> SaveBudgetAsync(
        int projectId,
        int? projectCategoryId,
        decimal? allocatedHours,
        decimal? hourlyRate,
        decimal? allocatedAmount,
        DateTime? periodStart,
        DateTime? periodEnd,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditTimesheetProjectBudget
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditTimesheetProjectBudget
                @PProjectID = {projectId},
                @PProjectCategoryID = {projectCategoryId},
                @PAllocatedHours = {allocatedHours},
                @PHourlyRate = {hourlyRate},
                @PAllocatedAmount = {allocatedAmount},
                @PPeriodStart = {periodStart},
                @PPeriodEnd = {periodEnd}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<IReadOnlyList<TimesheetProjectBudget>> GetBudgetListForUpdateByProjectIdAsync(
        int projectId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path.
        await _db.TimesheetProjectBudget
            .Where(budget => budget.ProjectId == projectId)
            .ToListAsync(cancellationToken);

    public async Task DeleteBudgetsAsync(
        IReadOnlyList<TimesheetProjectBudget> budgets,
        CancellationToken cancellationToken = default)
    {
        // One SaveChanges, so every row goes in one transaction or none does.
        _db.TimesheetProjectBudget.RemoveRange(budgets);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
