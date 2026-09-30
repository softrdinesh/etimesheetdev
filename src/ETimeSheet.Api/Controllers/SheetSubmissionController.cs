using ETimeSheet.Application.Models;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Responses;
using ETimeSheet.Shared.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETimeSheet.Api.Controllers;

/// <summary>
/// HTTP surface for the SheetSubmission module - timesheet submission over
/// <c>dbo.TimesheetSubmission</c>.
/// <para>
/// As thin as <see cref="TimeLogController"/>: bind, call
/// <see cref="ISheetSubmissionService"/>, shape the response. No rules, no role
/// checks, no data access.
/// </para>
/// <para>
/// <b>Unauthenticated for now</b>, like the rest of the API.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class SheetSubmissionController : ControllerBase
{
    private readonly ISheetSubmissionService _sheetSubmissionService;

    public SheetSubmissionController(ISheetSubmissionService sheetSubmissionService)
    {
        _sheetSubmissionService = sheetSubmissionService;
    }

    /// <summary>
    /// Submits a user's timesheet. The total is added up from their
    /// <b>saved</b> time log entries on the sheet - drafts and deleted entries
    /// do not count - and recorded in <c>dbo.TimesheetSubmission</c> with status
    /// 1 (Submitted), with a matching entry in
    /// <c>dbo.TimeSheetSubmissionAuditLog</c>.
    /// <para>
    /// A sheet is submitted once; a second submit is a 409. <c>orgId</c> is
    /// accepted but not used yet.
    /// </para>
    /// </summary>
    /// <response code="200">The submission as it was stored, with its totals.</response>
    /// <response code="400">The payload failed validation, or the user has no saved time on the sheet.</response>
    /// <response code="409">The sheet has already been submitted.</response>
    [HttpPost("submit-timesheet")]
    [ProducesResponseType(typeof(ApiResponse<SheetSubmissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitTimesheet(
        [FromBody] SheetSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sheetSubmissionService.SubmitAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Timesheet submitted."));
    }

    /// <summary>
    /// Approves or rejects a submitted timesheet. <c>status</c> is <c>2</c>
    /// (Approved) or <c>3</c> (Rejected); the sheet's row in
    /// <c>dbo.TimesheetSubmission</c> gets that status and the admin's id and
    /// the time as <c>approvedBy</c> / <c>approvedDate</c> or
    /// <c>rejectedBy</c> / <c>rejectedDate</c>, with a matching entry in
    /// <c>dbo.TimeSheetSubmissionAuditLog</c>.
    /// <para>
    /// A sheet can be reviewed any number of times - a rejected sheet can be
    /// approved later, and the reverse. The row shows only the latest decision;
    /// the audit log keeps every one. <c>adminId</c> is not checked against any
    /// role yet, and <c>orgId</c> is accepted but not used.
    /// </para>
    /// </summary>
    /// <response code="200">The submission as it now stands.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="404">No submission exists for the sheet code.</response>
    [HttpPost("review-timesheet")]
    [ProducesResponseType(typeof(ApiResponse<SheetSubmissionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewTimesheet(
        [FromBody] SheetSubmissionReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sheetSubmissionService.ReviewAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(
            result,
            request.Status == Constants.TimesheetSubmission.Status.Approved
                ? "Timesheet approved."
                : "Timesheet rejected."));
    }

    /// <summary>
    /// Returns the user's current timesheet week: its start and end dates as
    /// their admin setup defines them, its sheet code, and the hours worked,
    /// expected and drift (worked minus expected).
    /// <para>
    /// "Current" is today in the user's time zone. Hours worked count saved
    /// entries on the week's sheet only - what submit-timesheet would record.
    /// A user with no setup gets a Monday week with null expected and drift;
    /// one who has logged nothing this week gets a null <c>sheetCode</c> and
    /// <c>0</c> hours worked. <c>orgID</c> is accepted but not used yet.
    /// </para>
    /// </summary>
    /// <response code="200">The current week's details.</response>
    [HttpGet("get-current-week-sheet-details/{userID:int:min(1)}/{orgID:int}")]
    [ProducesResponseType(typeof(ApiResponse<CurrentWeekSheetDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentWeekSheetDetails(
        // Spelled userID / orgID to match the route tokens character for
        // character - see AdminController.GetUserTimesheetSetup for why.
        [FromRoute] int userID,
        [FromRoute] int orgID,
        CancellationToken cancellationToken)
    {
        var result = await _sheetSubmissionService.GetCurrentWeekSheetDetailsAsync(userID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Returns the submitted timesheets in an organisation, for an admin - newest
    /// first, one row per submission.
    /// <para>
    /// <c>userId</c> <c>0</c> lists every user's submissions; any other value
    /// lists that user's only. Each row carries who submitted it, the sheet's
    /// week and hours - as get-current-week-sheet-details does - and its status.
    /// </para>
    /// <para>
    /// Backed by <c>dbo.spc_GetSubmittedSheetList</c>, which still has
    /// placeholders: <c>statusText</c> names only status 1, and
    /// <c>adminId</c> is not checked.
    /// </para>
    /// </summary>
    /// <response code="200">The submitted sheets. Nothing submitted is an empty list, not a 404.</response>
    /// <response code="400">The payload failed validation.</response>
    [HttpPost("get-submitted-sheet-list")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyCollection<SubmittedSheetResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSubmittedSheetList(
        [FromBody] SubmittedSheetListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sheetSubmissionService.GetSubmittedSheetListAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }
}
