using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access for <c>dbo.TimesheetProject</c>. Implemented in Infrastructure;
/// <c>TimesheetProjectService</c> depends only on this interface.
/// </summary>
public interface ITimesheetProjectRepository
{
    /// <summary>
    /// Returns the projects of one organisation, ordered by name, via
    /// <c>spc_GetTimesheetProject</c>.
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectDetail>> GetListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a project via <c>spc_AddEditTimesheetProject</c> and
    /// returns the saved row. The procedure generates the code on add.
    /// </summary>
    /// <param name="projectId"><see langword="null"/> to add; the id of the project to update.</param>
    /// <param name="projectName">Already trimmed and validated.</param>
    /// <returns>
    /// The saved project, or <see langword="null"/> when an update names a
    /// project that does not exist or belongs to another organisation.
    /// </returns>
    Task<TimesheetProjectSaveDetail?> SaveAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one project of one organisation, <b>tracked</b> for removal, or
    /// <see langword="null"/> when the project does not exist or belongs to
    /// another organisation.
    /// </summary>
    Task<TimesheetProject?> GetForUpdateAsync(
        int projectId,
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically deletes a project loaded by <see cref="GetForUpdateAsync"/>
    /// together with its budget rows, loaded by
    /// <see cref="GetBudgetListForUpdateByProjectIdAsync"/>, in one save - so
    /// either both go or neither does.
    /// </summary>
    Task DeleteAsync(
        TimesheetProject project,
        IReadOnlyList<TimesheetProjectBudget> budgets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live project statuses of one organisation, ordered by id,
    /// via <c>spc_GetTimesheetProjectStatus</c>.
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectStatusDetail>> GetStatusListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one project, <b>untracked</b>, or <see langword="null"/> when it
    /// does not exist. Read by the budget save to learn the project's
    /// organisation.
    /// </summary>
    Task<TimesheetProject?> GetByIdAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a live (not deleted) project category with this id belongs to
    /// this organisation.
    /// </summary>
    Task<bool> ProjectCategoryExistsAsync(
        int projectCategoryId,
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the budget lines of one project, ordered by period start, via
    /// <c>spc_GetTimesheetProjectBudget</c>.
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectBudgetDetail>> GetBudgetListByProjectIdAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the one budget of a project via
    /// <c>spc_AddEditTimesheetProjectBudget</c> - updating it when the project
    /// has one, adding it when not; the procedure decides - and returns the
    /// saved row.
    /// </summary>
    /// <param name="allocatedAmount">Already worked out from hours and rate.</param>
    /// <returns>
    /// The saved budget. <see langword="null"/> only if the procedure returns
    /// no row, which it does not.
    /// </returns>
    Task<TimesheetProjectBudgetSaveDetail?> SaveBudgetAsync(
        int projectId,
        int? projectCategoryId,
        decimal? allocatedHours,
        decimal? hourlyRate,
        decimal? allocatedAmount,
        DateTime? periodStart,
        DateTime? periodEnd,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every budget row of one project, <b>tracked</b> for removal -
    /// normally one, more only for duplicates saved before the one-budget rule.
    /// Empty when the project has no budget.
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectBudget>> GetBudgetListForUpdateByProjectIdAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one budget row, <b>tracked</b> for removal, or null when there
    /// is no row with this id.
    /// </summary>
    Task<TimesheetProjectBudget?> GetBudgetForUpdateAsync(
        int budgetId,
        CancellationToken cancellationToken = default);

    /// <summary>Physically deletes the budget row loaded by <see cref="GetBudgetForUpdateAsync"/>.</summary>
    Task DeleteBudgetAsync(
        TimesheetProjectBudget budget,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The <c>Currencycode</c> of the live (not deleted) client with this id,
    /// or null when no such client exists.
    /// </summary>
    Task<int?> GetClientCurrencyCodeAsync(
        int clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or edits a project status via
    /// <c>spc_AddEditTimesheetProjectStatus</c> and returns the saved row.
    /// </summary>
    /// <param name="statusId"><see langword="null"/> to add; the id of the status to edit.</param>
    /// <param name="status">Already trimmed and validated.</param>
    /// <param name="userId">Written to <c>CreatedBy</c> on add, <c>Updatedby</c> on edit.</param>
    /// <param name="now">Written to <c>CreateDate</c> on add, <c>UpdatedDate</c> on edit.</param>
    /// <returns>
    /// The saved status, or <see langword="null"/> when an edit names a status
    /// that does not exist, is deleted, or belongs to another organisation.
    /// </returns>
    Task<TimesheetProjectStatusSaveDetail?> SaveStatusAsync(
        int? statusId,
        int organizationId,
        string status,
        string? colorCode,
        int userId,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one live project status, <b>tracked</b> for removal, or
    /// <see langword="null"/> when it does not exist or is deleted already.
    /// </summary>
    Task<TimesheetProjectStatus?> GetStatusForUpdateAsync(
        int statusId,
        CancellationToken cancellationToken = default);

    /// <summary>Saves the changes made to a status loaded by <see cref="GetStatusForUpdateAsync"/>.</summary>
    Task UpdateStatusAsync(
        TimesheetProjectStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the budget lines of every project of one organisation, ordered
    /// by project name, then period start, via
    /// <c>spc_GetTimesheetProjectBudgetByOrgID</c>.
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectBudgetByOrgDetail>> GetBudgetListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <c>dbo.Signup</c> user with this email, <b>untracked</b>,
    /// matched as <c>spc_Signup_timesheet_user</c> matches it - ignoring case
    /// and surrounding spaces - or <see langword="null"/> when there is none.
    /// Deleted users are returned too: the procedure matches them as well.
    /// </summary>
    Task<Signup?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a <c>dbo.Organization</c> row with this id exists.</summary>
    Task<bool> OrganizationExistsAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a <c>dbo.Country</c> row with this id exists.</summary>
    Task<bool> CountryExistsAsync(
        int countryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a live (not deleted) <c>dbo.TimesheetUserRole</c> with this id
    /// belongs to this organisation.
    /// </summary>
    Task<bool> UserRoleExistsAsync(
        int roleId,
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a user up and puts them on a project via
    /// <c>spc_Signup_timesheet_user</c>. The procedure returns nothing; read
    /// the user back with <see cref="GetUserByEmailAsync"/>.
    /// </summary>
    /// <param name="fullName">Already trimmed and validated.</param>
    /// <param name="email">Already trimmed and validated.</param>
    /// <param name="address">Already trimmed; null when blank.</param>
    Task CreateTimesheetUserAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one live project assignment from
    /// <c>dbo.Timesheet_Users_Project_setup</c>, <b>tracked</b> for an edit or
    /// a soft delete, or <see langword="null"/> when it does not exist or is
    /// deleted already.
    /// </summary>
    Task<TimesheetUserProjectSetup?> GetUserProjectSetupForUpdateAsync(
        int timesheetUserProjectSetupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one <c>dbo.Signup</c> user, <b>tracked</b> for an edit, or
    /// <see langword="null"/> when there is none. Deleted users are returned
    /// too; the caller decides.
    /// </summary>
    Task<Signup?> GetUserForUpdateAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the user has a live assignment to this project other than
    /// <paramref name="excludedTimesheetUserProjectSetupId"/>.
    /// </summary>
    Task<bool> UserProjectSetupExistsAsync(
        int userId,
        int projectId,
        int excludedTimesheetUserProjectSetupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every live assignment of a user to a project, <b>tracked</b>
    /// for a soft delete - normally one, more only for duplicates. Empty when
    /// the user has no live assignment to the project.
    /// </summary>
    Task<IReadOnlyList<TimesheetUserProjectSetup>> GetUserProjectSetupListForUpdateAsync(
        int userId,
        int projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the changes made to the assignments loaded by
    /// <see cref="GetUserProjectSetupListForUpdateAsync"/>, in one save.
    /// </summary>
    Task UpdateTimesheetUsersAsync(
        IReadOnlyList<TimesheetUserProjectSetup> setups,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the changes made to an assignment loaded by
    /// <see cref="GetUserProjectSetupForUpdateAsync"/> - and to its user, when
    /// loaded by <see cref="GetUserForUpdateAsync"/> - in one save, so either
    /// both are written or neither is.
    /// </summary>
    Task UpdateTimesheetUserAsync(
        TimesheetUserProjectSetup setup,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every live project assignment of every live user of one organisation,
    /// via <c>spc_GetTimesheetUserListByOrgID</c>, ordered by user name, then
    /// project name. Empty when the organisation has none.
    /// </summary>
    Task<IReadOnlyList<TimesheetUserListDetail>> GetTimesheetUserListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live holidays of an organisation, a project or both, via
    /// <c>spc_GetTimesheetHolidaySetup</c>, ordered by date. A null filter
    /// matches anything. Empty when none match.
    /// </summary>
    Task<IReadOnlyList<TimesheetHolidaySetupDetail>> GetHolidaySetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a holiday (<paramref name="timesheetHolidaySetupId"/> null) or
    /// edits one, via <c>spc_AddEditTimesheetHolidaySetup</c>.
    /// </summary>
    /// <returns>
    /// The saved holiday, or <see langword="null"/> when an edit names a
    /// holiday that does not exist, is deleted, or belongs to another
    /// organisation.
    /// </returns>
    Task<TimesheetHolidaySetupSaveDetail?> SaveHolidaySetupAsync(
        int? timesheetHolidaySetupId,
        DateTime holidayDate,
        string? description,
        int organizationId,
        int? countryId,
        int? clientId,
        int? projectId,
        int createdBy,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a live holiday of an organisation, <b>tracked</b> for a soft
    /// delete; <see langword="null"/> when it does not exist, is deleted, or
    /// belongs to another organisation.
    /// </summary>
    Task<TimesheetHolidaySetup?> GetHolidaySetupForUpdateAsync(
        int timesheetHolidaySetupId,
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the changes made to a holiday loaded by
    /// <see cref="GetHolidaySetupForUpdateAsync"/>.
    /// </summary>
    Task UpdateHolidaySetupAsync(
        TimesheetHolidaySetup holiday,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a live client with this id belongs to the organisation.</summary>
    Task<bool> ClientExistsAsync(
        int clientId,
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live weekend setups of an organisation, a project or both, via
    /// <c>spc_GetTimesheetWeekendSetup</c>. A null filter matches anything.
    /// Empty when none match.
    /// </summary>
    Task<IReadOnlyList<TimesheetWeekendSetupDetail>> GetWeekendSetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a weekend setup (<paramref name="timeSheetWeekendSetupId"/> null)
    /// or edits one, via <c>spc_AddEditTimesheetWeekendSetup</c>.
    /// </summary>
    /// <returns>
    /// The saved setup, or <see langword="null"/> when an edit names a setup
    /// that does not exist, is deleted, or belongs to another organisation.
    /// </returns>
    Task<TimesheetWeekendSetupSaveDetail?> SaveWeekendSetupAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the project has a live weekend setup other than
    /// <paramref name="excludedTimeSheetWeekendSetupId"/>.
    /// </summary>
    Task<bool> WeekendSetupExistsForProjectAsync(
        int projectId,
        int excludedTimeSheetWeekendSetupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a live weekend setup, <b>tracked</b> for a soft delete;
    /// <see langword="null"/> when it does not exist or is deleted.
    /// </summary>
    Task<TimesheetWeekendSetup?> GetWeekendSetupForUpdateAsync(
        int timeSheetWeekendSetupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the changes made to a setup loaded by
    /// <see cref="GetWeekendSetupForUpdateAsync"/>.
    /// </summary>
    Task UpdateWeekendSetupAsync(
        TimesheetWeekendSetup setup,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The module assignments of live users of an organisation, a user or
    /// both, via <c>spc_GetUserAccessSetup</c>. A null filter matches
    /// anything. Empty when none match.
    /// </summary>
    Task<IReadOnlyList<TimesheetRoleAssignmentDetail>> GetRoleAssignmentListAsync(
        int? organizationId,
        int? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a module assignment (<paramref name="assignmentId"/> null) or
    /// edits one, via <c>spc_AddEditUserAccessSetup</c>.
    /// </summary>
    /// <returns>
    /// The saved assignment, or <see langword="null"/> when an edit names an
    /// assignment that does not exist.
    /// </returns>
    Task<TimesheetRoleAssignmentSaveDetail?> SaveRoleAssignmentAsync(
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
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the user has an assignment other than
    /// <paramref name="excludedAssignmentId"/>.
    /// </summary>
    Task<bool> RoleAssignmentExistsForUserAsync(
        int userId,
        int excludedAssignmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an assignment, <b>tracked</b> for a delete;
    /// <see langword="null"/> when it does not exist.
    /// </summary>
    Task<TimesheetRoleAssignment?> GetRoleAssignmentForUpdateAsync(
        int assignmentId,
        CancellationToken cancellationToken = default);

    /// <summary>Physically deletes an assignment loaded by <see cref="GetRoleAssignmentForUpdateAsync"/>.</summary>
    Task DeleteRoleAssignmentAsync(
        TimesheetRoleAssignment assignment,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a live user (<c>IsDelete</c> = 0) with this id belongs to the organisation.</summary>
    Task<bool> UserExistsAsync(
        int userId,
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live time entries of a user on live sheets, ordered by date, then
    /// start time. A null filter matches anything; the dates are inclusive.
    /// Empty when none match.
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectTimeLogDetail>> GetProjectTimeLogListAsync(
        int userId,
        int? projectId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a live time entry, <b>tracked</b> for an edit or a soft delete;
    /// <see langword="null"/> when it does not exist or is deleted.
    /// </summary>
    Task<TimesheetProjectTimeLog?> GetProjectTimeLogForUpdateAsync(
        int timesheetProjectTimeLogId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a live sheet, <b>tracked</b> so its totals can be changed;
    /// <see langword="null"/> when it does not exist or is deleted.
    /// </summary>
    Task<TimesheetProjectSubmission?> GetProjectSubmissionForUpdateAsync(
        int timesheetProjectSubmissionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live sheet of a user and project for the period starting on
    /// <paramref name="periodStartDate"/>, <b>tracked</b>;
    /// <see langword="null"/> when the period has no sheet yet.
    /// </summary>
    Task<TimesheetProjectSubmission?> GetProjectSubmissionForPeriodForUpdateAsync(
        int userId,
        int projectId,
        DateTime periodStartDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The highest generated <c>PSHT</c> sheet code - deleted sheets included,
    /// because their codes are spent - or <see langword="null"/> when there is
    /// none. Longer codes rank higher, so <c>PSHT10000</c> outranks
    /// <c>PSHT9999</c>.
    /// </summary>
    Task<string?> GetLatestProjectSubmissionCodeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The minutes of every live entry on a sheet except
    /// <paramref name="excludedTimesheetProjectTimeLogId"/>; 0 when there are none.
    /// </summary>
    Task<int> GetProjectSubmissionLoggedMinutesAsync(
        int timesheetProjectSubmissionId,
        int excludedTimesheetProjectTimeLogId,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts a new sheet and returns it with its <c>TimesheetProjectSubmissionId</c>.</summary>
    Task<TimesheetProjectSubmission> AddProjectSubmissionAsync(
        TimesheetProjectSubmission submission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts <paramref name="timeLog"/> when it is new, and saves it together
    /// with every change made to the tracked sheets - their totals - in one
    /// save, so the entry and the totals are written together or not at all.
    /// </summary>
    Task<TimesheetProjectTimeLog> SaveProjectTimeLogAsync(
        TimesheetProjectTimeLog timeLog,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live project assignments matching every filter given,
    /// <b>untracked</b>. A null filter matches anything;
    /// <paramref name="organizationId"/> matches on the project's organisation.
    /// </summary>
    Task<IReadOnlyList<TimesheetUserProjectSetup>> GetUserProjectSetupListAsync(
        int? userId,
        int? projectId,
        int? organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The organisations a user has project sheets in: those of the projects
    /// they are on now, and those of their stored sheets.
    /// </summary>
    Task<IReadOnlyList<int>> GetUserProjectOrganizationIdsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>The projects with these ids, <b>untracked</b>. An id with no project is left out.</summary>
    Task<IReadOnlyList<TimesheetProject>> GetListByIdsAsync(
        IReadOnlyCollection<int> projectIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live sheets matching every filter given - a null filter matches
    /// anything - each with the code, count and minutes of its live entries in
    /// <c>dbo.TimesheetProjectTimeLog</c>.
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectSheetDetail>> GetProjectSheetListAsync(
        int? userId,
        int? projectId,
        int? organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live sheet with this code, <b>tracked</b>;
    /// <see langword="null"/> when there is none.
    /// </summary>
    Task<TimesheetProjectSubmission?> GetProjectSubmissionByCodeForUpdateAsync(
        string sheetSubmissionCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the status change made to a tracked sheet together with its
    /// audit row, in one save - so the change is never written without its
    /// record, or the record without the change.
    /// </summary>
    Task SaveProjectSubmissionStatusAsync(
        TimesheetProjectSubmission submission,
        TimesheetProjectSubmissionAuditLog auditLog,
        CancellationToken cancellationToken = default);
}
