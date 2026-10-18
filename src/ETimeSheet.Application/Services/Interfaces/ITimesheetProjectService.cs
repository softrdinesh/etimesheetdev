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
    /// Edits one project assignment of a user through EF Core. The name and
    /// email are saved to <c>dbo.Signup</c> when they change; every other field
    /// to <c>dbo.Timesheet_Users_Project_setup</c>. Both in one transaction.
    /// </summary>
    /// <returns>The assignment and the user's name and email, as saved.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// Any field fails its rule, or the project or role it names does not exist
    /// in the organisation - every failure is reported together.
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
}
