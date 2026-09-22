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
    /// <para>
    /// <b><c>countryId</c> and <c>timeZone</c> are stored exactly as sent.</b>
    /// Neither is looked up or derived - <c>dbo.Country</c> is not read on this
    /// path at all - so whatever the payload carries is what the row ends up
    /// with. <c>countryId</c> is required; <c>timeZone</c> is optional, and a
    /// blank one is rejected rather than stored.
    /// </para>
    /// <para>
    /// It follows that <b>nothing stops a setup holding a zone its country does
    /// not have</b>. The caller owns that consistency;
    /// <c>get-country-list-with-timezones</c> is there to build the choice from.
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
    /// <para>
    /// <b>A user with no setup is a 200, not a 404</b> - <c>success: true</c>
    /// with <c>data: null</c> and a message saying so, including when theirs was
    /// soft-deleted. Finding an unconfigured employee is one of the reasons to
    /// call this, and it is an answer rather than an error.
    /// </para>
    /// <para>
    /// The country comes back three ways: <c>countryId</c> as stored,
    /// <c>countryName</c> looked up from <c>dbo.Country</c>, and
    /// <c>countryWithTimeZone</c> - the name and the zone joined as
    /// <c>"United States-America/New_York"</c>. That last one holds the same
    /// string <c>get-country-list-with-timezones</c> returns as its
    /// <c>optionValue</c> for the same pairing, so a screen can preselect its
    /// country picker with one comparison instead of reassembling the label.
    /// </para>
    /// <para>
    /// <c>countryName</c> and <c>countryWithTimeZone</c> are <b>derived on the
    /// way out</b> and stored nowhere. A setup naming a country that does not
    /// exist - which the save permits, since it stores <c>countryId</c>
    /// unchecked - comes back with a null <c>countryName</c> and the bare zone
    /// as its <c>countryWithTimeZone</c>, rather than an error.
    /// </para>
    /// </summary>
    /// <response code="200">
    /// The user's setup, or <c>null</c> when they have none. Check <c>data</c>,
    /// not the status code.
    /// </response>
    [HttpGet("get-user-timesheet-setup/{userID:int:min(1)}")]
    [ProducesResponseType(typeof(ApiResponse<AdminResponse>), StatusCodes.Status200OK)]
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

        // The message is what distinguishes "nothing there" from "here it is",
        // now that both are success: true.
        return Ok(result is null
            ? ApiResponse.Ok(result, "This user has no timesheet setup.")
            : ApiResponse.Ok(result));
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
    /// The organisation whose employees to list, as a route segment.
    /// </param>
    /// <response code="200">The employees and their totals. An organisation with nobody in it is an empty grid, not a 404.</response>
    /// <response code="400">No organisation id, or one that is not greater than zero.</response>
    [HttpGet("get-all-employees-by-orgid/{orgID:int}")]
    [ProducesResponseType(typeof(ApiResponse<EmployeeListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAllEmployeesByOrgId(
        // Spelled orgID, matching the route token character for character - see
        // GetUserTimesheetSetup above for why the casing matters in Swagger UI.
        //
        // Constrained to :int but deliberately not :min(1): a 0 has to reach the
        // service so it can answer 400 saying what is wrong, rather than missing
        // the route and coming back as a 401 from the fallback policy.
        [FromRoute] int orgID,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetEmployeeListByOrganizationIdAsync(orgID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Returns every country paired with each of its time zones - the list a
    /// setup screen's country picker binds to.
    /// <para>
    /// <b>One entry per time zone, not one per country.</b>
    /// <c>dbo.Country.TimeZone</c> holds a country's IANA zones comma-separated,
    /// and this unpacks the whole lookup: the United Kingdom is one entry, the
    /// United States is twenty-nine, and all of them repeat the <b>same</b>
    /// <c>countryId</c>.
    /// </para>
    /// <para>
    /// Each entry carries the parts <b>and</b> the label: <c>countryId</c>,
    /// <c>countryName</c> and <c>timeZone</c> on their own, plus
    /// <c>optionValue</c> - the two joined with a hyphen,
    /// <c>"United States-America/New_York"</c> - for a dropdown to display. So
    /// a client shows <c>optionValue</c> and sends <c>countryId</c> and
    /// <c>timeZone</c> straight back to <c>save-user-timesheet-setup</c>,
    /// without taking a label apart.
    /// </para>
    /// <para>
    /// It takes no parameter, and that is the point: this replaced a read that
    /// wanted a country id, which could only be called once a country had
    /// already been chosen. The client needs the list in order to build the
    /// choice, so it is one call at screen load rather than one per click.
    /// </para>
    /// <para>
    /// A country with no zone recorded is <b>absent</b> from the list:
    /// <c>save-user-timesheet-setup</c> refuses one, and a picker should only
    /// hold answers that work. Countries come back ordered by name, and each
    /// country's zones in the order its column lists them - so the first entry
    /// for a country is its primary zone.
    /// </para>
    /// </summary>
    /// <response code="200">Every country/time-zone pairing.</response>
    [HttpGet("get-country-list-with-timezones")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CountryTimeZoneResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCountryListWithTimeZones(
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetCountryListWithTimeZonesAsync(cancellationToken);
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
