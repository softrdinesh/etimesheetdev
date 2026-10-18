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
        int clientId,
        int? submissionFrequency,
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
                @PStatus = {status},
                @PClientID = {clientId},
                @PSubmissionFrequency = {submissionFrequency}")
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

    public async Task<IReadOnlyList<TimesheetProjectStatusDetail>> GetStatusListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The procedure filters by organisation, drops deleted statuses and
        // does the ordering; no LINQ is composed on top, because SQL Server
        // cannot wrap an EXEC.
        await _db.spc_GetTimesheetProjectStatus.FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetProjectStatus @POrgID = {organizationId}").ToListAsync(cancellationToken);

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

    public async Task<TimesheetProjectBudget?> GetBudgetForUpdateAsync(
        int budgetId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path.
        await _db.TimesheetProjectBudget
            .FirstOrDefaultAsync(budget => budget.BudgetId == budgetId, cancellationToken);

    public async Task DeleteBudgetAsync(
        TimesheetProjectBudget budget,
        CancellationToken cancellationToken = default)
    {
        _db.TimesheetProjectBudget.Remove(budget);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int?> GetClientCurrencyCodeAsync(
        int clientId,
        CancellationToken cancellationToken = default) =>
        // The client's query filter hides deleted clients, so a deleted one
        // comes back null, as a missing one does.
        await _db.TimesheetClientSetup
            .AsNoTracking()
            .Where(client => client.ClientId == clientId)
            .Select(client => (int?)client.CurrencyCode)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<TimesheetProjectStatusSaveDetail?> SaveStatusAsync(
        int? statusId,
        int organizationId,
        string status,
        string? colorCode,
        int userId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditTimesheetProjectStatus
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditTimesheetProjectStatus
                @PStatusID = {statusId},
                @POrgID = {organizationId},
                @PStatus = {status},
                @PColorCode = {colorCode},
                @PUserID = {userId},
                @PDate = {now}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<TimesheetProjectStatus?> GetStatusForUpdateAsync(
        int statusId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter still
        // applies, so a status deleted already is not found.
        await _db.TimesheetProjectStatus
            .FirstOrDefaultAsync(status => status.StatusId == statusId, cancellationToken);

    public async Task UpdateStatusAsync(
        TimesheetProjectStatus status,
        CancellationToken cancellationToken = default) =>
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetProjectBudgetByOrgDetail>> GetBudgetListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the joins, the organisation filter and the
        // ordering; no LINQ is composed on top, because SQL Server cannot wrap
        // an EXEC.
        await _db.spc_GetTimesheetProjectBudgetByOrgID.FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetProjectBudgetByOrgID @POrgID = {organizationId}").ToListAsync(cancellationToken);

    public async Task<Signup?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        // The procedure's own match: TRIM(UPPER(Email)) = TRIM(UPPER(@Email)).
        // No IsDelete filter, because the procedure has none either.
        var normalizedEmail = email.Trim().ToUpperInvariant();

        return await _db.Signup
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email.Trim().ToUpper() == normalizedEmail, cancellationToken);
    }

    public async Task<bool> OrganizationExistsAsync(
        int organizationId,
        CancellationToken cancellationToken = default) =>
        await _db.Organization
            .AnyAsync(organization => organization.OrganizationId == organizationId, cancellationToken);

    public async Task<bool> CountryExistsAsync(
        int countryId,
        CancellationToken cancellationToken = default) =>
        await _db.Country
            .AnyAsync(country => country.Id == countryId, cancellationToken);

    public async Task<bool> UserRoleExistsAsync(
        int roleId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The role's query filter hides deleted roles, so a deleted one does
        // not count.
        await _db.TimesheetUserRole
            .AnyAsync(role => role.RoleId == roleId && role.OrganizationId == organizationId, cancellationToken);

    public async Task CreateTimesheetUserAsync(
        string fullName,
        string email,
        int? countryId,
        string? address,
        int organizationId,
        int? projectRoleId,
        int? jobType,
        decimal costRate,
        decimal billRate,
        int createdBy,
        int? workLocationRange,
        int projectId,
        string? projectLocationLatitude,
        int? maxLogHoursPerDay,
        CancellationToken cancellationToken = default)
    {
        // Opened here so the EXEC and the clean-up below share one connection.
        await _db.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            // @Range and @Projectloclongtitude are left to their NULL defaults:
            // the procedure never reads either. @Maxloghourday was added to the
            // procedure on 2026-10-18 and is still DRAFT: until the owner
            // applies it, this EXEC fails on the unknown parameter.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $@"EXEC dbo.spc_Signup_timesheet_user
                    @Fullname = {fullName},
                    @Email = {email},
                    @CountryID = {countryId},
                    @Address = {address},
                    @OrgID = {organizationId},
                    @ProjectroleID = {projectRoleId},
                    @Jobtype = {jobType},
                    @Costrate = {costRate},
                    @Billrate = {billRate},
                    @LoginuserID = {createdBy},
                    @worklocrange = {workLocationRange},
                    @projectID = {projectId},
                    @Projectloclatitude = {projectLocationLatitude},
                    @Maxloghourday = {maxLogHoursPerDay}",
                cancellationToken);
        }
        catch
        {
            // The procedure's CATCH swallows the real error and does not roll
            // back, so it ends with its transaction still open and SQL Server
            // raises error 266 instead. Roll back on this connection before it
            // goes back to the pool, rather than leave the partial inserts and
            // their locks waiting for the next borrower's reset.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;",
                CancellationToken.None);
            throw;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task<TimesheetUserProjectSetup?> GetUserProjectSetupForUpdateAsync(
        int timesheetUserProjectSetupId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter still
        // applies, so an assignment deleted already is not found.
        await _db.Timesheet_Users_Project_setup
            .FirstOrDefaultAsync(
                setup => setup.TimesheetUserProjectSetupId == timesheetUserProjectSetupId,
                cancellationToken);

    public async Task<Signup?> GetUserForUpdateAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - the edit may change the name and email.
        await _db.Signup
            .FirstOrDefaultAsync(user => user.UserId == userId, cancellationToken);

    public async Task<bool> UserProjectSetupExistsAsync(
        int userId,
        int projectId,
        int excludedTimesheetUserProjectSetupId,
        CancellationToken cancellationToken = default) =>
        // The query filter hides deleted assignments, so a deleted one does
        // not count.
        await _db.Timesheet_Users_Project_setup
            .AnyAsync(
                setup => setup.UserId == userId
                         && setup.ProjectId == projectId
                         && setup.TimesheetUserProjectSetupId != excludedTimesheetUserProjectSetupId,
                cancellationToken);

    public async Task UpdateTimesheetUserAsync(
        TimesheetUserProjectSetup setup,
        CancellationToken cancellationToken = default) =>
        // One SaveChanges, so the assignment and the user's dbo.Signup row go
        // in one transaction.
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetUserProjectSetup>> GetUserProjectSetupListForUpdateAsync(
        int userId,
        int projectId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter hides
        // assignments deleted already.
        await _db.Timesheet_Users_Project_setup
            .Where(setup => setup.UserId == userId && setup.ProjectId == projectId)
            .ToListAsync(cancellationToken);

    public async Task UpdateTimesheetUsersAsync(
        IReadOnlyList<TimesheetUserProjectSetup> setups,
        CancellationToken cancellationToken = default) =>
        // One SaveChanges, so every row is stamped or none is.
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetUserListDetail>> GetTimesheetUserListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the joins, the live-row filters and the ordering;
        // no LINQ is composed on top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetUserListByOrgID.FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetUserListByOrgID @POrgID = {organizationId}").ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetHolidaySetupDetail>> GetHolidaySetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the joins, the filters and the ordering; no LINQ
        // is composed on top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetHolidaySetup
            .FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetHolidaySetup @POrgID = {organizationId}, @PProjectID = {projectId}")
            .ToListAsync(cancellationToken);

    public async Task<TimesheetHolidaySetupSaveDetail?> SaveHolidaySetupAsync(
        int? timesheetHolidaySetupId,
        DateTime holidayDate,
        string? description,
        int organizationId,
        int? countryId,
        int? clientId,
        int? projectId,
        int createdBy,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditTimesheetHolidaySetup
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditTimesheetHolidaySetup
                @PTimesheetHolidaySetupID = {timesheetHolidaySetupId},
                @PHolidayDate = {holidayDate},
                @PDescription = {description},
                @POrgID = {organizationId},
                @PCountryID = {countryId},
                @PClientID = {clientId},
                @PProjectID = {projectId},
                @PUserID = {createdBy},
                @PDate = {now}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<TimesheetHolidaySetup?> GetHolidaySetupForUpdateAsync(
        int timesheetHolidaySetupId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter still
        // applies, so a holiday deleted already is not found.
        await _db.TimesheetHolidaysetup
            .FirstOrDefaultAsync(
                holiday => holiday.TimesheetHolidaySetupId == timesheetHolidaySetupId
                           && holiday.OrganizationId == organizationId,
                cancellationToken);

    public async Task UpdateHolidaySetupAsync(
        TimesheetHolidaySetup holiday,
        CancellationToken cancellationToken = default) =>
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<bool> ClientExistsAsync(
        int clientId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The client's query filter hides deleted clients, so a deleted one
        // does not count.
        await _db.TimesheetClientSetup
            .AnyAsync(client => client.ClientId == clientId && client.OrganizationId == organizationId, cancellationToken);

    public async Task<IReadOnlyList<TimesheetWeekendSetupDetail>> GetWeekendSetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the joins, the filters and the ordering; no LINQ
        // is composed on top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetWeekendSetup
            .FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetWeekendSetup @POrgID = {organizationId}, @PProjectID = {projectId}")
            .ToListAsync(cancellationToken);

    public async Task<TimesheetWeekendSetupSaveDetail?> SaveWeekendSetupAsync(
        int? timeSheetWeekendSetupId,
        int organizationId,
        int? clientId,
        int projectId,
        bool isMondayWeekend,
        bool isTuesdayWeekend,
        bool isWednesdayWeekend,
        bool isThursdayWeekend,
        bool isFridayWeekend,
        bool isSaturdayWeekend,
        bool isSundayWeekend,
        int userId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditTimesheetWeekendSetup
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditTimesheetWeekendSetup
                @PTimeSheetWeekendSetupID = {timeSheetWeekendSetupId},
                @POrgID = {organizationId},
                @PClientID = {clientId},
                @PProjectID = {projectId},
                @PIsMondayWeekend = {isMondayWeekend},
                @PIsTuesdayWeekend = {isTuesdayWeekend},
                @PIsWednesdayWeekend = {isWednesdayWeekend},
                @PIsThursdayWeekend = {isThursdayWeekend},
                @PIsFridayWeekend = {isFridayWeekend},
                @PIsSaturdayWeekend = {isSaturdayWeekend},
                @PIsSundayWeekend = {isSundayWeekend},
                @PUserID = {userId},
                @PDate = {now}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<bool> WeekendSetupExistsForProjectAsync(
        int projectId,
        int excludedTimeSheetWeekendSetupId,
        CancellationToken cancellationToken = default) =>
        // The query filter hides deleted setups, so a deleted one does not
        // count.
        await _db.TimeSheet_Weekend_setup
            .AnyAsync(
                setup => setup.ProjectId == projectId
                         && setup.TimeSheetWeekendSetupId != excludedTimeSheetWeekendSetupId,
                cancellationToken);

    public async Task<TimesheetWeekendSetup?> GetWeekendSetupForUpdateAsync(
        int timeSheetWeekendSetupId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter still
        // applies, so a setup deleted already is not found.
        await _db.TimeSheet_Weekend_setup
            .FirstOrDefaultAsync(setup => setup.TimeSheetWeekendSetupId == timeSheetWeekendSetupId, cancellationToken);

    public async Task UpdateWeekendSetupAsync(
        TimesheetWeekendSetup setup,
        CancellationToken cancellationToken = default) =>
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetRoleAssignmentDetail>> GetRoleAssignmentListAsync(
        int? organizationId,
        int? userId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the join, the filters and the ordering; no LINQ
        // is composed on top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetUserAccessSetup
            .FromSqlInterpolated($"EXEC dbo.spc_GetUserAccessSetup @POrgID = {organizationId}, @PUserID = {userId}")
            .ToListAsync(cancellationToken);

    public async Task<TimesheetRoleAssignmentSaveDetail?> SaveRoleAssignmentAsync(
        int? assignmentId,
        int userId,
        bool isClientSetup,
        bool isProject,
        bool isProjectBudget,
        bool isProjectCategory,
        bool isStatusSetup,
        bool isRoleSetup,
        bool isUserSetup,
        bool isTimeLog,
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditUserAccessSetup
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditUserAccessSetup
                @PAssignmentID = {assignmentId},
                @PUserID = {userId},
                @PIsClientSetup = {isClientSetup},
                @PIsProject = {isProject},
                @PIsProjectBudget = {isProjectBudget},
                @PIsProjectCategory = {isProjectCategory},
                @PIsStatusSetup = {isStatusSetup},
                @PIsRoleSetup = {isRoleSetup},
                @PIsUserSetup = {isUserSetup},
                @PIsTimeLog = {isTimeLog},
                @PLoginUserID = {loginUserId},
                @PDate = {now}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<bool> RoleAssignmentExistsForUserAsync(
        int userId,
        int excludedAssignmentId,
        CancellationToken cancellationToken = default) =>
        await _db.Timesheet_role_assignment
            .AnyAsync(
                assignment => assignment.UserId == userId && assignment.AssignmentId != excludedAssignmentId,
                cancellationToken);

    public async Task<TimesheetRoleAssignment?> GetRoleAssignmentForUpdateAsync(
        int assignmentId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path.
        await _db.Timesheet_role_assignment
            .FirstOrDefaultAsync(assignment => assignment.AssignmentId == assignmentId, cancellationToken);

    public async Task DeleteRoleAssignmentAsync(
        TimesheetRoleAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        _db.Timesheet_role_assignment.Remove(assignment);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UserExistsAsync(
        int userId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // dbo.Signup has no query filter; every procedure on it treats only
        // IsDelete = 0 as live, so a NULL is not live here either.
        await _db.Signup
            .AnyAsync(
                user => user.UserId == userId && user.OrganizationId == organizationId && user.IsDelete == 0,
                cancellationToken);
}
