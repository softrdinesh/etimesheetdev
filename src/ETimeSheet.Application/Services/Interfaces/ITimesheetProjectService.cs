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
    /// Physically deletes the budget of a project - every budget row it has,
    /// duplicates included. The table has no soft-delete column, so nothing of
    /// the rows is kept.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// A <c>ProjectId</c> that is not positive.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// The project has no budget - including one removed already, so removing
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
}
