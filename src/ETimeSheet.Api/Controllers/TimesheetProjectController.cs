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

    #region Project

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

    #endregion

    #region Project Budget

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
    /// Deletes one budget, named by its <c>budgetId</c>. <b>A hard delete</b>:
    /// the table has no soft-delete column, so the row is physically removed.
    /// </summary>
    /// <response code="200">The budget was removed.</response>
    /// <response code="400">A <c>budgetId</c> that is not greater than zero.</response>
    /// <response code="404">No budget has this id; removing one twice is not a silent success.</response>
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

    #endregion

    #region Project Status

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

    #endregion

    #region Timesheet User

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

    /// <summary>
    /// Returns the timesheet users of one organisation, through
    /// <c>dbo.spc_GetTimesheetUserListByOrgID</c> - one row per live project
    /// assignment of a live user, with the user's name and email, the
    /// project's code and name, and the project role's name. Ordered by user
    /// name, then project name. A user with no live assignment is not listed.
    /// </summary>
    /// <param name="orgID">The organisation whose users to list, as a route segment.</param>
    /// <response code="200">The users' assignments. An organisation with none is an empty list, not a 404.</response>
    /// <response code="400">An organisation id that is not greater than zero.</response>
    [HttpGet("get-timesheet-user-by-orgID/{orgID:int}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetUserListResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetUserByOrgId(
        // :int without :min(1): a 0 has to reach the service so it can answer
        // 400 saying what is wrong.
        [FromRoute] int orgID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetTimesheetUserListByOrganizationIdAsync(orgID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    #endregion

    #region Holiday Setup

    /// <summary>
    /// Returns the holidays in <c>dbo.TimesheetHolidaysetup</c>, ordered by
    /// date, each with its country, client and project names.
    /// <para>
    /// Filter by organisation, by project, or both. A filter left out or sent
    /// as <c>0</c> is not applied: <c>orgID</c> alone returns every holiday of
    /// the organisation, <c>projectID</c> alone every holiday of the project.
    /// At least one is required. Deleted holidays are not included.
    /// </para>
    /// </summary>
    /// <param name="orgID">The organisation whose holidays to list. Optional when <c>projectID</c> is sent.</param>
    /// <param name="projectID">The project whose holidays to list. Optional when <c>orgID</c> is sent.</param>
    /// <response code="200">The holidays. None matching is an empty list, not a 404.</response>
    /// <response code="400">Neither <c>orgID</c> nor <c>projectID</c> was sent, or one is negative.</response>
    [HttpGet("get-timesheet-holiday-setup")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetHolidaySetupResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetHolidaySetup(
        // Query string rather than route segments: either may be left out.
        [FromQuery] int? orgID,
        [FromQuery] int? projectID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetHolidaySetupListAsync(orgID, projectID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Adds a holiday to an organisation, or edits one - <b>one endpoint for
    /// both</b>.
    /// <para>
    /// Send <c>timesheetHolidaySetupId</c> as <c>0</c> (or leave it out) to
    /// add a holiday to <c>organizationId</c>. Send an existing holiday's
    /// <c>timesheetHolidaySetupId</c> to edit it; the holiday must belong to
    /// <c>organizationId</c>. An edit overwrites <c>holidayDate</c>,
    /// <c>description</c>, <c>countryId</c>, <c>clientId</c> and
    /// <c>projectId</c>, so send them all.
    /// </para>
    /// </summary>
    /// <response code="200">The saved holiday, including its <c>timesheetHolidaySetupId</c>.</response>
    /// <response code="400">The payload failed validation, or names a country, client or project that does not exist - every failure is listed.</response>
    /// <response code="404">A <c>timesheetHolidaySetupId</c> was sent, but no live holiday of that organisation has it.</response>
    [HttpPost("add-edit-timesheet-holiday-setup")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetHolidaySetupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEditTimesheetHolidaySetup(
        [FromBody] TimesheetHolidaySetupSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveHolidaySetupAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Holiday saved."));
    }

    /// <summary>
    /// Soft-deletes a holiday of an organisation. The row is kept, with
    /// <c>IsDelete</c> set to 1; nothing is physically deleted.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching the other removes here:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The holiday was removed.</response>
    /// <response code="400">A <c>timesheetHolidaySetupId</c> or <c>organizationId</c> that is not greater than zero.</response>
    /// <response code="404">No live holiday with that id belongs to that organisation; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-holiday-setup")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetHolidaySetup(
        [FromBody] TimesheetHolidaySetupDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteHolidaySetupAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Holiday removed."));
    }

    #endregion

    #region Weekend Setup

    /// <summary>
    /// Returns the weekend setups in <c>dbo.TimeSheet_Weekend_setup</c> - which
    /// days of the week are weekend days for each project - with the client
    /// and project names.
    /// <para>
    /// Filter by organisation, by project, or both. A filter left out or sent
    /// as <c>0</c> is not applied: <c>orgID</c> alone returns every setup of
    /// the organisation, <c>projectID</c> alone the project's setup (a project
    /// has at most one). At least one is required. Deleted setups are not
    /// included.
    /// </para>
    /// </summary>
    /// <param name="orgID">The organisation whose setups to list. Optional when <c>projectID</c> is sent.</param>
    /// <param name="projectID">The project whose setup to return. Optional when <c>orgID</c> is sent.</param>
    /// <response code="200">The setups. None matching is an empty list, not a 404.</response>
    /// <response code="400">Neither <c>orgID</c> nor <c>projectID</c> was sent, or one is negative.</response>
    [HttpGet("get-timesheet-weekend-setup")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetWeekendSetupResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetWeekendSetup(
        // Query string rather than route segments: either may be left out.
        [FromQuery] int? orgID,
        [FromQuery] int? projectID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetWeekendSetupListAsync(orgID, projectID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Adds the weekend setup of a project, or edits one - <b>one endpoint for
    /// both</b>.
    /// <para>
    /// Send <c>timeSheetWeekendSetupId</c> as <c>0</c> (or leave it out) to
    /// add a setup. Send an existing setup's <c>timeSheetWeekendSetupId</c> to
    /// edit it; the setup must belong to <c>organizationId</c>. An edit
    /// overwrites the client, the project and all seven day flags, so send
    /// them all.
    /// </para>
    /// <para>
    /// A project has only one weekend setup: adding a second one for the same
    /// project, or moving a setup onto a project that already has one, is a 409.
    /// </para>
    /// </summary>
    /// <response code="200">The saved setup, including its <c>timeSheetWeekendSetupId</c>.</response>
    /// <response code="400">The payload failed validation, or names a client or project that does not exist in the organisation - every failure is listed.</response>
    /// <response code="404">A <c>timeSheetWeekendSetupId</c> was sent, but no live setup of that organisation has it.</response>
    /// <response code="409">The project already has another weekend setup.</response>
    [HttpPost("add-edit-timesheet-weekend-setup")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetWeekendSetupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddEditTimesheetWeekendSetup(
        [FromBody] TimesheetWeekendSetupSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveWeekendSetupAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Weekend setup saved."));
    }

    /// <summary>
    /// Soft-deletes a weekend setup, by <c>timeSheetWeekendSetupId</c>. The row
    /// is kept, stamped with <c>IsDelete</c> = 1, <c>DeletedDate</c> and
    /// <c>DeletedBy</c>; the project may then be given a new setup.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching the other removes here:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The setup was removed.</response>
    /// <response code="400">A <c>timeSheetWeekendSetupId</c> or <c>deletedBy</c> that is not greater than zero.</response>
    /// <response code="404">No live setup has that id; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-weekend-setup")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetWeekendSetup(
        [FromBody] TimesheetWeekendSetupDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteWeekendSetupAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Weekend setup removed."));
    }

    #endregion

    #region User Access Setup

    /// <summary>
    /// Returns the module assignments in <c>dbo.Timesheet_role_assignment</c> -
    /// which modules each user may use - with the user's name, email and
    /// organisation.
    /// <para>
    /// Filter by organisation, by user, or both. A filter left out or sent as
    /// <c>0</c> is not applied: <c>orgID</c> alone returns every assignment of
    /// the organisation's users, <c>userID</c> alone the user's assignment (a
    /// user has at most one). At least one is required. Assignments of removed
    /// users are not included.
    /// </para>
    /// </summary>
    /// <param name="orgID">The organisation whose users' assignments to list. Optional when <c>userID</c> is sent.</param>
    /// <param name="userID">The user whose assignment to return. Optional when <c>orgID</c> is sent.</param>
    /// <response code="200">The assignments. None matching is an empty list, not a 404.</response>
    /// <response code="400">Neither <c>orgID</c> nor <c>userID</c> was sent, or one is negative.</response>
    [HttpGet("get-user-access-setup")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetRoleAssignmentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUserAccessSetup(
        // Query string rather than route segments: either may be left out.
        [FromQuery] int? orgID,
        [FromQuery] int? userID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetRoleAssignmentListAsync(orgID, userID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Adds the module assignment of a user, or edits one - <b>one endpoint for
    /// both</b>.
    /// <para>
    /// Send <c>assignmentId</c> as <c>0</c> (or leave it out) to add an
    /// assignment. Send an existing assignment's <c>assignmentId</c> to edit
    /// it. An edit overwrites the user and all eight module flags, so send
    /// them all.
    /// </para>
    /// <para>
    /// A user has only one assignment: adding a second one for the same user,
    /// or moving an assignment onto a user who already has one, is a 409.
    /// </para>
    /// </summary>
    /// <response code="200">The saved assignment, including its <c>assignmentId</c>.</response>
    /// <response code="400">The payload failed validation, or the user is not a live user of the organisation.</response>
    /// <response code="404">An <c>assignmentId</c> was sent, but no assignment has it.</response>
    /// <response code="409">The user already has another assignment.</response>
    [HttpPost("add-edit-user-access-setup")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetRoleAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddEditUserAccessSetup(
        [FromBody] TimesheetRoleAssignmentSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveRoleAssignmentAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "User access setup saved."));
    }

    /// <summary>
    /// Deletes a module assignment, by <c>assignmentId</c>. <b>A hard
    /// delete</b>: the table has no soft-delete column, so the row is
    /// physically removed, and the user may then be given a new assignment.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching the other removes here:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The assignment was removed.</response>
    /// <response code="400">An <c>assignmentId</c> that is not greater than zero.</response>
    /// <response code="404">No assignment has that id; removing one twice is not a silent success.</response>
    [HttpPost("remove-user-access-setup")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveUserAccessSetup(
        [FromBody] TimesheetRoleAssignmentDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteRoleAssignmentAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("User access setup removed."));
    }

    #endregion

    #region Project Time Log

    /// <summary>
    /// Returns the live project time log entries matching the filters, with
    /// the user and project named.
    /// <para>
    /// Filter by user, by project, by sheet code, or any combination, and
    /// narrow to a <c>LogDate</c> range with <c>fromDate</c> / <c>toDate</c>.
    /// A filter left out or sent as <c>0</c> is not applied. <b>At least one
    /// of <c>userID</c>, <c>projectID</c> and <c>sheetSubmissionCode</c> is
    /// required</b> - the whole table is never returned. Deleted entries are
    /// not included.
    /// </para>
    /// <para>
    /// Read through EF Core, not a stored procedure: this module has none, so
    /// it works as soon as the tables exist. Ordered by <c>logDate</c>, then
    /// start time, then id.
    /// </para>
    /// </summary>
    /// <param name="userID">Whose entries to list. Optional when another filter is sent.</param>
    /// <param name="projectID">Which project's entries to list. Optional when another filter is sent.</param>
    /// <param name="sheetSubmissionCode">Which sheet's entries to list. Optional when another filter is sent.</param>
    /// <param name="fromDate">Earliest <c>logDate</c> to include. Optional; any time part is ignored.</param>
    /// <param name="toDate">Latest <c>logDate</c> to include. Optional; any time part is ignored.</param>
    /// <response code="200">The entries. None matching is an empty list, not a 404.</response>
    /// <response code="400">No filter was sent, one is negative, or <c>toDate</c> is before <c>fromDate</c>.</response>
    [HttpGet("get-project-time-log")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetProjectTimeLogResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProjectTimeLog(
        // Query string rather than route segments: every filter is optional on
        // its own, and only the combination is required.
        [FromQuery] int? userID,
        [FromQuery] int? projectID,
        [FromQuery] string? sheetSubmissionCode,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetProjectTimeLogListAsync(
            userID,
            projectID,
            sheetSubmissionCode,
            fromDate,
            toDate,
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Logs a block of time against a project, or edits one - <b>one endpoint
    /// for both</b>.
    /// <para>
    /// Send <c>timesheetProjectTimeLogId</c> as <c>0</c> (or leave it out) to
    /// log a new block. Send an existing entry's id to edit it. An edit
    /// overwrites every field, so a field left out is saved as <c>null</c>
    /// rather than left as it was - send the whole entry.
    /// </para>
    /// <para>
    /// <b>The sheet is decided here, not sent.</b> A new entry joins the
    /// user's open sheet - every entry they log between one submission and the
    /// next shares one <c>sheetSubmissionCode</c>, across every project - and
    /// starts a new sheet only when their last one has been submitted.
    /// Submitting is what ends a sheet; no date does. An edit leaves the entry
    /// on the sheet it is already on, so editing can never move time from one
    /// submission to another.
    /// </para>
    /// <para>
    /// Once a sheet is submitted its entries are frozen: editing or removing
    /// one answers <b>409</b> until the sheet is rejected. Logging NEW time
    /// after a submission is always fine - it simply starts the next sheet.
    /// </para>
    /// </summary>
    /// <response code="200">The saved entry, with the <c>sheetSubmissionCode</c> it was filed under.</response>
    /// <response code="400">The payload failed validation, or the user has no live assignment to the project - every failure is listed.</response>
    /// <response code="404">An id was sent, but no live entry has it.</response>
    /// <response code="409">The edited entry's sheet has been submitted and not rejected.</response>
    [HttpPost("add-edit-project-time-log")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetProjectTimeLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddEditProjectTimeLog(
        [FromBody] TimesheetProjectTimeLogSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveProjectTimeLogAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Time log entry saved."));
    }

    /// <summary>
    /// Soft-deletes a project time log entry. The row is kept, stamped with
    /// <c>IsDelete</c> = 1, <c>DeletedDate</c> and <c>DeletedBy</c>; nothing
    /// is physically deleted.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching the other removes here:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The entry was removed.</response>
    /// <response code="400">A <c>timesheetProjectTimeLogId</c> or <c>deletedBy</c> that is not greater than zero.</response>
    /// <response code="404">No live entry has that id; removing one twice is not a silent success.</response>
    /// <response code="409">The entry's sheet has been submitted and not rejected.</response>
    [HttpPost("remove-project-time-log")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveProjectTimeLog(
        [FromBody] TimesheetProjectTimeLogDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteProjectTimeLogAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Time log entry removed."));
    }

    #endregion

    #region Project Sheet Submission

    /// <summary>
    /// Returns the user's current project sheet, ready to review and hand in.
    /// <para>
    /// Everything a review-and-submit screen needs in one call: the sheet's
    /// <c>sheetSubmissionCode</c>, the span actually logged, the totals in
    /// minutes and hours, one line per project worked on, and every entry
    /// behind them. <c>canSubmit</c> says whether the button should be live.
    /// </para>
    /// <para>
    /// A user who has never logged anything still gets a sheet back - the code
    /// their first one would take, with zero totals and no entries. That is a
    /// submittable sheet, not an error: handing in a period with nothing
    /// logged is a statement.
    /// </para>
    /// <para>
    /// When the sheet has already been handed in, <c>submission</c> carries
    /// it. A <b>rejected</b> sheet comes back here too, with
    /// <c>rejectionReason</c>, which is what the screen shows the user to
    /// correct before they submit again.
    /// </para>
    /// </summary>
    /// <param name="userID">Whose sheet to return.</param>
    /// <response code="200">The sheet, empty or not.</response>
    /// <response code="400">A <c>userID</c> that is not greater than zero.</response>
    [HttpGet("get-current-project-timesheet")]
    [ProducesResponseType(typeof(ApiResponse<ProjectTimesheetCurrentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCurrentProjectTimesheet(
        [FromQuery] int userID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetCurrentProjectTimesheetAsync(userID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Hands in the user's open sheet.
    /// <para>
    /// There is no sheet code to send: whichever sheet the user's entries are
    /// accumulating on is the one submitted. Its entries are totalled, the
    /// submission is written with status <c>1</c> (Submitted), and a row is
    /// appended to the audit trail.
    /// </para>
    /// <para>
    /// Submitting is what <b>ends</b> a sheet. From then on its entries are
    /// frozen - editing or removing one answers 409 - and the next time the
    /// user logs anything it starts the next sheet, with the next code.
    /// </para>
    /// <para>
    /// A user with nothing logged submits an <b>empty</b> sheet, which is
    /// allowed; only its owner may submit it, because the row has no column
    /// for whose sheet it is and an empty one has no entries to recover that
    /// from. A <b>rejected</b> sheet is resubmitted in place, so one code
    /// never becomes two sheets.
    /// </para>
    /// </summary>
    /// <response code="200">The submitted sheet.</response>
    /// <response code="400">The payload failed validation, or an empty sheet was submitted by someone other than its owner.</response>
    /// <response code="409">The sheet has already been submitted and is awaiting review.</response>
    [HttpPost("submit-project-timesheet")]
    [ProducesResponseType(typeof(ApiResponse<ProjectTimesheetSubmissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitProjectTimesheet(
        [FromBody] ProjectTimesheetSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SubmitProjectTimesheetAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Timesheet submitted."));
    }

    /// <summary>
    /// Approves or rejects a submitted sheet.
    /// <para>
    /// <c>statusId</c> takes <c>2</c> (Approved) or <c>3</c> (Rejected) only -
    /// a sheet reaches <c>1</c> (Submitted) by being submitted, not here. A
    /// rejection <b>requires</b> a <c>rejectionReason</c>: the user has to
    /// know what to correct.
    /// </para>
    /// <para>
    /// Repeatable, like the review on the employee timesheet module: an
    /// approved sheet can be rejected and the other way round. The columns
    /// describe the sheet's <b>current</b> status, so the stamps that no
    /// longer apply are cleared - the history is in the audit trail, which is
    /// append-only.
    /// </para>
    /// <para>
    /// Rejecting a sheet <b>reopens its entries</b>, so the user can correct
    /// them and submit the same sheet again.
    /// </para>
    /// </summary>
    /// <response code="200">The reviewed sheet.</response>
    /// <response code="400">A status other than 2 or 3, a rejection with no reason, or a field that failed its rule.</response>
    /// <response code="404">No live sheet has that id.</response>
    [HttpPost("update-project-timesheet-status")]
    [ProducesResponseType(typeof(ApiResponse<ProjectTimesheetSubmissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProjectTimesheetStatus(
        [FromBody] ProjectTimesheetStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.UpdateProjectTimesheetStatusAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Timesheet status updated."));
    }

    /// <summary>
    /// Soft-deletes a submitted sheet. The row is kept with <c>IsDelete</c> =
    /// 1; nothing is physically deleted.
    /// <para>
    /// <b>This reopens the sheet's entries.</b> The submission was the only
    /// thing holding them shut, so after this the user can edit them and
    /// submit the same code again - the sheet goes back to being their open
    /// one.
    /// </para>
    /// <para>
    /// <c>dbo.TimesheetProjectSubmission</c> has no <c>DeletedBy</c> or
    /// <c>DeletedDate</c> column, so <c>deletedBy</c> is recorded in the
    /// application log and nowhere else. The audit trail is not touched: it
    /// records status changes, and a removal is not one.
    /// </para>
    /// </summary>
    /// <response code="200">The sheet was removed.</response>
    /// <response code="400">An id or a <c>deletedBy</c> that is not greater than zero.</response>
    /// <response code="404">No live sheet has that id; removing one twice is not a silent success.</response>
    [HttpPost("remove-project-timesheet")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProjectTimesheet(
        [FromBody] ProjectTimesheetDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteProjectTimesheetAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Timesheet removed."));
    }

    #endregion
}
