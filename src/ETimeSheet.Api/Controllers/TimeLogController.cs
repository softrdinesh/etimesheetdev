using ETimeSheet.Application.Models;
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
    /// Returns every entry the given user has logged, across all tasks and all
    /// dates, newest first - most recently logged at the top.
    /// <para>
    /// A user with no entries is a 200 with an empty list, not a 404.
    /// </para>
    /// </summary>
    [HttpGet("get-logged-time-list/{userId:int:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<TimeLoggedDetailResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLoggedTimeList(
        [FromRoute] int userId,
        CancellationToken cancellationToken)
    {
        var result = await _timeLogService.GetLoggedTimeListByUserIdAsync(userId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Returns the timesheet setup for the given user: their maximum loggable
    /// time, contract type and the bounds of their timesheet week.
    /// <para>
    /// <b>A user with no setup is a 200, not a 404</b> - <c>success: true</c>
    /// with <c>data: null</c> and a message saying so. The read succeeded; the
    /// answer is that there is no row. <c>success: false</c> is reserved for a
    /// caller who has something to fix.
    /// </para>
    /// </summary>
    /// <response code="200">
    /// The user's setup, or <c>null</c> when they have none. Check <c>data</c>,
    /// not the status code.
    /// </response>
    [HttpGet("get-timesheet-setup-by-user/{userId:int:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<TimesheetMasterSetupResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTimesheetSetupByUser(
        [FromRoute] int userId,
        CancellationToken cancellationToken)
    {
        var result = await _timeLogService.GetTimesheetMasterSetupByUserIdAsync(userId, cancellationToken);

        // The message is what distinguishes "nothing there" from "here it is",
        // now that both are success: true. data is null in the first case and an
        // object in the second, so a client can branch on either.
        return Ok(result is null
            ? ApiResponse.Ok(result, "This user has no timesheet setup.")
            : ApiResponse.Ok(result));
    }

    /// <summary>
    /// Returns the tasks the given user owns, as two separate lists:
    /// <c>projectTasks</c> and <c>sprintTasks</c>.
    /// <para>
    /// A user who owns no tasks gets a 200 with both lists empty. A task id is
    /// unique only within its own list - the two come from different tables.
    /// </para>
    /// </summary>
    [HttpGet("get-user-task-list-by-userid/{userId:int:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<UserTaskListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserTaskListByUserId(
        [FromRoute] int userId,
        CancellationToken cancellationToken)
    {
        var result = await _timeLogService.GetUserTaskListByUserIdAsync(userId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Returns the dashboard summary for the given user: time logged today and
    /// this timesheet week, the week's expected time, the percentage logged and
    /// the time still pending.
    /// <para>
    /// The week starts on the setup's <c>StartDay</c> (Monday when none) and runs
    /// seven days. A user with no setup still gets their logged figures; the
    /// expected, percentage and pending figures are then null.
    /// </para>
    /// <para>
    /// <b>A user who does not exist, or is deleted, is a 200, not a 404</b> -
    /// <c>success: true</c> with <c>data: null</c>, as on the setup read.
    /// </para>
    /// </summary>
    /// <response code="200">
    /// The summary, or <c>null</c> when the user does not exist. Check
    /// <c>data</c>, not the status code.
    /// </response>
    [HttpGet("get-user-dashboard-summary-by-userID/{userId:int:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<UserDashboardSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserDashboardSummaryByUserId(
        [FromRoute] int userId,
        CancellationToken cancellationToken)
    {
        var result = await _timeLogService.GetUserDashboardSummaryByUserIdAsync(userId, cancellationToken);

        return Ok(result is null
            ? ApiResponse.Ok(result, "This user was not found.")
            : ApiResponse.Ok(result));
    }

    /// <summary>
    /// Logs one block of time for an employee.
    /// <para>
    /// The entry is checked against that user's timesheet setup before it is
    /// stored - their working week, whether they may still back-date, their
    /// daily maximum, the day's cut-off - and against the entries they already
    /// have that day, so two blocks cannot cover the same hour. A user with no
    /// setup cannot log time at all.
    /// </para>
    /// <para>
    /// <b>Times of day are judged in the employee's own time zone</b>, the one
    /// on their setup - not the server's. A <c>timeEntryLockAt</c> of 21:00
    /// means nine in the evening where they are. Once that moment has passed
    /// they can still log the hours they are working - an entry starting at
    /// 22:00 is after the cut-off - but can no longer add a block that starts
    /// before it; that needs an administrator. Both rejections are 400s.
    /// </para>
    /// <para>
    /// <b>Adds or edits.</b> A <c>timeLogId</c> of zero, or none, creates an
    /// entry and returns it with its generated <c>sheetId</c>. The
    /// <c>sheetId</c> of an existing entry edits that entry instead, under the
    /// same rules - and the cut-off applies to the entry as it stands as well
    /// as to the new values, so after the cut-off a block that started before
    /// it can be neither changed nor moved.
    /// </para>
    /// </summary>
    /// <response code="200">The entry as it was stored.</response>
    /// <response code="400">The payload failed validation, or the entry breaks one of the setup's rules.</response>
    /// <response code="404"><c>timeLogId</c> names no live entry.</response>
    /// <response code="409">The entry overlaps one the user already has that day.</response>
    [HttpPost("save-employee-time-log")]
    [ProducesResponseType(typeof(ApiResponse<TimeLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveEmployeeTimeLog(
        [FromBody] TimeLogSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _timeLogService.SaveTimeLogAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, request.TimeLogId > 0 ? "Time log updated." : "Time logged."));
    }
}
