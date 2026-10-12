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
    /// Returns the dashboard summary for one organisation: total, full-time and
    /// part-time employees, this week's logged and expected time across all of
    /// them, and the pending / approved timesheet counts.
    /// <para>
    /// "This week" is each employee's own timesheet week, starting on their
    /// setup's <c>StartDay</c> (Monday when none). <c>pendingTimesheets</c> and
    /// <c>approvedTimesheets</c> are static placeholders - always 0 for now.
    /// </para>
    /// </summary>
    /// <param name="orgID">The organisation to summarise, as a route segment.</param>
    /// <response code="200">The summary. An organisation with nobody in it is all zeros, not a 404.</response>
    /// <response code="400">No organisation id, or one that is not greater than zero.</response>
    [HttpGet("get-admin-dashboard-summary-by-orgID/{orgID:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAdminDashboardSummaryByOrgId(
        // :int without :min(1), as on get-all-employees-by-orgid: a 0 has to
        // reach the service so it can answer 400 saying what is wrong.
        [FromRoute] int orgID,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetAdminDashboardSummaryByOrganizationIdAsync(orgID, cancellationToken);
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

    /// <summary>
    /// Adds a role to an organisation, or renames one - <b>one endpoint for
    /// both</b>.
    /// <para>
    /// Send <c>roleId</c> as <c>0</c> (or leave it out) to add a role to
    /// <c>organizationId</c>. Send an existing role's <c>roleId</c> to rename it;
    /// the role must belong to <c>organizationId</c> and not be deleted.
    /// </para>
    /// <para>
    /// The response carries the saved role, including its <c>roleId</c>.
    /// </para>
    /// </summary>
    /// <response code="200">The saved role.</response>
    /// <response code="400">The payload failed validation - every failing field is listed.</response>
    /// <response code="404">A <c>roleId</c> was sent, but no live role of that organisation has it.</response>
    /// <response code="409">The organisation already has another role with that name.</response>
    [HttpPost("add-edit-user-role")]
    [ProducesResponseType(typeof(ApiResponse<UserRoleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddEditUserRole(
        [FromBody] UserRoleSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.SaveUserRoleAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "User role saved."));
    }

    /// <summary>
    /// Returns the roles defined for one organisation, from
    /// <c>dbo.TimesheetUserRole</c>, ordered by name. Deleted roles
    /// (<c>IsDelete = 1</c>) are not included.
    /// </summary>
    /// <param name="orgID">The organisation whose roles to list, as a route segment.</param>
    /// <response code="200">The roles. An organisation with none is an empty list, not a 404.</response>
    /// <response code="400">An organisation id that is not greater than zero.</response>
    [HttpGet("get-user-role-list-by-orgid/{orgID:int}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserRoleResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetUserRoleListByOrgId(
        // :int without :min(1), as on get-all-employees-by-orgid: a 0 has to
        // reach the service so it can answer 400 saying what is wrong.
        [FromRoute] int orgID,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetUserRoleListByOrganizationIdAsync(orgID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Soft-deletes a role of an organisation. The row is kept, marked
    /// <c>IsDelete = 1</c> and stamped with who deleted it and when; nothing is
    /// ever physically deleted.
    /// <para>
    /// The role must belong to <c>organizationId</c>. A POST rather than an HTTP
    /// DELETE, matching <c>delete-timesheet-setup</c>: every argument in this
    /// API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The role was deleted.</response>
    /// <response code="400">A <c>roleId</c>, <c>organizationId</c> or <c>deletedBy</c> that is not greater than zero.</response>
    /// <response code="404">No live role with that id belongs to that organisation; deleting one twice is not a silent success.</response>
    [HttpPost("delete-user-role")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUserRole(
        [FromBody] UserRoleDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _adminService.DeleteUserRoleAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("User role deleted."));
    }

    /// <summary>
    /// Returns the project categories defined for one organisation, from
    /// <c>dbo.TimesheetProjectCategory</c>, ordered by name. Deleted categories
    /// (<c>IsDelete = 1</c>) are not included.
    /// </summary>
    /// <param name="orgID">The organisation whose categories to list, as a route segment.</param>
    /// <response code="200">The categories. An organisation with none is an empty list, not a 404.</response>
    /// <response code="400">An organisation id that is not greater than zero.</response>
    [HttpGet("get-timesheet-project-category-by-orgID/{orgID:int}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProjectCategoryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimesheetProjectCategoryByOrgId(
        // :int without :min(1), as on get-all-employees-by-orgid: a 0 has to
        // reach the service so it can answer 400 saying what is wrong.
        [FromRoute] int orgID,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.GetProjectCategoryListByOrganizationIdAsync(orgID, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Adds a project category to an organisation, or renames one - <b>one
    /// endpoint for both</b>.
    /// <para>
    /// Send <c>categoryId</c> as <c>0</c> (or leave it out) to add a category to
    /// <c>organizationId</c>. Send an existing category's <c>categoryId</c> to
    /// rename it; the category must belong to <c>organizationId</c> and not be
    /// deleted.
    /// </para>
    /// <para>
    /// The response carries the saved category, including its <c>categoryId</c>.
    /// </para>
    /// </summary>
    /// <response code="200">The saved category.</response>
    /// <response code="400">The payload failed validation - every failing field is listed.</response>
    /// <response code="404">A <c>categoryId</c> was sent, but no live category of that organisation has it.</response>
    [HttpPost("add-edit-timesheet-project-category")]
    [ProducesResponseType(typeof(ApiResponse<ProjectCategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEditTimesheetProjectCategory(
        [FromBody] ProjectCategorySaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.SaveProjectCategoryAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Project category saved."));
    }

    /// <summary>
    /// Soft-deletes a project category of an organisation. The row is kept,
    /// marked <c>IsDelete = 1</c> and stamped with who removed it and when;
    /// nothing is ever physically deleted.
    /// <para>
    /// The category must belong to <c>organizationId</c>. A POST rather than an
    /// HTTP DELETE, matching <c>delete-user-role</c>: every argument in this
    /// API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The category was removed.</response>
    /// <response code="400">A <c>categoryId</c>, <c>organizationId</c> or <c>deletedBy</c> that is not greater than zero.</response>
    /// <response code="404">No live category with that id belongs to that organisation; removing one twice is not a silent success.</response>
    [HttpPost("remove-timesheet-project-category")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTimesheetProjectCategory(
        [FromBody] ProjectCategoryDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _adminService.DeleteProjectCategoryAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Project category removed."));
    }
}
