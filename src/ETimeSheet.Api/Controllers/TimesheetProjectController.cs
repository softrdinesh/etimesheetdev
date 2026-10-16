using ETimeSheet.Application.Models;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETimeSheet.Api.Controllers;

/// <summary>
/// HTTP surface for the TimesheetProject module - the projects of an
/// organisation, held in <c>dbo.TimesheetProject</c>.
/// <para>
/// Thin: bind, call <see cref="ITimesheetProjectService"/>, shape the response.
/// </para>
/// <para>
/// <b>Unauthenticated for now</b>, like the rest of the API.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class TimesheetProjectController : ControllerBase
{
    private readonly ITimesheetProjectService _timesheetProjectService;

    public TimesheetProjectController(ITimesheetProjectService timesheetProjectService)
    {
        _timesheetProjectService = timesheetProjectService;
    }

    /// <summary>
    /// Returns the projects of one organisation, from
    /// <c>dbo.TimesheetProject</c>, ordered by name, each with its status name.
    /// </summary>
    /// <param name="orgID">The organisation whose projects to list, as a route segment.</param>
    /// <response code="200">The projects. An organisation with none is an empty list, not a 404.</response>
    /// <response code="400">An organisation id that is not greater than zero.</response>
    [HttpGet("get-timesheet-project-by-orgID/{orgID:int}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetProjectResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetProjectByOrgId(
        // :int without :min(1): a 0 has to reach the service so it can answer
        // 400 saying what is wrong.
        [FromRoute] int orgID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetListByOrganizationIdAsync(orgID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Adds a project to an organisation, or updates one - <b>one endpoint for
    /// both</b>.
    /// <para>
    /// Send <c>projectId</c> as <c>0</c> (or leave it out) to add a project to
    /// <c>organizationId</c>. Send an existing project's <c>projectId</c> to
    /// update it; the project must belong to <c>organizationId</c>. An update
    /// overwrites every field, so send the whole project.
    /// </para>
    /// <para>
    /// No code is sent: an added project is given the next one in the
    /// sequence PR0001, PR0002 ..., and an edit never changes it.
    /// </para>
    /// <para>
    /// The response carries the saved project, including its <c>projectId</c>
    /// and <c>code</c>.
    /// </para>
    /// </summary>
    /// <response code="200">The saved project.</response>
    /// <response code="400">The payload failed validation - every failing field is listed.</response>
    /// <response code="404">A <c>projectId</c> was sent, but no project of that organisation has it.</response>
    [HttpPost("add-edit-timesheet-project")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetProjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEditTimesheetProject(
        [FromBody] TimesheetProjectSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Project saved."));
    }

    /// <summary>
    /// Deletes a project of an organisation <b>and its budget</b>. <b>A hard
    /// delete</b>: neither table has a soft-delete column, so the rows are
    /// physically removed, together - both or neither.
    /// <para>
    /// The project must belong to <c>organizationId</c>. A POST rather than an
    /// HTTP DELETE, matching <c>remove-timesheet-project-category</c>: every
    /// argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The project and its budget were removed.</response>
    /// <response code="400">A <c>projectId</c> or <c>organizationId</c> that is not greater than zero.</response>
    /// <response code="404">No project with that id belongs to that organisation; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-project")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetProject(
        [FromBody] TimesheetProjectDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Project removed."));
    }

    /// <summary>
    /// Returns the budget lines of one project, from
    /// <c>dbo.TimesheetProjectBudget</c>, ordered by period start, each with
    /// its category name.
    /// </summary>
    /// <param name="projectID">The project whose budget to list, as a route segment.</param>
    /// <response code="200">The budget lines. A project with none is an empty list, not a 404.</response>
    /// <response code="400">A project id that is not greater than zero.</response>
    [HttpGet("get-timesheet-project-budget-by-projectID/{projectID:int}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetProjectBudgetResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetProjectBudgetByProjectId(
        // :int without :min(1): a 0 has to reach the service so it can answer
        // 400 saying what is wrong.
        [FromRoute] int projectID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetBudgetListByProjectIdAsync(projectID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Saves the budget of a project - <b>one endpoint for both add and
    /// update</b>.
    /// <para>
    /// A project has exactly one budget, so no <c>budgetId</c> is sent:
    /// <c>projectId</c> decides. If the project already has a budget it is
    /// updated; if it has none, one is added. An update overwrites every field,
    /// so send the whole budget.
    /// </para>
    /// <para>
    /// No amount is sent: <c>allocatedAmount</c> is worked out as
    /// <c>allocatedHours</c> x <c>hourlyRate</c>.
    /// </para>
    /// </summary>
    /// <response code="200">The saved budget, with its <c>budgetId</c>.</response>
    /// <response code="400">The payload failed validation, or the category is not one of the project's organisation.</response>
    /// <response code="404">The project does not exist.</response>
    [HttpPost("add-edit-timesheet-project-budget")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetProjectBudgetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEditTimesheetProjectBudget(
        [FromBody] TimesheetProjectBudgetSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveBudgetAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Project budget saved."));
    }

    /// <summary>
    /// Deletes the budget of a project. Only <c>projectId</c> is sent - a
    /// project has one budget - and every budget row of the project is
    /// removed. <b>A hard delete</b>: the table has no soft-delete column, so
    /// the rows are physically removed.
    /// </summary>
    /// <response code="200">The budget was removed.</response>
    /// <response code="400">A <c>projectId</c> that is not greater than zero.</response>
    /// <response code="404">The project has no budget; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-project-budget")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetProjectBudget(
        [FromBody] TimesheetProjectBudgetDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteBudgetAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Project budget removed."));
    }

    /// <summary>
    /// Returns the project statuses of one organisation, from
    /// <c>dbo.TimesheetProjectStatus</c>, ordered by id - the order they were
    /// added in. Deleted statuses are not included. The <c>statusId</c> is what
    /// <c>add-edit-timesheet-project</c> takes as <c>status</c>.
    /// </summary>
    /// <param name="orgID">The organisation whose statuses to list, as a route segment.</param>
    /// <response code="200">The statuses. An organisation with none is an empty list, not a 404.</response>
    /// <response code="400">An organisation id that is not greater than zero.</response>
    [HttpGet("get-timesheet-project-status-by-orgID/{orgID:int}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetProjectStatusResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetProjectStatusByOrgId(
        // :int without :min(1): a 0 has to reach the service so it can answer
        // 400 saying what is wrong.
        [FromRoute] int orgID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetStatusListByOrganizationIdAsync(orgID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Adds a project status to an organisation, or edits one - <b>one
    /// endpoint for both</b>.
    /// <para>
    /// Send <c>statusId</c> as <c>0</c> (or leave it out) to add a status to
    /// <c>organizationId</c>. Send an existing status's <c>statusId</c> to
    /// edit it; the status must belong to <c>organizationId</c>. An edit
    /// overwrites <c>status</c> and <c>colorCode</c>, so send both.
    /// </para>
    /// </summary>
    /// <response code="200">The saved status, including its <c>statusId</c>.</response>
    /// <response code="400">The payload failed validation - every failing field is listed.</response>
    /// <response code="404">A <c>statusId</c> was sent, but no live status of that organisation has it.</response>
    [HttpPost("add-edit-timesheet-project-status")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetProjectStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEditTimesheetProjectStatus(
        [FromBody] TimesheetProjectStatusSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveStatusAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Project status saved."));
    }

    /// <summary>
    /// Soft-deletes a project status. The row is kept, stamped with
    /// <c>DeletedDate</c> and <c>Deletedby</c>; nothing is physically deleted.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching
    /// <c>remove-timesheet-project</c>: every argument in this API travels in
    /// the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The status was removed.</response>
    /// <response code="400">A <c>statusId</c> or <c>deletedBy</c> that is not greater than zero.</response>
    /// <response code="404">No live status has that id; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-project-status")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetProjectStatus(
        [FromBody] TimesheetProjectStatusDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteStatusAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Project status removed."));
    }

    /// <summary>
    /// Returns the budget lines of every project of one organisation, from
    /// <c>dbo.TimesheetProjectBudget</c>, ordered by project name, then period
    /// start. Each line carries its project's id, code and name, and its
    /// category name. A project with no budget is not listed.
    /// </summary>
    /// <param name="orgID">The organisation whose project budgets to list, as a route segment.</param>
    /// <response code="200">The budget lines. An organisation with none is an empty list, not a 404.</response>
    /// <response code="400">An organisation id that is not greater than zero.</response>
    [HttpGet("get-timesheet-project-budget-by-orgID/{orgID:int}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetProjectBudgetByOrgResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetProjectBudgetByOrgId(
        // :int without :min(1): a 0 has to reach the service so it can answer
        // 400 saying what is wrong.
        [FromRoute] int orgID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetBudgetListByOrganizationIdAsync(orgID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Signs a user up and puts them on a project of their organisation,
    /// through <c>dbo.spc_Signup_timesheet_user</c>.
    /// <para>
    /// An <c>email</c> that is not yet a user creates the user, their login
    /// row and the project assignment. An <c>email</c> that is already a live
    /// user of <c>organizationId</c> only adds the project assignment; the user
    /// is not changed, so <c>fullName</c>, <c>countryId</c> and <c>address</c>
    /// are ignored. <c>isNewUser</c> in the response says which happened.
    /// </para>
    /// </summary>
    /// <response code="200">The user, with their <c>userId</c>.</response>
    /// <response code="400">The payload failed validation, or names an organisation, country, project or role that does not exist - every failure is listed.</response>
    /// <response code="409">The email belongs to a removed user, or to a user of another organisation.</response>
    [HttpPost("create-timesheet-user")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetUserCreateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTimesheetUser(
        [FromBody] TimesheetUserCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.CreateTimesheetUserAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, result.IsNewUser ? "User created." : "User added to the project."));
    }

    /// <summary>
    /// Edits one project assignment of a user, identified by
    /// <c>timesheetUserProjectSetupId</c>.
    /// <para>
    /// <c>fullName</c> and <c>email</c> are saved to the user in
    /// <c>dbo.Signup</c> when they change - and so change for every project the
    /// user is on. Every other field is saved to the assignment in
    /// <c>dbo.Timesheet_Users_Project_setup</c>. Both are written together, or
    /// neither is.
    /// </para>
    /// <para>
    /// An edit overwrites every field, so send the whole assignment.
    /// </para>
    /// </summary>
    /// <response code="200">The saved assignment, with the user's name and email.</response>
    /// <response code="400">The payload failed validation, or names a project or role that does not exist in the organisation - every failure is listed.</response>
    /// <response code="404">No live assignment has that id, or its user is removed or belongs to another organisation.</response>
    /// <response code="409">The email is used by another user, or the user is already on the project through another assignment.</response>
    [HttpPost("edit-timesheet-user")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditTimesheetUser(
        [FromBody] TimesheetUserUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.UpdateTimesheetUserAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Timesheet user saved."));
    }

    /// <summary>
    /// Takes a user off a project, by <c>userId</c> and <c>projectId</c>. A
    /// soft delete: the user's live assignment to the project in
    /// <c>dbo.Timesheet_Users_Project_setup</c> is kept, stamped with
    /// <c>IsDelete</c> = 1, <c>DeletedDate</c> and <c>DeletedBy</c> - every
    /// live row, if duplicates exist. The user in <c>dbo.Signup</c> and their
    /// other projects are not touched.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching the other removes here:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The user was taken off the project.</response>
    /// <response code="400">A <c>userId</c>, <c>projectId</c> or <c>deletedBy</c> that is not greater than zero.</response>
    /// <response code="404">The user is not on the project; removing twice is not a silent success.</response>
    [HttpPost("remove-timesheet-user")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetUser(
        [FromBody] TimesheetUserDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteTimesheetUserAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Timesheet user removed."));
    }
}
