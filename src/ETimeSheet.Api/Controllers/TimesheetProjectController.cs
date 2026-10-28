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
    /// <c>fullName</c>, <c>email</c>, <c>countryId</c> and <c>address</c> are
    /// saved to the user in <c>dbo.Signup</c> when they change - and so change
    /// for every project the user is on. Every other field is saved to the
    /// assignment in <c>dbo.Timesheet_Users_Project_setup</c>. Both are
    /// written together, or neither is.
    /// </para>
    /// <para>
    /// An edit overwrites every field, so send the whole assignment -
    /// <c>countryId</c> or <c>address</c> left out clears the stored value.
    /// </para>
    /// </summary>
    /// <response code="200">The saved assignment, with the user's name, email, country and address.</response>
    /// <response code="400">The payload failed validation, or names a country, project or role that does not exist - every failure is listed.</response>
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
    /// it. An edit overwrites the user and all nine module flags, so send
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
    /// Returns a user's time on projects, from <c>dbo.TimesheetProjectTimeLog</c>.
    /// Ordered by date, then project, then entry - a day may hold several
    /// entries on one project.
    /// <para>
    /// <c>projectID</c>, <c>fromDate</c> and <c>toDate</c> narrow the list; one
    /// left out (or <c>projectID</c> sent as <c>0</c>) is not applied. The
    /// dates are inclusive. Deleted entries are not included.
    /// </para>
    /// </summary>
    /// <param name="userID">The user whose entries to list. Required.</param>
    /// <param name="projectID">The project to narrow to. Optional.</param>
    /// <param name="fromDate">The first log date to include. Optional.</param>
    /// <param name="toDate">The last log date to include. Optional.</param>
    /// <response code="200">The entries. None matching is an empty list, not a 404.</response>
    /// <response code="400">No <c>userID</c>, a negative <c>projectID</c>, or <c>toDate</c> before <c>fromDate</c>.</response>
    [HttpGet("get-timesheet-project-time-log")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetProjectTimeLogResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetProjectTimeLog(
        // Query string rather than route segments: all but userID may be left out.
        [FromQuery] int userID,
        [FromQuery] int? projectID,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetProjectTimeLogListAsync(
            userID,
            projectID,
            fromDate,
            toDate,
            cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Adds or edits an entry of hours on a project for one day. Send
    /// <c>userId</c>, <c>projectId</c>, <c>logDate</c>, <c>hoursLogged</c>
    /// (whole hours) and <c>addedBy</c>; no start or end time.
    /// <para>
    /// <c>timesheetProjectTimeLogId</c> decides which it is. <b>0</b>, or left
    /// out, <b>adds</b> a new entry - a day may hold several. An id
    /// <b>greater than 0 edits</b> that entry, which must exist and belong to
    /// <c>userId</c> on <c>projectId</c>: <c>hoursLogged</c> replaces its
    /// hours, and <c>isRemoteLog</c> or <c>description</c> replace the
    /// entry's when sent and leave them when left out. The hours are not
    /// capped. Any day of the project may be logged, past days included.
    /// </para>
    /// <para>
    /// The day gets the <c>sheetSubmissionCode</c> of its submission period -
    /// worked out from the project's start date and submission frequency, and
    /// returned with <c>periodStartDate</c> and <c>periodEndDate</c>. Every day
    /// of the user on the project in one period shares one code: the first day
    /// logged in it takes the next <c>PSHT</c> code, whichever day that is.
    /// An edit that moves an entry to another period takes that period's code,
    /// and the period it leaves must be open as well.
    /// </para>
    /// <para>
    /// Logging time does not open a timesheet submission.
    /// </para>
    /// </summary>
    /// <response code="200">The entry as saved.</response>
    /// <response code="400">The payload failed validation, the user is not on the project, the date is outside the project's dates, the project has no start date, submission frequency or organization, the day is leave or a holiday, the entry belongs to another user or project, or the period is submitted or approved.</response>
    /// <response code="404">No live entry has that <c>timesheetProjectTimeLogId</c>.</response>
    [HttpPost("add-edit-timesheet-project-time-log")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetProjectTimeLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEditTimesheetProjectTimeLog(
        [FromBody] TimesheetProjectTimeLogSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveProjectTimeLogAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Time log saved."));
    }

    /// <summary>
    /// Soft-deletes a time entry, by <c>timesheetProjectTimeLogId</c>. The row
    /// is kept, stamped with <c>IsDelete</c> = 1, <c>DeletedDate</c> and
    /// <c>DeletedBy</c>.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching the other removes here:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The entry was removed.</response>
    /// <response code="400">An id that is not greater than zero, the entry is another user's, or its period is submitted or approved.</response>
    /// <response code="404">No live entry has that id; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-project-time-log")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetProjectTimeLog(
        [FromBody] TimesheetProjectTimeLogDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteProjectTimeLogAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Time log removed."));
    }

    #endregion

    #region Project Time Sheet

    /// <summary>
    /// Returns project timesheets, ordered by period, then project name, then
    /// user - one per user, project and <b>submitted</b> period.
    /// <para>
    /// Filter by user, by project, by organisation, or any combination. A
    /// filter left out or sent as <c>0</c> is not applied; at least one is
    /// required.
    /// </para>
    /// <para>
    /// A sheet exists only once its period is submitted, so a period that has
    /// not been submitted is not listed. Each sheet carries its status
    /// (Submitted, Approved or Rejected), and the entry count and time from
    /// <c>dbo.TimesheetProjectTimeLog</c> within its period.
    /// </para>
    /// </summary>
    /// <param name="userID">The user whose sheets to list. Optional when <c>projectID</c> or <c>orgID</c> is sent.</param>
    /// <param name="projectID">The project whose sheets to list. Optional when <c>userID</c> or <c>orgID</c> is sent.</param>
    /// <param name="orgID">The organisation whose sheets to list. Optional when <c>userID</c> or <c>projectID</c> is sent.</param>
    /// <response code="200">The sheets. None matching is an empty list, not a 404.</response>
    /// <response code="400">No filter was sent, or one is negative.</response>
    [HttpGet("get-timesheet-project-sheet")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetProjectSheetResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetProjectSheet(
        // Query string rather than route segments: any of them may be left out.
        [FromQuery] int? userID,
        [FromQuery] int? projectID,
        [FromQuery] int? orgID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetProjectSheetListAsync(userID, projectID, orgID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Submits a user's project timesheet for one period. Send <c>userId</c>,
    /// <c>projectId</c>, <c>addedBy</c> and the period: its
    /// <c>sheetSubmissionCode</c> (from the time log, or a rejected sheet), or
    /// <c>periodDate</c> - any day in it. A period with no time logged has no
    /// code, so its blank sheet is submitted by <c>periodDate</c>.
    /// <para>
    /// The period comes from the project's start date and submission
    /// frequency, and can be submitted on its <b>last day or any day after</b>
    /// - the 7th day of a weekly period, the 15th of a 15-day one, the day
    /// before the next month's start day for a monthly one - judged in the
    /// user's time zone (<c>dbo.TimesheetMasterSetup.TimeZone</c>; UTC when
    /// they have none).
    /// </para>
    /// <para>
    /// The first submit creates the period's one row, already Submitted, with
    /// the code its time log carries (the next code for a blank sheet). A
    /// rejected sheet is resubmitted in that row. The totals are taken from the
    /// time log, and the submit is recorded in
    /// <c>dbo.TimesheetProjectSubmissionAuditLog</c>. Once submitted, the
    /// period's time cannot be changed until the sheet is rejected.
    /// </para>
    /// </summary>
    /// <response code="200">The submitted sheet.</response>
    /// <response code="400">A field is missing; the code is not this user's on this project; the date is outside the project or not in the code's period; the project is not set up for submission; the period has not reached its last day; or the user is not on the project.</response>
    /// <response code="409">The period is already submitted or approved.</response>
    [HttpPost("submit-timesheet-project-sheet")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetProjectSheetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitTimesheetProjectSheet(
        [FromBody] TimesheetProjectSheetSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SubmitProjectSheetAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, $"Timesheet {result.SheetSubmissionCode} submitted."));
    }

    /// <summary>
    /// Approves or rejects a submitted project timesheet: <c>statusId</c> 2 to
    /// approve, 3 to reject with a <c>rejectionReason</c>. The change is
    /// recorded in <c>dbo.TimesheetProjectSubmissionAuditLog</c>.
    /// <para>
    /// Only a submitted sheet can be approved or rejected. An approved sheet is
    /// final; a rejected one goes back to the user, who can change its time
    /// and resubmit it.
    /// </para>
    /// </summary>
    /// <response code="200">The sheet after the change.</response>
    /// <response code="400">The payload failed validation - every failing field is listed.</response>
    /// <response code="403"><c>addedBy</c> is not the project's timesheet approver (<c>get-timesheet-approver-setup</c>, <c>isTimesheetApprover</c> true).</response>
    /// <response code="404">No sheet has that id.</response>
    /// <response code="409">The sheet is not submitted.</response>
    [HttpPost("update-timesheet-project-sheet-status")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetProjectSheetResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTimesheetProjectSheetStatus(
        [FromBody] TimesheetProjectSheetStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.UpdateProjectSheetStatusAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, $"Timesheet {result.SheetSubmissionCode} {result.StatusName.ToLowerInvariant()}."));
    }

    #endregion

    #region Approver Setup

    /// <summary>
    /// Returns the live approver setups in <c>dbo.Timesheet_Approver_setup</c> -
    /// who approves each project's timesheets and leave - with the project's code and name
    /// and the approver's name and email.
    /// <para>
    /// Filter by organisation, project or both. A filter left out or sent as
    /// <c>0</c> is not applied: <c>orgID</c> alone returns every setup of the
    /// organisation, <c>projectID</c> alone the project's approvers (at most
    /// one timesheet approver and one leave approver). At least one is
    /// required. Deleted setups are not
    /// included.
    /// </para>
    /// </summary>
    /// <param name="orgID">The organisation whose setups to list. Optional when <c>projectID</c> is sent.</param>
    /// <param name="projectID">The project whose approvers to return. Optional when <c>orgID</c> is sent.</param>
    /// <response code="200">The setups. None matching is an empty list, not a 404.</response>
    /// <response code="400">Neither <c>orgID</c> nor <c>projectID</c> was sent, or one is negative.</response>
    [HttpGet("get-timesheet-approver-setup")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetApproverSetupResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetApproverSetup(
        // Query string rather than route segments: either may be left out.
        [FromQuery] int? orgID,
        [FromQuery] int? projectID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetApproverSetupListAsync(orgID, projectID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Adds the approver of a project, or edits one - <b>one endpoint for
    /// both</b>.
    /// <para>
    /// Send <c>timesheetApproverSetupId</c> as <c>0</c> (or leave it out) to
    /// add a setup. Send an existing setup's id to edit it; it must belong to
    /// <c>organizationId</c>. An edit overwrites the project, the approver,
    /// <c>isTimesheetApprover</c> and <c>isLeaveApprover</c>, so send them all.
    /// At least one of the two flags must be true.
    /// </para>
    /// <para>
    /// A project has one timesheet approver, and an organisation has one leave
    /// approver - who may be different users. Claiming an area that already
    /// has a live approver - by adding, or by editing a setup onto it - is a
    /// 409. An area nobody holds yet can be taken. A leave-only setup may
    /// leave out <c>projectId</c>.
    /// </para>
    /// </summary>
    /// <response code="200">The saved setup, including its <c>timesheetApproverSetupId</c>.</response>
    /// <response code="400">The payload failed validation (including neither flag true), the project is not in the organisation, or the approver is not a live user of it.</response>
    /// <response code="404">A <c>timesheetApproverSetupId</c> was sent, but no live setup of the organisation has it.</response>
    /// <response code="409">The project already has a timesheet approver, or the organisation a leave approver.</response>
    [HttpPost("add-edit-timesheet-approver-setup")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetApproverSetupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddEditTimesheetApproverSetup(
        [FromBody] TimesheetApproverSetupSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveApproverSetupAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Approver setup saved."));
    }

    /// <summary>
    /// Removes an approver setup, by <c>timesheetApproverSetupId</c>. <b>A soft
    /// delete</b>: the row is stamped with <c>IsDelete</c>, <c>DeletedDate</c>
    /// and <c>DeletedBy</c> and kept, and the project may then be given a new
    /// approver.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching the other removes here:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The setup was removed.</response>
    /// <response code="400">A <c>timesheetApproverSetupId</c> or <c>deletedBy</c> that is not greater than zero.</response>
    /// <response code="404">No live setup has that id; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-approver-setup")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetApproverSetup(
        [FromBody] TimesheetApproverSetupDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteApproverSetupAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Approver setup removed."));
    }

    #endregion

    #region User Details

    /// <summary>
    /// Returns one timesheet user: their details - name, email, address,
    /// profile picture, organisation, country, currency and time zone - and a
    /// list of every live project they are assigned to, through
    /// <c>dbo.spc_GetTimesheetUserDetailsByUserID</c>.
    /// <para>
    /// Each project carries the assignment (role, job type, rates, daily cap,
    /// and the <c>projectLocationLatitude</c> / <c>projectLocationLongitude</c>
    /// recorded for this user on it), the project's <c>client</c>, and its
    /// hours: <c>totalExpectedHours</c>
    /// (the project's budgeted <c>Totalhours</c>), <c>totalLoggedHours</c>
    /// (the user's, all time) and <c>currentPeriodLoggedHours</c> - the
    /// user's hours in the project's current timesheet period, worked out on
    /// the user's own date in the time zone of their timesheet setup, with the
    /// period's code and submission status.
    /// </para>
    /// </summary>
    /// <param name="userID">The user. Must be greater than 0.</param>
    /// <response code="200">The user. A user on no project has an empty <c>projects</c> list.</response>
    /// <response code="400"><c>userID</c> is not greater than 0.</response>
    /// <response code="404">No live user has that id.</response>
    [HttpGet("get-project-timesheet-user-details-by-userID/{userID:int}")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetUserDetailsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimesheetUserDetailsByUserId(
        int userID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetTimesheetUserDetailsAsync(userID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    #endregion

    #region Leave Setup

    /// <summary>
    /// Returns the live leave types of an organisation from
    /// <c>dbo.Timesheet_Leavetype_Setup</c>, ordered by name, each with the
    /// days allocated. Deleted leave types are not included.
    /// </summary>
    /// <param name="orgID">The organisation whose leave types to list. Required, greater than 0.</param>
    /// <response code="200">The leave types. None is an empty list, not a 404.</response>
    /// <response code="400"><c>orgID</c> was not sent, or is not greater than 0.</response>
    [HttpGet("get-timesheet-leave-setup")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetLeaveSetupResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetLeaveSetup(
        [FromQuery] int? orgID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetLeaveSetupListAsync(orgID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Adds a leave type to an organisation, or edits one - <b>one endpoint for
    /// both</b>.
    /// <para>
    /// Send <c>leaveTypeId</c> as <c>0</c> (or leave it out) to add a leave
    /// type to <c>organizationId</c>. Send an existing leave type's
    /// <c>leaveTypeId</c> to edit it; it must belong to <c>organizationId</c>.
    /// An edit overwrites <c>leaveType</c> and <c>noOfLeaveAllocated</c>, so
    /// send both.
    /// </para>
    /// </summary>
    /// <response code="200">The saved leave type, including its <c>leaveTypeId</c>.</response>
    /// <response code="400">The payload failed validation - every failure is listed.</response>
    /// <response code="404">A <c>leaveTypeId</c> was sent, but no live leave type of that organisation has it.</response>
    [HttpPost("add-edit-timesheet-leave-setup")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetLeaveSetupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEditTimesheetLeaveSetup(
        [FromBody] TimesheetLeaveSetupSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveLeaveSetupAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Leave type saved."));
    }

    /// <summary>
    /// Removes a leave type of an organisation. <b>A soft delete</b>: the row
    /// is stamped with <c>IsDelete</c>, <c>DeletedDate</c> and
    /// <c>DeletedBy</c> and kept.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching the other removes here:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The leave type was removed.</response>
    /// <response code="400">A <c>leaveTypeId</c>, <c>organizationId</c> or <c>deletedBy</c> that is not greater than zero.</response>
    /// <response code="404">No live leave type with that id belongs to that organisation; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-leave-setup")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetLeaveSetup(
        [FromBody] TimesheetLeaveSetupDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteLeaveSetupAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Leave type removed."));
    }

    #endregion

    #region User Leave Assignment

    /// <summary>
    /// Returns the live leave assignments in
    /// <c>dbo.Timesheet_Leave_assignment_user</c> - which leave types each user
    /// holds and the balance left - with the user's name and email and the
    /// leave type's name and allocation.
    /// <para>
    /// Filter by organisation, user or both. A filter left out or sent as
    /// <c>0</c> is not applied: <c>orgID</c> alone returns every assignment of
    /// the organisation's live users, <c>userID</c> alone one user's. At least
    /// one is required. Deleted assignments are not included.
    /// </para>
    /// </summary>
    /// <param name="orgID">The organisation whose assignments to list. Optional when <c>userID</c> is sent.</param>
    /// <param name="userID">The user whose assignments to list. Optional when <c>orgID</c> is sent.</param>
    /// <response code="200">The assignments. None matching is an empty list, not a 404.</response>
    /// <response code="400">Neither <c>orgID</c> nor <c>userID</c> was sent, or one is negative.</response>
    [HttpGet("get-timesheet-user-leave-assignment")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetUserLeaveAssignmentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetUserLeaveAssignment(
        // Query string rather than route segments: either may be left out.
        [FromQuery] int? orgID,
        [FromQuery] int? userID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetUserLeaveAssignmentListAsync(orgID, userID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Assigns a leave type to a user. The balance is not sent: it starts at
    /// the leave type's allocation (<c>NoofLeaveAllocated</c>).
    /// <para>
    /// A user holds each leave type once: assigning one the user already holds
    /// is a 409.
    /// </para>
    /// </summary>
    /// <response code="200">The new assignment, including its <c>timesheetLeaveAssignmentUserId</c>.</response>
    /// <response code="400">The payload failed validation, the user is not a live user of the organisation, or the leave type is not a live leave type of it.</response>
    /// <response code="409">The user already holds this leave type.</response>
    [HttpPost("add-timesheet-user-leave-assignment")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetUserLeaveAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddTimesheetUserLeaveAssignment(
        [FromBody] TimesheetUserLeaveAssignmentAddRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.AddUserLeaveAssignmentAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Leave assignment added."));
    }

    /// <summary>
    /// Edits a leave assignment of the organisation. The user and the leave
    /// type are overwritten, so send both.
    /// <para>
    /// <c>balanceLeave</c> is optional: sent, it replaces the balance; left out
    /// or null, the balance is not changed - even when the leave type is.
    /// </para>
    /// <para>
    /// Moving the assignment onto a leave type the user already holds in
    /// another assignment is a 409.
    /// </para>
    /// </summary>
    /// <response code="200">The edited assignment.</response>
    /// <response code="400">The payload failed validation, the user is not a live user of the organisation, or the leave type is not a live leave type of it.</response>
    /// <response code="404">No live assignment of the organisation has that <c>timesheetLeaveAssignmentUserId</c>.</response>
    /// <response code="409">The user already holds this leave type in another assignment.</response>
    [HttpPost("edit-timesheet-user-leave-assignment")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetUserLeaveAssignmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditTimesheetUserLeaveAssignment(
        [FromBody] TimesheetUserLeaveAssignmentEditRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.EditUserLeaveAssignmentAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Leave assignment updated."));
    }

    /// <summary>
    /// Removes a leave assignment. <b>A soft delete</b>: the row is stamped with
    /// <c>IsDelete</c>, <c>DeletedDate</c> and <c>DeletedBy</c> and kept, and
    /// the user may then be assigned the leave type again.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching the other removes here:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The assignment was removed.</response>
    /// <response code="400">A <c>timesheetLeaveAssignmentUserId</c>, <c>organizationId</c> or <c>deletedBy</c> that is not greater than zero.</response>
    /// <response code="404">No live assignment of that organisation has that id; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-user-leave-assignment")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetUserLeaveAssignment(
        [FromBody] TimesheetUserLeaveAssignmentDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteUserLeaveAssignmentAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Leave assignment removed."));
    }

    #endregion

    #region User Leave Apply

    /// <summary>
    /// Returns the leave applications in <c>dbo.Timesheet_Leave_User_apply</c>,
    /// newest first, with the user's name and email, the leave type's name and
    /// the status's name from <c>dbo.TimesheetLeaveStatus_Lookup</c>.
    /// <para>
    /// Filter by organisation, user or both. A filter left out or sent as
    /// <c>0</c> is not applied: <c>orgID</c> alone returns every application of
    /// the organisation's live users, <c>userID</c> alone one user's. At least
    /// one is required.
    /// </para>
    /// </summary>
    /// <param name="orgID">The organisation whose applications to list. Optional when <c>userID</c> is sent.</param>
    /// <param name="userID">The user whose applications to list. Optional when <c>orgID</c> is sent.</param>
    /// <response code="200">The applications. None matching is an empty list, not a 404.</response>
    /// <response code="400">Neither <c>orgID</c> nor <c>userID</c> was sent, or one is negative.</response>
    [HttpGet("get-timesheet-user-leave-apply")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TimesheetUserLeaveApplyResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetUserLeaveApply(
        // Query string rather than route segments: either may be left out.
        [FromQuery] int? orgID,
        [FromQuery] int? userID,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.GetUserLeaveApplyListAsync(orgID, userID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Applies for leave, or edits an application - <b>one endpoint for
    /// both</b>.
    /// <para>
    /// Send <c>timesheetLeaveUserApplyId</c> as <c>0</c> (or leave it out) to
    /// apply: the application is saved as Submitted and given the next
    /// <c>leaveId</c>. Send an existing application's id to edit it; it must be
    /// the user's own. An edit overwrites the leave type, the dates and the
    /// comment, so send them all, and puts the application back to
    /// Submitted - a rejected one is resubmitted.
    /// </para>
    /// <para>
    /// The days are not sent: they are worked out from the dates - every
    /// calendar day from <c>startDate</c> to <c>endDate</c>, both counted.
    /// </para>
    /// <para>
    /// <b>An approved application cannot be edited.</b> The balance is not
    /// touched here.
    /// </para>
    /// </summary>
    /// <response code="200">The saved application, including its <c>timesheetLeaveUserApplyId</c> and <c>leaveId</c>.</response>
    /// <response code="400">The payload failed validation, the user, leave type or assignment does not exist, or the application is approved.</response>
    /// <response code="404">A <c>timesheetLeaveUserApplyId</c> was sent, but no application of this user in the organisation has it.</response>
    [HttpPost("add-edit-timesheet-user-leave-apply")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetUserLeaveApplyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEditTimesheetUserLeaveApply(
        [FromBody] TimesheetUserLeaveApplySaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.SaveUserLeaveApplyAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Leave application saved."));
    }

    /// <summary>
    /// Removes a leave application. <b>A hard delete</b>: the table has no
    /// soft-delete column, so the row is physically removed.
    /// <para>
    /// If the application was <b>approved</b>, its days are given back to the
    /// user's balance for that leave type, in the same transaction. If it was
    /// not approved, the balance is not touched.
    /// </para>
    /// <para>
    /// A POST rather than an HTTP DELETE, matching the other removes here:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The application was removed.</response>
    /// <response code="400">A <c>timesheetLeaveUserApplyId</c>, <c>organizationId</c> or <c>deletedBy</c> that is not greater than zero, or giving the days back would take the balance past 99.99.</response>
    /// <response code="404">No application of that organisation has that id; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-user-leave-apply")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetUserLeaveApply(
        [FromBody] TimesheetUserLeaveApplyDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _timesheetProjectService.DeleteUserLeaveApplyAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Leave application removed."));
    }


    /// <summary>
    /// Approves or rejects a submitted leave application - <c>leaveStatus</c>
    /// is the <c>Value</c> of the Approved or Rejected row of
    /// <c>dbo.TimesheetLeaveStatus_Lookup</c>.
    /// <para>
    /// Only a leave approver of the organisation can decide, and only on its
    /// own organisation's leave: <c>addedBy</c> must be a live user of
    /// <c>organizationId</c> with leave approval on in an approver setup of it,
    /// and the application must belong to <c>organizationId</c>.
    /// </para>
    /// <para>
    /// Approving takes the days from the user's balance for that leave type;
    /// a balance too small is refused. Rejecting needs
    /// <c>approverComments</c> and leaves the balance alone. Only a submitted
    /// application can be decided: an approved one is final, and a rejected
    /// one goes back to the user, who edits it to resubmit.
    /// </para>
    /// </summary>
    /// <response code="200">The application after the change.</response>
    /// <response code="400">The payload failed validation, the balance is too small, or the user no longer holds the leave type.</response>
    /// <response code="403"><c>addedBy</c> is not a leave approver of the organisation.</response>
    /// <response code="404">No application of the organisation has that id.</response>
    /// <response code="409">The application is not submitted.</response>
    [HttpPost("update-timesheet-user-leave-apply-status")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetUserLeaveApplyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTimesheetUserLeaveApplyStatus(
        [FromBody] TimesheetUserLeaveApplyStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timesheetProjectService.UpdateUserLeaveApplyStatusAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, $"Leave application {(result.LeaveStatusName ?? "updated").ToLowerInvariant()}."));
    }

    #endregion
}
