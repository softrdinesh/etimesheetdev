using ETimeSheet.Application.DTOs.AdminSetups;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETimeSheet.Api.Controllers;

/// <summary>
/// HTTP surface for the AdminSetup module - administrative CRUD over the
/// per-user timesheet setup held in <c>dbo.TimesheetMasterSetup</c>.
/// <para>
/// As thin as <see cref="TimeLogController"/>: bind, call
/// <see cref="IAdminSetupService"/>, shape the response. No rules, no role
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
public class AdminSetupController : ControllerBase
{
    private readonly IAdminSetupService _adminSetupService;

    public AdminSetupController(IAdminSetupService adminSetupService)
    {
        _adminSetupService = adminSetupService;
    }

    /// <summary>
    /// Adds or edits a user's timesheet setup.
    /// <para>
    /// <b>One endpoint for both.</b> Send <c>setupId</c> and the row is updated;
    /// leave it out and a new one is added. The response is the saved row either
    /// way, so the caller learns the new <c>setupId</c> without a second call.
    /// </para>
    /// </summary>
    /// <response code="200">The setup as it now stands.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="404">A setupId was supplied but no live setup has it.</response>
    /// <response code="409">The user already has a setup; a user can have only one.</response>
    [HttpPost("save-user-timesheet-setup")]
    [ProducesResponseType(typeof(ApiResponse<AdminSetupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveUserTimesheetSetup(
        [FromBody] AdminSetupSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _adminSetupService.SaveAsync(request, cancellationToken);

        var message = request.SetupId is > 0
            ? "Timesheet setup updated."
            : "Timesheet setup added.";

        return Ok(ApiResponse.Ok(result, message));
    }

    /// <summary>
    /// Returns the timesheet setup belonging to one user. A user has at most
    /// one, so this is a single setup rather than a list.
    /// </summary>
    /// <response code="404">The user has no setup - including one that was deleted.</response>
    [HttpGet("get-user-timesheet-setup/{userID:int:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<AdminSetupResponse>), StatusCodes.Status200OK)]
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
        var result = await _adminSetupService.GetByUserIdAsync(userID, cancellationToken);
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
        [FromBody] AdminSetupDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _adminSetupService.DeleteAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Timesheet setup deleted."));
    }
}
