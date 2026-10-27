using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Services.Interfaces;

/// <summary>
/// Business operations for the TimesheetProject module - the projects of an
/// organisation, held in <c>dbo.TimesheetProject</c>.
/// </summary>
public interface ITimesheetProjectService
{
    /// <summary>
    /// Returns the projects of one organisation, ordered by name, via
    /// <c>spc_GetTimesheetProject</c>. An organisation with no projects is an
    /// empty list, not a 404.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// <paramref name="organizationId"/> is not a positive id.
    /// </exception>
    Task<IReadOnlyList<TimesheetProjectResponse>> GetListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a project to an organisation, or updates one - <b>one method for
    /// both</b>, via <c>spc_AddEditTimesheetProject</c>. A
    /// <see cref="TimesheetProjectSaveRequest.ProjectId"/> absent or <c>0</c>
    /// adds; any greater value updates that project.
    /// </summary>
    /// <returns>The saved project, including its <c>ProjectId</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule - every failing field is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// A <c>ProjectId</c> was sent, but no project of this organisation has it.
    /// </exception>
    Task<TimesheetProjectResponse> SaveAsync(
        TimesheetProjectSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically deletes a project of an organisation <b>and its budget</b>,
    /// in one transaction. Neither table has a soft-delete column, so nothing
    /// of either is kept.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>ProjectId</c> or <c>OrganizationId</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No project with that id belongs to that organisation - including one
    /// removed already, so removing twice is a 404.
    /// </exception>
    Task DeleteAsync(
        TimesheetProjectDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the budget lines of one project, ordered by period start, via
    /// <c>spc_GetTimesheetProjectBudget</c>. A project with no lines - or no
    /// such project - is an empty list, not a 404.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// <paramref name="projectId"/> is not a positive id.
    /// </exception>
    Task<IReadOnlyList<TimesheetProjectBudgetResponse>> GetBudgetListByProjectIdAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the one budget of a project - <b>one method for both add and
    /// update</b>, via <c>spc_AddEditTimesheetProjectBudget</c>. The project
    /// decides: its existing budget is updated, or one is added if it has
    /// none. The amount is worked out as hours x rate.
    /// </summary>
    /// <returns>The saved budget, including its <c>BudgetId</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, or the category is not a live category of the
    /// project's organisation.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// The project does not exist.
    /// </exception>
    Task<TimesheetProjectBudgetResponse> SaveBudgetAsync(
        TimesheetProjectBudgetSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically deletes one budget row, named by its <c>BudgetId</c>. The
    /// table has no soft-delete column, so nothing of the row is kept.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>BudgetId</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No budget has this id - including one removed already, so removing
    /// twice is a 404.
    /// </exception>
    Task DeleteBudgetAsync(
        TimesheetProjectBudgetDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live project statuses of one organisation, ordered by id,
    /// via <c>spc_GetTimesheetProjectStatus</c>. An organisation with no
    /// statuses is an empty list, not a 404.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// <paramref name="organizationId"/> is not a positive id.
    /// </exception>
    Task<IReadOnlyList<TimesheetProjectStatusResponse>> GetStatusListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a project status to an organisation, or edits one - <b>one method
    /// for both</b>, via <c>spc_AddEditTimesheetProjectStatus</c>. A
    /// <see cref="TimesheetProjectStatusSaveRequest.StatusId"/> absent or
    /// <c>0</c> adds; any greater value edits that status.
    /// </summary>
    /// <returns>The saved status, including its <c>StatusId</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule - every failing field is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// A <c>StatusId</c> was sent, but no live status of this organisation has it.
    /// </exception>
    Task<TimesheetProjectStatusResponse> SaveStatusAsync(
        TimesheetProjectStatusSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a project status: the row stays, stamped with
    /// <c>DeletedDate</c> and <c>Deletedby</c>.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>StatusId</c> or <c>DeletedBy</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live status has that id - including one removed already, so
    /// removing twice is a 404.
    /// </exception>
    Task DeleteStatusAsync(
        TimesheetProjectStatusDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the budget lines of every project of one organisation, ordered
    /// by project name, then period start, via
    /// <c>spc_GetTimesheetProjectBudgetByOrgID</c>. An organisation with no
    /// budget lines is an empty list, not a 404.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// <paramref name="organizationId"/> is not a positive id.
    /// </exception>
    Task<IReadOnlyList<TimesheetProjectBudgetByOrgResponse>> GetBudgetListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a user up and puts them on a project of their organisation, via
    /// <c>spc_Signup_timesheet_user</c>. An email that is already a live user
    /// of the same organisation only gets the project assignment; the user
    /// itself is not changed.
    /// </summary>
    /// <returns>The user as <c>dbo.Signup</c> holds it after the save.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, or the organisation, country, project or role
    /// it names does not exist - every failure is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The email belongs to a removed user, or to a user of another organisation.
    /// </exception>
    Task<TimesheetUserCreateResponse> CreateTimesheetUserAsync(
        TimesheetUserCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits one project assignment of a user through EF Core. The name,
    /// email, country and address are saved to <c>dbo.Signup</c> when they
    /// change; every other field to
    /// <c>dbo.Timesheet_Users_Project_setup</c>. Both in one transaction.
    /// </summary>
    /// <returns>
    /// The assignment and the user's name, email, country and address, as
    /// saved.
    /// </returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, or the country, project or role it names does
    /// not exist - every failure is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live assignment has that id, or its user is removed or belongs to
    /// another organisation.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The email belongs to another user, or the user is already on the project
    /// through another assignment.
    /// </exception>
    Task<TimesheetUserResponse> UpdateTimesheetUserAsync(
        TimesheetUserUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a user off a project: soft-deletes every live assignment of the
    /// user to the project - normally one. The rows stay, stamped with
    /// <c>IsDelete</c> = 1, <c>DeletedDate</c> and <c>DeletedBy</c>. The user
    /// in <c>dbo.Signup</c> is not touched.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>UserId</c>, <c>ProjectId</c> or <c>DeletedBy</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// The user has no live assignment to the project - including one removed
    /// already, so removing twice is a 404.
    /// </exception>
    Task DeleteTimesheetUserAsync(
        TimesheetUserDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the timesheet users of one organisation - every live project
    /// assignment of every live user of it - via
    /// <c>spc_GetTimesheetUserListByOrgID</c>, ordered by user name, then
    /// project name. An organisation with none is an empty list, not a 404.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// <paramref name="organizationId"/> is not a positive id.
    /// </exception>
    Task<IReadOnlyList<TimesheetUserListResponse>> GetTimesheetUserListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one timesheet user via <c>spc_GetTimesheetUserDetailsByUserID</c>:
    /// their details, and every live project assignment with its project,
    /// client, expected and logged hours, and the hours logged in the
    /// project's current timesheet period - worked out on the user's own date,
    /// in the time zone of their timesheet setup.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// <paramref name="userId"/> is not a positive id.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live user has that id.
    /// </exception>
    Task<TimesheetUserDetailsResponse> GetTimesheetUserDetailsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live holidays of an organisation, a project or both,
    /// ordered by date, via <c>spc_GetTimesheetHolidaySetup</c>. A filter that
    /// is null or <c>0</c> is not applied; at least one must be given. No
    /// matching holidays is an empty list, not a 404.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Neither filter is given, or one is negative.
    /// </exception>
    Task<IReadOnlyList<TimesheetHolidaySetupResponse>> GetHolidaySetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a holiday to an organisation, or edits one - <b>one method for
    /// both</b>, via <c>spc_AddEditTimesheetHolidaySetup</c>. A
    /// <see cref="TimesheetHolidaySetupSaveRequest.TimesheetHolidaySetupId"/>
    /// absent or <c>0</c> adds; any greater value edits that holiday.
    /// </summary>
    /// <returns>The saved holiday, including its <c>TimesheetHolidaySetupId</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, or names a country, client or project that
    /// does not exist - every failure is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// An id was sent, but no live holiday of this organisation has it.
    /// </exception>
    Task<TimesheetHolidaySetupResponse> SaveHolidaySetupAsync(
        TimesheetHolidaySetupSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a holiday of an organisation: <c>IsDelete</c> is set to 1
    /// and the row is kept.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>TimesheetHolidaySetupId</c> or <c>OrganizationId</c> that is not
    /// positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live holiday with that id belongs to that organisation - including
    /// one removed already, so removing twice is a 404.
    /// </exception>
    Task DeleteHolidaySetupAsync(
        TimesheetHolidaySetupDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live weekend setups of an organisation, a project or both,
    /// via <c>spc_GetTimesheetWeekendSetup</c>. A filter that is null or
    /// <c>0</c> is not applied; at least one must be given. No matching setups
    /// is an empty list, not a 404.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Neither filter is given, or one is negative.
    /// </exception>
    Task<IReadOnlyList<TimesheetWeekendSetupResponse>> GetWeekendSetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the weekend setup of a project, or edits one - <b>one method for
    /// both</b>, via <c>spc_AddEditTimesheetWeekendSetup</c>. A
    /// <see cref="TimesheetWeekendSetupSaveRequest.TimeSheetWeekendSetupId"/>
    /// absent or <c>0</c> adds; any greater value edits that setup.
    /// </summary>
    /// <returns>The saved setup, including its <c>TimeSheetWeekendSetupId</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, or names a client or project that does not
    /// exist in the organisation - every failure is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// An id was sent, but no live setup of this organisation has it.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The project already has another live weekend setup.
    /// </exception>
    Task<TimesheetWeekendSetupResponse> SaveWeekendSetupAsync(
        TimesheetWeekendSetupSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a weekend setup: <c>IsDelete</c>, <c>DeletedDate</c> and
    /// <c>DeletedBy</c> are stamped and the row is kept.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>TimeSheetWeekendSetupId</c> or <c>DeletedBy</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live setup has that id - including one removed already, so removing
    /// twice is a 404.
    /// </exception>
    Task DeleteWeekendSetupAsync(
        TimesheetWeekendSetupDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the module assignments of live users of an organisation, a user
    /// or both, via <c>spc_GetUserAccessSetup</c>. A filter that is
    /// null or <c>0</c> is not applied; at least one must be given. No
    /// matching assignments is an empty list, not a 404.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Neither filter is given, or one is negative.
    /// </exception>
    Task<IReadOnlyList<TimesheetRoleAssignmentResponse>> GetRoleAssignmentListAsync(
        int? organizationId,
        int? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the module assignment of a user, or edits one - <b>one method for
    /// both</b>, via <c>spc_AddEditUserAccessSetup</c>. An
    /// <see cref="TimesheetRoleAssignmentSaveRequest.AssignmentId"/> absent or
    /// <c>0</c> adds; any greater value edits that assignment.
    /// </summary>
    /// <returns>The saved assignment, including its <c>AssignmentId</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, or the user is not a live user of the
    /// organisation - every failure is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// An id was sent, but no assignment has it.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The user already has another assignment.
    /// </exception>
    Task<TimesheetRoleAssignmentResponse> SaveRoleAssignmentAsync(
        TimesheetRoleAssignmentSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically deletes a module assignment. The table has no soft-delete
    /// column, so nothing of the row is kept.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// An <c>AssignmentId</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No assignment has that id - including one removed already, so removing
    /// twice is a 404.
    /// </exception>
    Task DeleteRoleAssignmentAsync(
        TimesheetRoleAssignmentDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live time log days of a user - one per project per day -
    /// optionally narrowed to one project and a date range, ordered by date,
    /// then project. No matching days is an empty list, not a 404.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// <paramref name="userId"/> is not positive, <paramref name="projectId"/>
    /// is negative, or <paramref name="fromDate"/> is after <paramref name="toDate"/>.
    /// </exception>
    Task<IReadOnlyList<TimesheetProjectTimeLogResponse>> GetProjectTimeLogListAsync(
        int userId,
        int? projectId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or edits an entry of hours on a project for one day.
    /// <c>TimesheetProjectTimeLogId</c> of 0 adds a new entry - a day may hold
    /// several - and an id greater than 0 edits that one, replacing its hours
    /// rather than adding to them. The hours are not capped. A new entry takes
    /// the sheet code of its submission period - its sheet's, or the one
    /// already on the user's days on the project in that period, deleted ones
    /// included, or else the next code; an entry moved to another period takes
    /// that period's. No submission is opened. A period that is submitted or
    /// approved is closed; a rejected one is open, and its sheet is left as it
    /// is.
    /// </summary>
    /// <returns>The entry as saved, with its code and period.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, the project does not exist, the user is not
    /// assigned to it, or the date is outside the project's dates.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// An id greater than 0 that no live entry has.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.BusinessException">
    /// The project has no start date, submission frequency or organisation,
    /// the day is approved leave or a holiday, the entry belongs to another
    /// user or project, or a period involved is submitted or approved.
    /// </exception>
    Task<TimesheetProjectTimeLogResponse> SaveProjectTimeLogAsync(
        TimesheetProjectTimeLogSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a time log day: <c>IsDelete</c>, <c>DeletedDate</c> and
    /// <c>DeletedBy</c> are stamped and the row is kept.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>TimesheetProjectTimeLogId</c> or <c>UserId</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live entry has that id - including one removed already, so removing
    /// twice is a 404.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.BusinessException">
    /// The entry belongs to another user, or its period is submitted or approved.
    /// </exception>
    Task DeleteProjectTimeLogAsync(
        TimesheetProjectTimeLogDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the project sheets matching every filter given - a user, a
    /// project, an organisation, or any combination; a filter that is null or
    /// <c>0</c> is not applied, and at least one is required. Ordered by
    /// period, then project name, then user. Only submitted periods are
    /// listed - a sheet has no row until it is submitted - each with its time
    /// from <c>dbo.TimesheetProjectTimeLog</c>.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// No filter is given, or one is negative.
    /// </exception>
    Task<IReadOnlyList<TimesheetProjectSheetResponse>> GetProjectSheetListAsync(
        int? userId,
        int? projectId,
        int? organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a user's timesheet for one period of a project, named by its
    /// code or by a day in it. The period comes from the project's start date
    /// and frequency, and may be submitted on its last day or after, in the
    /// user's time zone. The first submit creates the sheet's row, already
    /// Submitted, with the period's time-log code (the next code for a blank
    /// sheet); a rejected sheet is resubmitted in its row. Totals are taken
    /// from the time log, and the change is recorded in
    /// <c>dbo.TimesheetProjectSubmissionAuditLog</c>.
    /// </summary>
    /// <returns>The submitted sheet.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// No user, project, code or date; the project does not exist; the code is
    /// not this user's on this project; the date is outside the project or not
    /// in the code's period.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.BusinessException">
    /// The coded sheet is another user's or project's; the project has no start
    /// date, frequency or organisation; the period has not reached its last
    /// day; or the user is not assigned to the project.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The period is already submitted or approved.
    /// </exception>
    Task<TimesheetProjectSheetResponse> SubmitProjectSheetAsync(
        TimesheetProjectSheetSubmitRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves or rejects a submitted project sheet, and records the change
    /// in <c>dbo.TimesheetProjectSubmissionAuditLog</c>. A rejected sheet is
    /// open again: its time can be changed and it can be resubmitted.
    /// </summary>
    /// <returns>The sheet after the change.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule - every failing field is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live sheet has that id.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ForbiddenException">
    /// <c>AddedBy</c> is not the live approver of the sheet's project with
    /// <c>IsTimesheetApprover</c> = 1, in <c>dbo.Timesheet_Approver_setup</c>.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The sheet is not submitted.
    /// </exception>
    Task<TimesheetProjectSheetResponse> UpdateProjectSheetStatusAsync(
        TimesheetProjectSheetStatusUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the live approver setups of an organisation, a project or both,
    /// via <c>spc_GetTimesheetApproverSetup</c>. A filter that is null or
    /// <c>0</c> is not applied; at least one must be given. No matching setups
    /// is an empty list, not a 404.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Neither filter is given, or one is negative.
    /// </exception>
    Task<IReadOnlyList<TimesheetApproverSetupResponse>> GetApproverSetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the approver of a project, or edits one - <b>one method for
    /// both</b>, via <c>spc_AddEditTimesheetApproverSetup</c>. A
    /// <see cref="TimesheetApproverSetupSaveRequest.TimesheetApproverSetupId"/>
    /// absent or <c>0</c> adds; any greater value edits that setup.
    /// </summary>
    /// <returns>The saved setup, including its <c>TimesheetApproverSetupId</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule (including neither approver flag true),
    /// names a project that is not in the organisation, or an approver who is
    /// not a live user of it - every
    /// failure is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// An id was sent, but no live setup of this organisation has it.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The project already has another live timesheet approver or leave
    /// approver, for an area this setup claims.
    /// </exception>
    Task<TimesheetApproverSetupResponse> SaveApproverSetupAsync(
        TimesheetApproverSetupSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes an approver setup: <c>IsDelete</c>, <c>DeletedDate</c> and
    /// <c>DeletedBy</c> are stamped and the row is kept.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>TimesheetApproverSetupId</c> or <c>DeletedBy</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live setup has that id - including one removed already, so removing
    /// twice is a 404.
    /// </exception>
    Task DeleteApproverSetupAsync(
        TimesheetApproverSetupDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live leave types of an organisation, via
    /// <c>spc_GetTimesheetLeaveSetup</c>, ordered by name.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// <paramref name="organizationId"/> is missing or not positive.
    /// </exception>
    Task<IReadOnlyList<TimesheetLeaveSetupResponse>> GetLeaveSetupListAsync(
        int? organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a leave type to an organisation, or edits one - <b>one method for
    /// both</b>, via <c>spc_AddEditTimesheetLeaveSetup</c>. A
    /// <see cref="TimesheetLeaveSetupSaveRequest.LeaveTypeId"/> absent or
    /// <c>0</c> adds; any greater value edits that leave type.
    /// </summary>
    /// <returns>The saved leave type, including its <c>LeaveTypeId</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule - every failure is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// An id was sent, but no live leave type of this organisation has it.
    /// </exception>
    Task<TimesheetLeaveSetupResponse> SaveLeaveSetupAsync(
        TimesheetLeaveSetupSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a leave type: <c>IsDelete</c>, <c>DeletedDate</c> and
    /// <c>DeletedBy</c> are stamped and the row is kept.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>LeaveTypeId</c>, <c>OrganizationId</c> or <c>DeletedBy</c> that is
    /// not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live leave type of the organisation has that id - including one
    /// removed already, so removing twice is a 404.
    /// </exception>
    Task DeleteLeaveSetupAsync(
        TimesheetLeaveSetupDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live leave assignments of an organisation, a user or both, via
    /// <c>spc_GetTimesheetUserLeaveAssignment</c>. A filter that is null or
    /// <c>0</c> is not applied, but at least one is required.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A negative filter, or neither filter sent.
    /// </exception>
    Task<IReadOnlyList<TimesheetUserLeaveAssignmentResponse>> GetUserLeaveAssignmentListAsync(
        int? organizationId,
        int? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a leave type to a user, via
    /// <c>spc_AddTimesheetUserLeaveAssignment</c>. The balance starts at the
    /// leave type's <c>NoofLeaveAllocated</c>.
    /// </summary>
    /// <returns>The new assignment, including its <c>TimesheetLeaveAssignmentUserId</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, names a user who is not a live user of the
    /// organisation, or a leave type that is not a live leave type of it -
    /// every failure is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The user already holds this leave type.
    /// </exception>
    Task<TimesheetUserLeaveAssignmentResponse> AddUserLeaveAssignmentAsync(
        TimesheetUserLeaveAssignmentAddRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a leave assignment, via
    /// <c>spc_EditTimesheetUserLeaveAssignment</c>. A null
    /// <see cref="TimesheetUserLeaveAssignmentEditRequest.BalanceLeave"/> keeps
    /// the balance as it is.
    /// </summary>
    /// <returns>The edited assignment.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, names a user who is not a live user of the
    /// organisation, or a leave type that is not a live leave type of it -
    /// every failure is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live assignment of this organisation has the id.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The user already holds this leave type in another live assignment.
    /// </exception>
    Task<TimesheetUserLeaveAssignmentResponse> EditUserLeaveAssignmentAsync(
        TimesheetUserLeaveAssignmentEditRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a leave assignment: <c>IsDelete</c>, <c>DeletedDate</c> and
    /// <c>DeletedBy</c> are stamped and the row is kept.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>TimesheetLeaveAssignmentUserId</c>, <c>OrganizationId</c> or
    /// <c>DeletedBy</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live assignment of the organisation has that id - including one
    /// removed already, so removing twice is a 404.
    /// </exception>
    Task DeleteUserLeaveAssignmentAsync(
        TimesheetUserLeaveAssignmentDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The leave applications of an organisation, a user or both, via
    /// <c>spc_GetTimesheetUserLeaveApply</c>, newest first. A filter that is
    /// null or <c>0</c> is not applied, but at least one is required.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A negative filter, or neither filter sent.
    /// </exception>
    Task<IReadOnlyList<TimesheetUserLeaveApplyResponse>> GetUserLeaveApplyListAsync(
        int? organizationId,
        int? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies for leave, or edits an application - <b>one method for both</b>,
    /// via <c>spc_AddEditTimesheetUserLeaveApply</c>. A new application is
    /// Submitted; an edit puts it back to Submitted. An approved application
    /// cannot be edited.
    /// </summary>
    /// <returns>The saved application, including its <c>TimesheetLeaveUserApplyId</c> and <c>LeaveId</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, names a user who is not a live user of the
    /// organisation, a leave type that is not a live leave type of it, or a
    /// leave type the user is not assigned - every failure is reported
    /// together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// An id was sent, but no application of this user in this organisation
    /// has it.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.BusinessException">
    /// The application is approved.
    /// </exception>
    Task<TimesheetUserLeaveApplyResponse> SaveUserLeaveApplyAsync(
        TimesheetUserLeaveApplySaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a leave application. If it was approved, its days are given back
    /// to the user's <c>BalanceLeave</c> for that leave type, in the same
    /// transaction; otherwise the balance is not touched.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>TimesheetLeaveUserApplyId</c>, <c>OrganizationId</c> or
    /// <c>DeletedBy</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No application of the organisation has that id - including one removed
    /// already, so removing twice is a 404.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.BusinessException">
    /// Giving the days back would take the balance past 99.99, which the column
    /// cannot hold.
    /// </exception>
    Task DeleteUserLeaveApplyAsync(
        TimesheetUserLeaveApplyDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves or rejects a submitted leave application. Approving takes its
    /// days from the user's <c>BalanceLeave</c> for that leave type, in the
    /// same transaction; rejecting leaves the balance alone.
    /// </summary>
    /// <returns>The application after the change.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A field fails its rule, the status is not Approved or Rejected, or a
    /// rejection has no comment - every failure is reported together.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ForbiddenException">
    /// <c>AddedBy</c> is not a live user and a leave approver of the
    /// organisation.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No application of the organisation has that id.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The application is not submitted - already approved or rejected.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.BusinessException">
    /// Approving would take more days than the user's balance holds, or the
    /// user is no longer assigned the leave type.
    /// </exception>
    Task<TimesheetUserLeaveApplyResponse> UpdateUserLeaveApplyStatusAsync(
        TimesheetUserLeaveApplyStatusUpdateRequest request,
        CancellationToken cancellationToken = default);
}
