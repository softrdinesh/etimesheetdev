using ETimeSheet.Application.Models;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETimeSheet.Api.Controllers;

/// <summary>
/// HTTP surface for the Admin module - administrative CRUD over the
/// per-user timesheet setup held in <c>dbo.TimesheetMasterSetup</c>.
/// <para>
/// As thin as <see cref="TimeLogController"/>: bind, call
/// <see cref="IAdminService"/>, shape the response. No rules, no role
/// checks, no data access.
/// </para>
/// <para>
/// <b>Unauthenticated for now</b>, like the rest of the API. This is the surface
/// that most obviously needs a permission behind it, so the moment JWT is turned
/// back on, this attribute comes off first.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// Saves a user's timesheet setup - adding or updating, whichever applies.
    /// <para>
    /// <b>There is no setup id in the payload.</b> A user holds exactly one
    /// setup, so <c>userId</c> is what names the row: send the same payload
    /// every time and the service updates their setup, or revives and overwrites
    /// a deleted one, or adds the first. The response carries the saved row with
    /// its <c>setupId</c>, so the caller never needs a second call to find out
    /// which happened.
    /// </para>
    /// </summary>
    /// <response code="200">The setup as it now stands.</response>
    /// <response code="400">The payload failed validation.</response>
    [HttpPost("save-user-timesheet-setup")]
    [ProducesResponseType(typeof(ApiResponse<AdminResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveUserTimesheetSetup(
        [FromBody] AdminSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.SaveAsync(request, cancellationToken);

        // Deliberately not "added" or "updated": the controller no longer knows
        // which one happened, and guessing from the payload is what the old
        // setupId switch did. The saved row is in the response if the client
        // cares.
        return Ok(ApiResponse.Ok(result, "Timesheet setup saved."));
    }

    /// <summary>
    /// Returns the timesheet setup belonging to one user. A user has at most
    /// one, so this is a single setup rather than a list.
    /// </summary>
    /// <response code="404">The user has no setup - including one that was deleted.</response>
    [HttpGet("get-user-timesheet-setup/{userID:int:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<AdminResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserTimesheetSetup(
        // Spelled userID, matching the route token character for character.
        // Swagger UI substitutes path parameters case-sensitively, so a
        // parameter named userId against a {userID} token would send the literal
        // text "{userID}", fail the :int constraint, match no route and come
        // back as a confusing 401 from the deny-by-default fallback policy.
        [FromRoute] int userID,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetByUserIdAsync(userID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Returns every employee in one organisation, with the head-count totals
    /// for the same rows.
    /// <para>
    /// The grid is the organisation's staff list as
    /// <c>dbo.spc_GetEmployeeListByPOrgID</c> builds it: contracted time per
    /// week, time logged in the current Monday-Sunday week, progress against the
    /// contract and the contract type. Employees who have <b>no</b> timesheet
    /// setup are included, with a null <c>setupId</c> - finding them is one of
    /// the reasons to open this screen.
    /// </para>
    /// <para>
    /// <c>summary</c> is counted from the rows in the same response rather than
    /// queried separately, so the totals can never disagree with the grid
    /// beneath them.
    /// </para>
    /// </summary>
    /// <param name="orgID">
    /// The organisation whose employees to list. A query parameter rather than a
    /// route segment: the route already says "by-orgid", and
    /// <c>get-all-employees-by-orgid/3</c> reads worse than
    /// <c>get-all-employees-by-orgid?orgID=3</c>.
    /// </param>
    /// <response code="200">The employees and their totals. An organisation with nobody in it is an empty grid, not a 404.</response>
    /// <response code="400">No organisation id, or one that is not greater than zero.</response>
    [HttpGet("get-all-employees-by-orgid")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAllEmployeesByOrgId(
        [FromQuery] int orgID,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetEmployeeListByOrganizationIdAsync(orgID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Soft-deletes a timesheet setup. The row is kept and stamped with who
    /// removed it and when; nothing is ever physically deleted.
    /// <para>
    /// A POST rather than an HTTP DELETE because the request carries a body -
    /// <c>deletedBy</c> has to travel with it - and a body on DELETE is
    /// inconsistently supported by proxies and HTTP clients. It also matches the
    /// rest of this API, where every argument travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="404">No live setup has that id; deleting one twice is not a silent success.</response>
    [HttpPost("delete-timesheet-setup")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTimesheetSetup(
        [FromBody] AdminDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _adminService.DeleteAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Timesheet setup deleted."));
    }
}
