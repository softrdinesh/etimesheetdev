using ETimeSheet.Application.Models;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETimeSheet.Api.Controllers;

/// <summary>
/// HTTP surface for the Master Admin module - client setup held in
/// <c>dbo.TimesheetClientSetup</c>.
/// <para>
/// Thin: bind, call <see cref="IMasterAdminService"/>, shape the response.
/// </para>
/// <para>
/// <b>Unauthenticated for now</b>, like the rest of the API.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class MasterAdminController : ControllerBase
{
    private readonly IMasterAdminService _masterAdminService;

    public MasterAdminController(IMasterAdminService masterAdminService)
    {
        _masterAdminService = masterAdminService;
    }

    /// <summary>
    /// Returns every client, ordered by name. Deleted clients are not included.
    /// </summary>
    /// <response code="200">The clients. None is an empty list, not a 404.</response>
    [HttpGet("get-client-list")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClientResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClientList(
        CancellationToken cancellationToken)
    {
        var result = await _masterAdminService.GetClientListAsync(cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>
    /// Adds a client, or edits one - <b>one endpoint for both</b>.
    /// <para>
    /// Send <c>clientId</c> as <c>0</c> (or leave it out) to add: the client
    /// gets the next code in the sequence <c>CL0001</c>, <c>CL0002</c> ...,
    /// unique across every client ever added. Send an existing client's
    /// <c>clientId</c> to edit it; its code never changes. <c>clientCode</c> is
    /// never accepted in the payload.
    /// </para>
    /// <para>
    /// The response carries the saved client, including its <c>clientId</c> and
    /// <c>clientCode</c>.
    /// </para>
    /// <para>
    /// <b>Sent as <c>multipart/form-data</c></b>, so the optional
    /// <c>clientLogo</c> file can travel with the fields. When it is sent, it is
    /// uploaded to the document service under project code <c>TSCLT</c> and the
    /// returned reference is stored as the client's <c>avatar</c>.
    /// </para>
    /// </summary>
    /// <response code="200">The saved client.</response>
    /// <response code="400">The payload failed validation.</response>
    /// <response code="404">A <c>clientId</c> was sent, but no non-deleted client has it.</response>
    /// <response code="409">Another client already has that <c>billingEmail</c>; nothing was uploaded or saved.</response>
    /// <response code="502">The logo could not be uploaded to the document service; nothing was saved.</response>
    [HttpPost("add-or-edit-client-setup")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ClientResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> AddOrEditClientSetup(
        [FromForm] ClientSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _masterAdminService.SaveClientAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok(result, "Client setup saved."));
    }

    /// <summary>
    /// Soft-deletes a client. The row is kept, marked <c>IsDeleted = 1</c> and
    /// stamped with <c>DeleteDate</c>; nothing is ever physically deleted.
    /// <para>
    /// A POST rather than an HTTP DELETE, matching <c>delete-timesheet-setup</c>:
    /// every argument in this API travels in the payload.
    /// </para>
    /// </summary>
    /// <response code="200">The client was removed.</response>
    /// <response code="400">No <c>clientId</c>, or one that is not greater than zero.</response>
    /// <response code="404">No live client has that id; removing one twice is not a silent success.</response>
    [HttpPost("remove-client-setup")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveClientSetup(
        [FromBody] ClientDeleteRequest request,
        CancellationToken cancellationToken)
    {
        await _masterAdminService.RemoveClientAsync(request, cancellationToken);
        return Ok(ApiResponse.Ok("Client setup removed."));
    }
}
