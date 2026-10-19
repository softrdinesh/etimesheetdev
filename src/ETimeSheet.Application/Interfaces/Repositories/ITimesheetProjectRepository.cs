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

    // =====================================================================
    //  PROJECT TIME LOG
    // =====================================================================

    /// <summary>
    /// Returns live project time log entries, filtered by any combination of
    /// user, project and sheet code, and optionally to a <c>LogDate</c> range.
    /// Ordered by <c>LogDate</c>, then <c>StartTime</c>, then id.
    /// <para>
    /// Composed in LINQ rather than read through a stored procedure: this
    /// module has none, so the endpoint works as soon as the table exists.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectTimeLogResponse>> GetProjectTimeLogListAsync(
        int? userId,
        int? projectId,
        string? sheetSubmissionCode,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one live entry, <b>tracked</b> for an edit or a soft delete, or
    /// <see langword="null"/> when it does not exist or is deleted already.
    /// </summary>
    Task<TimesheetProjectTimeLog?> GetProjectTimeLogForUpdateAsync(
        int timesheetProjectTimeLogId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds an entry and returns it with the id the database assigned.</summary>
    Task<TimesheetProjectTimeLog> AddProjectTimeLogAsync(
        TimesheetProjectTimeLog entry,
        CancellationToken cancellationToken = default);

    /// <summary>Saves a tracked entry - the edit and the soft delete both use this.</summary>
    Task UpdateProjectTimeLogAsync(
        TimesheetProjectTimeLog entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the user has a live assignment to the project in
    /// <c>dbo.Timesheet_Users_Project_setup</c>. The time log table names the
    /// user and the project directly, with no foreign key behind either, so
    /// this is the only thing standing between an entry and a pair that was
    /// never assigned.
    /// </summary>
    Task<bool> UserProjectAssignmentExistsAsync(
        int userId,
        int projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a live sheet carrying this code has been submitted and not
    /// rejected - in which case its entries may no longer be changed. A
    /// REJECTED sheet does not lock its entries: that is how a user corrects
    /// them and submits again.
    /// </summary>
    Task<bool> SheetIsSubmittedAsync(
        string sheetSubmissionCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The codes, among those given, whose sheet is submitted and not
    /// rejected. One round trip for a whole list, rather than one query per
    /// row.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetSubmittedSheetCodesAsync(
        IReadOnlyCollection<string> sheetSubmissionCodes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The code of the user's <b>open</b> sheet - the one their entries are
    /// still accumulating on - or <see langword="null"/> when they have none
    /// and the next entry starts a new sheet.
    /// <para>
    /// A sheet is open until it is submitted. A REJECTED sheet is open again,
    /// so corrections go back onto it rather than onto a new one.
    /// </para>
    /// <para>
    /// Deleted entries are included: a deleted entry still owns its code, and
    /// a sheet whose entries were all deleted is still the user's current one.
    /// </para>
    /// </summary>
    Task<string?> GetOpenSheetCodeForUserAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every distinct sheet code the user has ever had, deleted entries
    /// included, so a new code is never one they have used before. Small by
    /// construction: one per submission period.
    /// <para>
    /// Submissions are counted too, not only time log entries: an EMPTY sheet
    /// has no entries at all, so without them its code would be handed out a
    /// second time.
    /// </para>
    /// </summary>
    Task<IReadOnlyCollection<string>> GetSheetCodesForUserAsync(
        int userId,
        CancellationToken cancellationToken = default);

    // =====================================================================
    //  PROJECT SHEET SUBMISSION
    // =====================================================================

    /// <summary>
    /// The live submission for a sheet code, whatever its status, or
    /// <see langword="null"/> when the sheet has never been handed in.
    /// <b>Tracked</b>: a rejected sheet is revived through this.
    /// </summary>
    Task<TimesheetProjectSubmission?> GetSubmissionByCodeForUpdateAsync(
        string sheetSubmissionCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One live submission by its id, <b>tracked</b> for a review or a soft
    /// delete, or <see langword="null"/> when it does not exist or is deleted.
    /// </summary>
    Task<TimesheetProjectSubmission?> GetSubmissionForUpdateAsync(
        int timesheetProjectSubmissionId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a submission and returns it with the id the database assigned.</summary>
    Task<TimesheetProjectSubmission> AddSubmissionAsync(
        TimesheetProjectSubmission submission,
        CancellationToken cancellationToken = default);

    /// <summary>Saves a tracked submission - the review and the soft delete both use this.</summary>
    Task UpdateSubmissionAsync(
        TimesheetProjectSubmission submission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one row to the audit trail. Never updated, never deleted - a
    /// trail that can be rewritten is not a trail.
    /// </summary>
    Task AddSubmissionAuditAsync(
        TimesheetProjectSubmissionAuditLog audit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live entries on one sheet, with the user and project named, for
    /// totalling it and for showing it back. Ordered as the time log list is.
    /// </summary>
    Task<IReadOnlyList<TimesheetProjectTimeLogResponse>> GetEntriesForSheetAsync(
        string sheetSubmissionCode,
        CancellationToken cancellationToken = default);
}
