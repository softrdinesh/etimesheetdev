using ETimeSheet.Application.DTOs.TimeLogs;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETimeSheet.Api.Controllers;

/// <summary>
/// HTTP surface for the TimeLog module.
/// <para>
/// The controller is deliberately thin: it binds the request, calls
/// <see cref="ITimeLogService"/> and shapes the HTTP response. It holds no
/// business rules, no role checks and no data access, and it depends on the
/// service interface rather than on any implementation.
/// </para>
/// <para>
/// <b>Unauthenticated for now.</b> Authentication is deliberately switched off
/// for this project until it is implemented properly, so the endpoint is
/// explicitly anonymous - without that attribute the deny-by-default fallback
/// policy would still reject every call. The JWT stack is left registered and
/// intact so turning it back on is a one-line change here.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class TimeLogController : ControllerBase
{
    private readonly ITimeLogService _timeLogService;

    public TimeLogController(ITimeLogService timeLogService)
    {
        _timeLogService = timeLogService;
    }

    /// <summary>
    /// Returns every entry the given user logged against the given task within a
    /// date range, plus the totals for that period.
    /// <para>
    /// A POST rather than a GET because every argument travels in the payload;
    /// nothing is taken from the route or the query string.
    /// </para>
    /// </summary>
    [HttpPost("get-time-logged-details")]
    [ProducesResponseType(typeof(ApiResponse<TimeLoggedDetailsForTaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimeLoggedDetails(
        [FromBody] TimeLoggedDetailsForTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timeLogService.GetTimeLoggedDetailsForTaskAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Returns the timesheet setup for the given user: their maximum loggable
    /// time, contract type and the bounds of their timesheet week.
    /// </summary>
    /// <response code="404">The user has no timesheet setup row.</response>
    [HttpGet("get-timesheet-setup-by-user/{userId:int:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetMasterSetupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimesheetSetupByUser(
        [FromRoute] int userId,
        CancellationToken cancellationToken)
    {
        var result = await _timeLogService.GetTimesheetMasterSetupByUserIdAsync(userId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
