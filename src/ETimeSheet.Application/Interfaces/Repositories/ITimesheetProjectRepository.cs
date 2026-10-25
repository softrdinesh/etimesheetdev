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
        string? projectLocationLongitude,
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
        bool isUserLeaveApply,
        bool isLeaveSetup,
        bool isLeaveAssignmentUser,
        bool isLeaveApproveReject,
        bool isReportView,
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
    /// The live time log days of a user, <b>untracked</b>, ordered by date,
    /// then project. A null filter matches anything; the dates are inclusive.
    /// Empty when none match.
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectTimeLog>> GetProjectTimeLogListAsync(
        int userId,
        int? projectId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a live time log day, <b>tracked</b> for a soft delete;
    /// <see langword="null"/> when it does not exist or is deleted.
    /// </summary>
    Task<TimesheetProjectTimeLog?> GetProjectTimeLogForUpdateAsync(
        int timesheetProjectTimeLogId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the user's live time log for a project on one day,
    /// <b>tracked</b> so hours can be added to it; <see langword="null"/> when
    /// nothing is logged that day.
    /// </summary>
    Task<TimesheetProjectTimeLog?> GetProjectTimeLogForDayForUpdateAsync(
        int userId,
        int projectId,
        DateTime logDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The sheet code already on a user's time on a project between the two
    /// dates, both inclusive - <b>deleted days included</b>, since their code
    /// is spent; <see langword="null"/> when no day in the range has one.
    /// </summary>
    Task<string?> GetProjectTimeLogSheetCodeForPeriodAsync(
        int userId,
        int projectId,
        DateTime periodStartDate,
        DateTime periodEndDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The highest generated <c>PSHT</c> sheet code in either
    /// <c>dbo.TimesheetProjectTimeLog</c> or
    /// <c>dbo.TimesheetProjectSubmission</c> - deleted rows included, because
    /// their codes are spent - or <see langword="null"/> when there is none.
    /// Longer codes rank higher, so <c>PSHT10000</c> outranks <c>PSHT9999</c>.
    /// </summary>
    Task<string?> GetLatestProjectSheetCodeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The most hours a day the user may log on the project:
    /// <c>Max_log_hour_day</c> of their live assignment in
    /// <c>dbo.Timesheet_Users_Project_setup</c>. <see langword="null"/> when
    /// the assignment sets no cap.
    /// </summary>
    Task<int?> GetUserProjectMaxLogHoursPerDayAsync(
        int userId,
        int projectId,
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
    /// The minutes logged by a user on a project from
    /// <paramref name="periodStartDate"/> to <paramref name="periodEndDate"/>,
    /// both inclusive; 0 when there are none.
    /// </summary>
    Task<int> GetProjectLoggedMinutesForPeriodAsync(
        int userId,
        int projectId,
        DateTime periodStartDate,
        DateTime periodEndDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new sheet together with its first audit row, in one save, so
    /// a sheet is never written without its record. Returns the sheet with its
    /// <c>TimesheetProjectSubmissionId</c>.
    /// </summary>
    Task<TimesheetProjectSubmission> AddProjectSubmissionAsync(
        TimesheetProjectSubmission submission,
        TimesheetProjectSubmissionAuditLog auditLog,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live sheet of a user and project whose period contains
    /// <paramref name="date"/>, <b>untracked</b>; <see langword="null"/> when
    /// that period has not been submitted.
    /// </summary>
    Task<TimesheetProjectSubmission?> GetProjectSubmissionForDateAsync(
        int userId,
        int projectId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A log date of a user's time on a project that carries
    /// <paramref name="sheetSubmissionCode"/> - deleted days included - or
    /// <see langword="null"/> when none does. Any one will do: every day with
    /// the code is in the same period.
    /// </summary>
    Task<DateTime?> GetProjectTimeLogDateForCodeAsync(
        int userId,
        int projectId,
        string sheetSubmissionCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One user's details via <c>spc_GetTimesheetUserDetailsByUserID</c>: one
    /// row per live project assignment, the user's columns repeated, and the
    /// current timesheet period worked out for <paramref name="today"/>. A
    /// user with no assignment is one row with no project; a missing or
    /// deleted user is no rows.
    /// </summary>
    Task<IReadOnlyList<TimesheetUserDetailsDetail>> GetTimesheetUserDetailsAsync(
        int userId,
        DateTime today,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The IANA time zone of the user's live timesheet setup in
    /// <c>dbo.TimesheetMasterSetup</c>; <see langword="null"/> when they have
    /// no setup or it names no zone.
    /// </summary>
    Task<string?> GetUserTimeZoneAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts <paramref name="timeLog"/> when it is new, or saves the changes
    /// made to it when it was loaded tracked.
    /// </summary>
    Task<TimesheetProjectTimeLog> SaveProjectTimeLogAsync(
        TimesheetProjectTimeLog timeLog,
        CancellationToken cancellationToken = default);

    /// <summary>The projects with these ids, <b>untracked</b>. An id with no project is left out.</summary>
    Task<IReadOnlyList<TimesheetProject>> GetListByIdsAsync(
        IReadOnlyCollection<int> projectIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live sheets - every submitted period - matching every filter given;
    /// a null filter matches anything. Each comes with the count and minutes
    /// of the user's live days on the project within its period, from
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

    /// <summary>
    /// The live approver setups of an organisation, a project or both, via
    /// <c>spc_GetTimesheetApproverSetup</c>. A null filter matches anything.
    /// Empty when none match.
    /// </summary>
    Task<IReadOnlyList<TimesheetApproverSetupDetail>> GetApproverSetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an approver setup (<paramref name="timesheetApproverSetupId"/>
    /// null) or edits one, via <c>spc_AddEditTimesheetApproverSetup</c>, and
    /// returns the saved row. <see langword="null"/> when an edit names a
    /// setup that does not exist, is deleted, or belongs to another
    /// organisation.
    /// </summary>
    Task<TimesheetApproverSetupSaveDetail?> SaveApproverSetupAsync(
        int? timesheetApproverSetupId,
        int organizationId,
        int? projectId,
        int approverId,
        bool isTimesheetApprover,
        bool isLeaveApprover,
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the project has a live setup with <c>IsTimesheetApprover</c> = 1
    /// other than <paramref name="excludedTimesheetApproverSetupId"/>.
    /// </summary>
    Task<bool> TimesheetApproverExistsForProjectAsync(
        int projectId,
        int excludedTimesheetApproverSetupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the organisation has a live setup with <c>IsLeaveApprover</c> =
    /// 1, whatever its project, other than
    /// <paramref name="excludedTimesheetApproverSetupId"/> - an organisation
    /// has one leave approver.
    /// </summary>
    Task<bool> LeaveApproverExistsForOrganizationAsync(
        int organizationId,
        int excludedTimesheetApproverSetupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live approver setup with this id, <b>tracked</b>;
    /// <see langword="null"/> when there is none.
    /// </summary>
    Task<TimesheetApproverSetup?> GetApproverSetupForUpdateAsync(
        int timesheetApproverSetupId,
        CancellationToken cancellationToken = default);

    /// <summary>Saves the changes made to a tracked approver setup.</summary>
    Task UpdateApproverSetupAsync(
        TimesheetApproverSetup setup,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the user is the live approver of the project with
    /// <c>IsTimesheetApprover</c> = 1 - the one user who may approve or reject
    /// its sheets.
    /// </summary>
    Task<bool> IsProjectTimesheetApproverAsync(
        int projectId,
        int approverId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one <c>dbo.Signup</c> user, untracked, or <see langword="null"/>
    /// when there is none. Deleted users are returned too; the caller decides.
    /// </summary>
    Task<Signup?> GetUserByIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live leave types of an organisation, via
    /// <c>spc_GetTimesheetLeaveSetup</c>. Empty when it has none.
    /// </summary>
    Task<IReadOnlyList<TimesheetLeaveSetupDetail>> GetLeaveSetupListAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a leave type (<paramref name="leaveTypeId"/> null) or edits one,
    /// via <c>spc_AddEditTimesheetLeaveSetup</c>, and returns the saved row.
    /// <see langword="null"/> when an edit names a leave type that does not
    /// exist, is deleted, or belongs to another organisation.
    /// </summary>
    Task<TimesheetLeaveSetupSaveDetail?> SaveLeaveSetupAsync(
        int? leaveTypeId,
        int organizationId,
        string leaveType,
        decimal noOfLeaveAllocated,
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live leave type with this id in this organisation,
    /// <b>tracked</b>; <see langword="null"/> when there is none.
    /// </summary>
    Task<TimesheetLeaveTypeSetup?> GetLeaveSetupForUpdateAsync(
        int leaveTypeId,
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>Saves the changes made to a tracked leave type.</summary>
    Task UpdateLeaveSetupAsync(
        TimesheetLeaveTypeSetup leaveType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live leave assignments of an organisation, a user or both, via
    /// <c>spc_GetTimesheetUserLeaveAssignment</c>. A null filter matches
    /// anything. Empty when none match.
    /// </summary>
    Task<IReadOnlyList<TimesheetUserLeaveAssignmentDetail>> GetUserLeaveAssignmentListAsync(
        int? organizationId,
        int? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a leave assignment via <c>spc_AddTimesheetUserLeaveAssignment</c>
    /// - the balance starts at the leave type's allocation - and returns the
    /// new row. <see langword="null"/> only if it cannot be read back.
    /// </summary>
    Task<TimesheetUserLeaveAssignmentAddDetail?> AddUserLeaveAssignmentAsync(
        int organizationId,
        int userId,
        int leaveTypeId,
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a leave assignment via <c>spc_EditTimesheetUserLeaveAssignment</c>
    /// - a null <paramref name="balanceLeave"/> keeps the balance - and returns
    /// the saved row. <see langword="null"/> when the assignment does not
    /// exist, is deleted, or belongs to another organisation.
    /// </summary>
    Task<TimesheetUserLeaveAssignmentEditDetail?> EditUserLeaveAssignmentAsync(
        int timesheetLeaveAssignmentUserId,
        int organizationId,
        int userId,
        int leaveTypeId,
        decimal? balanceLeave,
        int loginUserId,
        DateTime now,
        CancellationToken cancellationToken = default);

    /// <summary>Whether the organisation has a live leave type with this id.</summary>
    Task<bool> LeaveTypeExistsAsync(
        int leaveTypeId,
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the user holds the leave type in a live assignment other than
    /// <paramref name="excludedTimesheetLeaveAssignmentUserId"/>.
    /// </summary>
    Task<bool> UserLeaveAssignmentExistsAsync(
        int userId,
        int leaveTypeId,
        int excludedTimesheetLeaveAssignmentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live leave assignment with this id whose user belongs to the
    /// organisation, <b>tracked</b>; <see langword="null"/> when there is none.
    /// </summary>
    Task<TimesheetLeaveAssignmentUser?> GetUserLeaveAssignmentForUpdateAsync(
        int timesheetLeaveAssignmentUserId,
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>Saves the changes made to a tracked leave assignment.</summary>
    Task UpdateUserLeaveAssignmentAsync(
        TimesheetLeaveAssignmentUser assignment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The leave applications of an organisation, a user or both, via
    /// <c>spc_GetTimesheetUserLeaveApply</c>. A null filter matches anything.
    /// Empty when none match.
    /// </summary>
    Task<IReadOnlyList<TimesheetUserLeaveApplyDetail>> GetUserLeaveApplyListAsync(
        int? organizationId,
        int? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a leave application (<paramref name="timesheetLeaveUserApplyId"/>
    /// null) or edits one, via <c>spc_AddEditTimesheetUserLeaveApply</c>, and
    /// returns the saved row. <see langword="null"/> when an edit names an
    /// application that does not exist, belongs to another user or
    /// organisation, or is approved.
    /// </summary>
    Task<TimesheetUserLeaveApplySaveDetail?> SaveUserLeaveApplyAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the leave application with this id in the organisation,
    /// <b>tracked</b>; <see langword="null"/> when there is none.
    /// </summary>
    Task<TimesheetLeaveUserApply?> GetUserLeaveApplyForUpdateAsync(
        int timesheetLeaveUserApplyId,
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The <c>Value</c> of the <c>dbo.TimesheetLeaveStatus_Lookup</c> row with
    /// this <c>Text</c> - what <c>LeaveStatus</c> stores for that status -
    /// or <see langword="null"/> when the lookup has no such row.
    /// </summary>
    Task<int?> GetLeaveStatusValueAsync(
        string statusText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the user's live assignment of the leave type, <b>tracked</b>, so
    /// its balance can be changed; <see langword="null"/> when there is none.
    /// </summary>
    Task<TimesheetLeaveAssignmentUser?> GetUserLeaveAssignmentForBalanceAsync(
        int userId,
        int leaveTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tracked leave application and saves, in one
    /// <c>SaveChanges</c> - so a balance change made to a tracked assignment
    /// beforehand is written in the same transaction, or not at all.
    /// </summary>
    Task RemoveUserLeaveApplyAsync(
        TimesheetLeaveUserApply application,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the user is a leave approver of the organisation - the approver
    /// of a live <c>dbo.Timesheet_Approver_setup</c> row of it with
    /// <c>IsLeaveApprover</c> = 1, for any project.
    /// </summary>
    Task<bool> IsOrganizationLeaveApproverAsync(
        int organizationId,
        int approverId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the changes made to a tracked leave application - and to any
    /// tracked assignment whose balance was changed with it - in one
    /// <c>SaveChanges</c>: both or neither.
    /// </summary>
    Task UpdateUserLeaveApplyAsync(
        TimesheetLeaveUserApply application,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The leave applications with this <c>LeaveStatus</c> that overlap
    /// <paramref name="fromDate"/> to <paramref name="toDate"/>, of an
    /// organisation (<c>OrgID</c>), a user or both, untracked. A null filter
    /// matches anything.
    /// </summary>
    Task<IReadOnlyList<TimesheetLeaveUserApply>> GetLeaveApplyOverlappingAsync(
        int? organizationId,
        int? userId,
        int leaveStatus,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The time zone of each user's timesheet setup, keyed by user id. A user
    /// with no live setup is not in the result.
    /// </summary>
    Task<IReadOnlyDictionary<int, string?>> GetUserTimeZonesAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default);
}
