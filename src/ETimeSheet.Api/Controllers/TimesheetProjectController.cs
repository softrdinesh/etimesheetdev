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
    /// Returns every project status from the <c>dbo.TimesheetProjectStatus</c>
    /// lookup - Open, Closed, Pending - ordered by id. The <c>statusId</c> is
    /// what <c>add-edit-timesheet-project</c> takes as <c>status</c>.
    /// </summary>
    /// <response code="200">The statuses.</response>
    [HttpGet("get-timesheet-project-status-list")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetProjectStatusResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimesheetProjectStatusList(
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetStatusListAsync(cancellationToken);
        return Ok(ApiResponse.Ok(result));
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
}
