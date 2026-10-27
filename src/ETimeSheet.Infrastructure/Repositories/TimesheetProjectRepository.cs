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
        string? projectLocationLongitude,
        int? maxLogHoursPerDay,
        CancellationToken cancellationToken = default)
    {
        // Opened here so the EXEC and the clean-up below share one connection.
        await _db.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            // @Range is left to its NULL default: the procedure never reads it.
            // Both coordinates have always been parameters, but the procedure
            // only writes the longitude from its 2026-10-22 revision, and
            // writes neither in the existing-user branch before its 2026-10-27
            // one; until the owner applies those, they are accepted and
            // dropped.
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
                    @Projectloclongtitude = {projectLocationLongitude},
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
        bool isViewTimesheet,
        bool isUserLeaveApply,
        bool isLeaveSetup,
        bool isLeaveAssignmentUser,
        bool isLeaveApproveReject,
        bool isReportView,
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
                @PIsViewTimesheet = {isViewTimesheet},
                @PIsUserLeaveApply = {isUserLeaveApply},
                @PIsLeaveSetup = {isLeaveSetup},
                @PIsLeaveAssignmentUser = {isLeaveAssignmentUser},
                @PIsLeaveApproveReject = {isLeaveApproveReject},
                @PIsReportView = {isReportView},
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

    public async Task<IReadOnlyList<TimesheetProjectTimeLog>> GetProjectTimeLogListAsync(
        int userId,
        int? projectId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default) =>
        // The query filter leaves deleted days out.
        await _db.TimesheetProjectTimeLog
            .AsNoTracking()
            .Where(timeLog => timeLog.UserId == userId
                              && (projectId == null || timeLog.ProjectId == projectId)
                              && (fromDate == null || timeLog.LogDate >= fromDate)
                              && (toDate == null || timeLog.LogDate <= toDate))
            .OrderBy(timeLog => timeLog.LogDate)
            .ThenBy(timeLog => timeLog.ProjectId)
            .ThenBy(timeLog => timeLog.TimesheetProjectTimeLogId)
            .ToListAsync(cancellationToken);

    public async Task<TimesheetProjectTimeLog?> GetProjectTimeLogForUpdateAsync(
        int timesheetProjectTimeLogId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter still
        // applies, so an entry deleted already is not found.
        await _db.TimesheetProjectTimeLog
            .FirstOrDefaultAsync(
                timeLog => timeLog.TimesheetProjectTimeLogId == timesheetProjectTimeLogId,
                cancellationToken);

    public async Task<string?> GetProjectTimeLogSheetCodeForPeriodAsync(
        int userId,
        int projectId,
        DateTime periodStartDate,
        DateTime periodEndDate,
        CancellationToken cancellationToken = default) =>
        await _db.TimesheetProjectTimeLog
            .AsNoTracking()
            // A deleted day has still given the period its code, so the filter
            // is ignored on purpose: skipping it would give the period a second.
            .IgnoreQueryFilters()
            .Where(timeLog => timeLog.UserId == userId
                              && timeLog.ProjectId == projectId
                              && timeLog.LogDate >= periodStartDate.Date
                              && timeLog.LogDate <= periodEndDate.Date
                              && timeLog.SheetSubmissionCode != "")
            // The first day logged in the period set the code.
            .OrderBy(timeLog => timeLog.TimesheetProjectTimeLogId)
            .Select(timeLog => timeLog.SheetSubmissionCode)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<string?> GetLatestProjectSheetCodeAsync(
        CancellationToken cancellationToken = default) =>
        // Both tables hand out codes from one sequence; deleted rows have
        // spent theirs, so neither query filter applies.
        await GeneratedSheetCodes(_db.TimesheetProjectTimeLog
                .IgnoreQueryFilters()
                .Select(timeLog => timeLog.SheetSubmissionCode))
            .Concat(GeneratedSheetCodes(_db.TimesheetProjectSubmission
                .IgnoreQueryFilters()
                .Select(submission => submission.SheetSubmissionCode)))
            // Longest first, then greatest: PSHT10000 outranks PSHT9999.
            .OrderByDescending(code => code.Length)
            .ThenByDescending(code => code)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The codes of the shape the sequence generates - "PSHT followed by
    /// digits and nothing else" - evaluated in SQL Server, so a hand-entered
    /// code cannot break the sequence.
    /// </summary>
    private static IQueryable<string> GeneratedSheetCodes(IQueryable<string> codes) =>
        codes.Where(code => EF.Functions.Like(code, "PSHT[0-9]%")
                            && !EF.Functions.Like(code.Substring(4), "%[^0-9]%"));

    public async Task<TimesheetProjectSubmission?> GetProjectSubmissionForUpdateAsync(
        int timesheetProjectSubmissionId,
        CancellationToken cancellationToken = default) =>
        await _db.TimesheetProjectSubmission
            .FirstOrDefaultAsync(
                submission => submission.TimesheetProjectSubmissionId == timesheetProjectSubmissionId,
                cancellationToken);

    public async Task<TimesheetProjectSubmission?> GetProjectSubmissionForPeriodForUpdateAsync(
        int userId,
        int projectId,
        DateTime periodStartDate,
        CancellationToken cancellationToken = default) =>
        // Oldest first, should duplicates ever exist: nothing in the database
        // stops two sheets for one period.
        await _db.TimesheetProjectSubmission
            .Where(submission => submission.UserId == userId
                                 && submission.ProjectId == projectId
                                 && submission.PeriodStartDate == periodStartDate.Date)
            .OrderBy(submission => submission.TimesheetProjectSubmissionId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> GetProjectLoggedMinutesForPeriodAsync(
        int userId,
        int projectId,
        DateTime periodStartDate,
        DateTime periodEndDate,
        CancellationToken cancellationToken = default) =>
        // Summed in the database. The query filter leaves deleted days out.
        await _db.TimesheetProjectTimeLog
            .Where(timeLog => timeLog.UserId == userId
                              && timeLog.ProjectId == projectId
                              && timeLog.LogDate >= periodStartDate.Date
                              && timeLog.LogDate <= periodEndDate.Date)
            .SumAsync(timeLog => timeLog.HoursLogged * 60, cancellationToken);

    public async Task<TimesheetProjectSubmission> AddProjectSubmissionAsync(
        TimesheetProjectSubmission submission,
        TimesheetProjectSubmissionAuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        // Linked through the model-only relationship, so one SaveChanges
        // inserts the sheet, then the audit row with the sheet's new identity -
        // one transaction, no explicit one to wrap in the retry strategy.
        auditLog.Submission = submission;

        _db.TimesheetProjectSubmission.Add(submission);
        _db.TimesheetProjectSubmissionAuditLog.Add(auditLog);

        await _db.SaveChangesAsync(cancellationToken);

        return submission;
    }

    public async Task<TimesheetProjectSubmission?> GetProjectSubmissionForDateAsync(
        int userId,
        int projectId,
        DateTime date,
        CancellationToken cancellationToken = default) =>
        // The stored period, not one worked out again: a project's start date
        // or frequency changed since the submit cannot unlock it. Oldest first,
        // should two rows ever overlap.
        await _db.TimesheetProjectSubmission
            .AsNoTracking()
            .Where(submission => submission.UserId == userId
                                 && submission.ProjectId == projectId
                                 && submission.PeriodStartDate <= date.Date
                                 && submission.PeriodEndDate >= date.Date)
            .OrderBy(submission => submission.TimesheetProjectSubmissionId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<DateTime?> GetProjectTimeLogDateForCodeAsync(
        int userId,
        int projectId,
        string sheetSubmissionCode,
        CancellationToken cancellationToken = default) =>
        await _db.TimesheetProjectTimeLog
            .AsNoTracking()
            // A deleted day still marks its period's code.
            .IgnoreQueryFilters()
            .Where(timeLog => timeLog.UserId == userId
                              && timeLog.ProjectId == projectId
                              && timeLog.SheetSubmissionCode == sheetSubmissionCode)
            .OrderBy(timeLog => timeLog.TimesheetProjectTimeLogId)
            .Select(timeLog => (DateTime?)timeLog.LogDate)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetUserDetailsDetail>> GetTimesheetUserDetailsAsync(
        int userId,
        DateTime today,
        CancellationToken cancellationToken = default) =>
        // The procedure does the joins, the totals, the current period and the
        // ordering; no LINQ is composed on top, because SQL Server cannot wrap
        // an EXEC. Only the date of today is passed - the procedure takes a DATE.
        await _db.spc_GetTimesheetUserDetailsByUserID
            .FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetUserDetailsByUserID @PUserID = {userId}, @PToday = {today.Date}")
            .ToListAsync(cancellationToken);

    public async Task<string?> GetUserTimeZoneAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        // The query filter hides deleted setups. Ordered, because nothing
        // stops a user having two.
        await _db.TimesheetMasterSetup
            .AsNoTracking()
            .Where(setup => setup.UserId == userId)
            .OrderBy(setup => setup.SetupId)
            .Select(setup => setup.TimeZone)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<TimesheetProjectTimeLog> SaveProjectTimeLogAsync(
        TimesheetProjectTimeLog timeLog,
        CancellationToken cancellationToken = default)
    {
        if (timeLog.TimesheetProjectTimeLogId == 0)
        {
            await _db.TimesheetProjectTimeLog.AddAsync(timeLog, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return timeLog;
    }

    public async Task<IReadOnlyList<TimesheetProject>> GetListByIdsAsync(
        IReadOnlyCollection<int> projectIds,
        CancellationToken cancellationToken = default) =>
        await _db.TimesheetProject
            .AsNoTracking()
            .Where(project => projectIds.Contains(project.ProjectId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetProjectSheetDetail>> GetProjectSheetListAsync(
        int? userId,
        int? projectId,
        int? organizationId,
        CancellationToken cancellationToken = default) =>
        // Every stored sheet is a submitted period. The count and the minutes
        // come from the time log - the user's days on
        // the project within the sheet's period - one subquery per sheet; the
        // time log's query filter applies inside them, so deleted days do not
        // count.
        await _db.TimesheetProjectSubmission
            .AsNoTracking()
            .Where(submission => (userId == null || submission.UserId == userId)
                                 && (projectId == null || submission.ProjectId == projectId)
                                 && (organizationId == null || submission.OrganizationId == organizationId))
            .Select(submission => new TimesheetProjectSheetDetail
            {
                TimesheetProjectSubmissionId = submission.TimesheetProjectSubmissionId,
                SheetSubmissionCode = submission.SheetSubmissionCode,
                StatusId = submission.StatusId,
                UserId = submission.UserId,
                ProjectId = submission.ProjectId,
                PeriodStartDate = submission.PeriodStartDate,
                PeriodEndDate = submission.PeriodEndDate,
                TimeLogCount = _db.TimesheetProjectTimeLog
                    .Count(timeLog => timeLog.UserId == submission.UserId
                                      && timeLog.ProjectId == submission.ProjectId
                                      && timeLog.LogDate >= submission.PeriodStartDate
                                      && timeLog.LogDate <= submission.PeriodEndDate),
                TotalMinutesLogged = _db.TimesheetProjectTimeLog
                    .Where(timeLog => timeLog.UserId == submission.UserId
                                      && timeLog.ProjectId == submission.ProjectId
                                      && timeLog.LogDate >= submission.PeriodStartDate
                                      && timeLog.LogDate <= submission.PeriodEndDate)
                    .Sum(timeLog => timeLog.HoursLogged * 60),
                SubmittedBy = submission.SubmittedBy,
                SubmittedDate = submission.SubmittedDate,
                ApprovedBy = submission.ApprovedBy,
                ApprovedDate = submission.ApprovedDate,
                RejectedBy = submission.RejectedBy,
                RejectedDate = submission.RejectedDate,
                RejectionReason = submission.RejectionReason,
            })
            .ToListAsync(cancellationToken);

    public async Task<TimesheetProjectSubmission?> GetProjectSubmissionByCodeForUpdateAsync(
        string sheetSubmissionCode,
        CancellationToken cancellationToken = default) =>
        // Oldest first, should duplicates ever exist: nothing in the database
        // makes the code unique.
        await _db.TimesheetProjectSubmission
            .Where(submission => submission.SheetSubmissionCode == sheetSubmissionCode)
            .OrderBy(submission => submission.TimesheetProjectSubmissionId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SaveProjectSubmissionStatusAsync(
        TimesheetProjectSubmission submission,
        TimesheetProjectSubmissionAuditLog auditLog,
        CancellationToken cancellationToken = default)
    {
        await _db.TimesheetProjectSubmissionAuditLog.AddAsync(auditLog, cancellationToken);

        // One SaveChanges: the sheet's new status and its audit row go in one
        // transaction.
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimesheetApproverSetupDetail>> GetApproverSetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the joins, the filters and the ordering; no LINQ
        // is composed on top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetApproverSetup
            .FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetApproverSetup @POrgID = {organizationId}, @PProjectID = {projectId}")
            .ToListAsync(cancellationToken);

    public async Task<TimesheetApproverSetupSaveDetail?> SaveApproverSetupAsync(
        int? timesheetApproverSetupId,
        int organizationId,
        int? projectId,
        int approverId,
        bool isTimesheetApprover,
        bool isLeaveApprover,
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditTimesheetApproverSetup
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditTimesheetApproverSetup
                @PTimesheetApproverSetupID = {timesheetApproverSetupId},
                @POrgID = {organizationId},
                @PProjectID = {projectId},
                @PApproverID = {approverId},
                @PIsTimesheetApprover = {isTimesheetApprover},
                @PIsLeaveApprover = {isLeaveApprover},
                @PLoginUserID = {loginUserId},
                @PDate = {now}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<bool> TimesheetApproverExistsForProjectAsync(
        int projectId,
        int excludedTimesheetApproverSetupId,
        CancellationToken cancellationToken = default) =>
        // The query filter hides deleted setups, so a deleted one does not
        // count. A NULL IsTimesheetApprover does not hold the area.
        await _db.Timesheet_Approver_setup
            .AnyAsync(
                setup => setup.ProjectId == projectId
                         && setup.IsTimesheetApprover == true
                         && setup.TimesheetApproverSetupId != excludedTimesheetApproverSetupId,
                cancellationToken);

    public async Task<bool> LeaveApproverExistsForOrganizationAsync(
        int organizationId,
        int excludedTimesheetApproverSetupId,
        CancellationToken cancellationToken = default) =>
        // The query filter hides deleted setups. The project does not matter:
        // the organisation has one leave approver. A NULL IsLeaveApprover does
        // not hold the slot.
        await _db.Timesheet_Approver_setup
            .AnyAsync(
                setup => setup.OrganizationId == organizationId
                         && setup.IsLeaveApprover == true
                         && setup.TimesheetApproverSetupId != excludedTimesheetApproverSetupId,
                cancellationToken);

    public async Task<TimesheetApproverSetup?> GetApproverSetupForUpdateAsync(
        int timesheetApproverSetupId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter still
        // applies, so a setup deleted already is not found.
        await _db.Timesheet_Approver_setup
            .FirstOrDefaultAsync(setup => setup.TimesheetApproverSetupId == timesheetApproverSetupId, cancellationToken);

    public async Task UpdateApproverSetupAsync(
        TimesheetApproverSetup setup,
        CancellationToken cancellationToken = default) =>
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<bool> IsProjectTimesheetApproverAsync(
        int projectId,
        int approverId,
        CancellationToken cancellationToken = default) =>
        // The query filter hides deleted setups, so a removed approver no
        // longer counts. A NULL IsTimesheetApprover is not an approver.
        await _db.Timesheet_Approver_setup
            .AnyAsync(
                setup => setup.ProjectId == projectId
                         && setup.ApproverId == approverId
                         && setup.IsTimesheetApprover == true,
                cancellationToken);

    public async Task<Signup?> GetUserByIdAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        await _db.Signup
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<TimesheetLeaveSetupDetail>> GetLeaveSetupListAsync(
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the filter and the ordering; no LINQ is composed
        // on top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetLeaveSetup
            .FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetLeaveSetup @POrgID = {organizationId}")
            .ToListAsync(cancellationToken);

    public async Task<TimesheetLeaveSetupSaveDetail?> SaveLeaveSetupAsync(
        int? leaveTypeId,
        int organizationId,
        string leaveType,
        decimal noOfLeaveAllocated,
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditTimesheetLeaveSetup
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditTimesheetLeaveSetup
                @PLeaveTypeID = {leaveTypeId},
                @POrgID = {organizationId},
                @PLeaveType = {leaveType},
                @PNoOfLeaveAllocated = {noOfLeaveAllocated},
                @PLoginUserID = {loginUserId},
                @PDate = {now}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<TimesheetLeaveTypeSetup?> GetLeaveSetupForUpdateAsync(
        int leaveTypeId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter still
        // applies, so a leave type deleted already is not found.
        await _db.Timesheet_Leavetype_Setup
            .FirstOrDefaultAsync(
                leaveType => leaveType.LeaveTypeId == leaveTypeId && leaveType.OrganizationId == organizationId,
                cancellationToken);

    public async Task UpdateLeaveSetupAsync(
        TimesheetLeaveTypeSetup leaveType,
        CancellationToken cancellationToken = default) =>
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetUserLeaveAssignmentDetail>> GetUserLeaveAssignmentListAsync(
        int? organizationId,
        int? userId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the joins, the filters and the ordering; no LINQ
        // is composed on top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetUserLeaveAssignment
            .FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetUserLeaveAssignment @POrgID = {organizationId}, @PUserID = {userId}")
            .ToListAsync(cancellationToken);

    public async Task<TimesheetUserLeaveAssignmentAddDetail?> AddUserLeaveAssignmentAsync(
        int organizationId,
        int userId,
        int leaveTypeId,
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddTimesheetUserLeaveAssignment
            .FromSqlInterpolated($@"EXEC dbo.spc_AddTimesheetUserLeaveAssignment
                @POrgID = {organizationId},
                @PUserID = {userId},
                @PLeaveTypeID = {leaveTypeId},
                @PLoginUserID = {loginUserId},
                @PDate = {now}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<TimesheetUserLeaveAssignmentEditDetail?> EditUserLeaveAssignmentAsync(
        int timesheetLeaveAssignmentUserId,
        int organizationId,
        int userId,
        int leaveTypeId,
        decimal? balanceLeave,
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // As AddUserLeaveAssignmentAsync: the list, then the first row.
        var rows = await _db.spc_EditTimesheetUserLeaveAssignment
            .FromSqlInterpolated($@"EXEC dbo.spc_EditTimesheetUserLeaveAssignment
                @PTimesheetLeaveAssignmentUserID = {timesheetLeaveAssignmentUserId},
                @POrgID = {organizationId},
                @PUserID = {userId},
                @PLeaveTypeID = {leaveTypeId},
                @PBalanceLeave = {balanceLeave},
                @PLoginUserID = {loginUserId},
                @PDate = {now}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<bool> LeaveTypeExistsAsync(
        int leaveTypeId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The query filter hides deleted leave types.
        await _db.Timesheet_Leavetype_Setup
            .AnyAsync(
                leaveType => leaveType.LeaveTypeId == leaveTypeId && leaveType.OrganizationId == organizationId,
                cancellationToken);

    public async Task<bool> UserLeaveAssignmentExistsAsync(
        int userId,
        int leaveTypeId,
        int excludedTimesheetLeaveAssignmentUserId,
        CancellationToken cancellationToken = default) =>
        // The query filter hides deleted assignments, so a removed one does not
        // count and the leave type may be assigned again.
        await _db.Timesheet_Leave_assignment_user
            .AnyAsync(
                assignment => assignment.UserId == userId
                              && assignment.LeaveTypeId == leaveTypeId
                              && assignment.TimesheetLeaveAssignmentUserId != excludedTimesheetLeaveAssignmentUserId,
                cancellationToken);

    public async Task<TimesheetLeaveAssignmentUser?> GetUserLeaveAssignmentForUpdateAsync(
        int timesheetLeaveAssignmentUserId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The table has no
        // organisation column, so the organisation is the user's. The query
        // filter still applies, so an assignment deleted already is not found.
        await _db.Timesheet_Leave_assignment_user
            .FirstOrDefaultAsync(
                assignment => assignment.TimesheetLeaveAssignmentUserId == timesheetLeaveAssignmentUserId
                              && _db.Signup.Any(user => user.UserId == assignment.UserId
                                                        && user.OrganizationId == organizationId),
                cancellationToken);

    public async Task UpdateUserLeaveAssignmentAsync(
        TimesheetLeaveAssignmentUser assignment,
        CancellationToken cancellationToken = default) =>
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetUserLeaveApplyDetail>> GetUserLeaveApplyListAsync(
        int? organizationId,
        int? userId,
        CancellationToken cancellationToken = default) =>
        // The procedure does the joins, the filters and the ordering; no LINQ
        // is composed on top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetUserLeaveApply
            .FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetUserLeaveApply @POrgID = {organizationId}, @PUserID = {userId}")
            .ToListAsync(cancellationToken);

    public async Task<TimesheetUserLeaveApplySaveDetail?> SaveUserLeaveApplyAsync(
        int? timesheetLeaveUserApplyId,
        int organizationId,
        int userId,
        int leaveTypeId,
        DateTime startDate,
        DateTime endDate,
        decimal noOfDays,
        string? userComments,
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditTimesheetUserLeaveApply
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditTimesheetUserLeaveApply
                @PTimesheetLeaveUserApplyID = {timesheetLeaveUserApplyId},
                @POrgID = {organizationId},
                @PUserID = {userId},
                @PLeaveTypeID = {leaveTypeId},
                @PStartDate = {startDate},
                @PEndDate = {endDate},
                @PNoOfDays = {noOfDays},
                @PUserComments = {userComments},
                @PLoginUserID = {loginUserId},
                @PDate = {now}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<TimesheetLeaveUserApply?> GetUserLeaveApplyForUpdateAsync(
        int timesheetLeaveUserApplyId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - the remove deletes it.
        await _db.Timesheet_Leave_User_apply
            .FirstOrDefaultAsync(
                application => application.TimesheetLeaveUserApplyId == timesheetLeaveUserApplyId
                               && application.OrganizationId == organizationId,
                cancellationToken);

    public async Task<int?> GetLeaveStatusValueAsync(
        string statusText,
        CancellationToken cancellationToken = default) =>
        // The lowest ID wins if the lookup ever holds the same text twice, as
        // in the add/edit procedure.
        await _db.TimesheetLeaveStatus_Lookup
            .AsNoTracking()
            .Where(status => status.Text == statusText)
            .OrderBy(status => status.Id)
            .Select(status => status.Value)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<TimesheetLeaveAssignmentUser?> GetUserLeaveAssignmentForBalanceAsync(
        int userId,
        int leaveTypeId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - the balance is written back. The query filter
        // hides removed assignments.
        await _db.Timesheet_Leave_assignment_user
            .OrderBy(assignment => assignment.TimesheetLeaveAssignmentUserId)
            .FirstOrDefaultAsync(
                assignment => assignment.UserId == userId && assignment.LeaveTypeId == leaveTypeId,
                cancellationToken);

    public async Task RemoveUserLeaveApplyAsync(
        TimesheetLeaveUserApply application,
        CancellationToken cancellationToken = default)
    {
        _db.Timesheet_Leave_User_apply.Remove(application);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsOrganizationLeaveApproverAsync(
        int organizationId,
        int approverId,
        CancellationToken cancellationToken = default) =>
        // The query filter hides deleted setups, so a removed approver no
        // longer counts. A NULL IsLeaveApprover is not an approver.
        await _db.Timesheet_Approver_setup
            .AnyAsync(
                setup => setup.OrganizationId == organizationId
                         && setup.ApproverId == approverId
                         && setup.IsLeaveApprover == true,
                cancellationToken);

    public async Task UpdateUserLeaveApplyAsync(
        TimesheetLeaveUserApply application,
        CancellationToken cancellationToken = default) =>
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<TimesheetLeaveUserApply>> GetLeaveApplyOverlappingAsync(
        int? organizationId,
        int? userId,
        int leaveStatus,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default) =>
        // Compared as datetime: StartDate / EndDate are stored at midnight, so
        // "starts on or before the last day and ends on or after the first" is
        // the overlap.
        await _db.Timesheet_Leave_User_apply
            .AsNoTracking()
            .Where(application => application.LeaveStatus == leaveStatus
                                  && application.StartDate <= toDate
                                  && application.EndDate >= fromDate
                                  && (organizationId == null || application.OrganizationId == organizationId)
                                  && (userId == null || application.UserId == userId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, string?>> GetUserTimeZonesAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default)
    {
        // One query for every user. The query filter hides deleted setups;
        // the lowest SetupId wins when a user has two, as in
        // GetUserTimeZoneAsync.
        var setups = await _db.TimesheetMasterSetup
            .AsNoTracking()
            .Where(setup => setup.UserId != null && userIds.Contains(setup.UserId.Value))
            .OrderBy(setup => setup.SetupId)
            .Select(setup => new { UserId = setup.UserId!.Value, setup.TimeZone })
            .ToListAsync(cancellationToken);

        return setups
            .GroupBy(setup => setup.UserId)
            .ToDictionary(group => group.Key, group => group.First().TimeZone);
    }

    public async Task<TimesheetHolidaySetup?> GetHolidayForProjectOnDateAsync(
        int organizationId,
        int projectId,
        int clientId,
        int? countryId,
        DateTime date,
        CancellationToken cancellationToken = default) =>
        // The query filter hides deleted holidays. HolidayDate is a date
        // column, so the day compares exactly.
        await _db.TimesheetHolidaysetup
            .AsNoTracking()
            .Where(holiday => holiday.HolidayDate == date.Date
                              && holiday.OrganizationId == organizationId
                              && (holiday.CountryId == null || holiday.CountryId == countryId)
                              && (holiday.ClientId == null || holiday.ClientId == clientId)
                              && (holiday.ProjectId == null || holiday.ProjectId == projectId))
            .OrderBy(holiday => holiday.TimesheetHolidaySetupId)
            .FirstOrDefaultAsync(cancellationToken);
}
