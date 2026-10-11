using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Services.Interfaces;

/// <summary>
/// Application contract for the Master Admin module - client setup held in
/// <c>dbo.TimesheetClientSetup</c>. Every public method of
/// <c>MasterAdminService</c> is declared here; its private helpers are not.
/// <para>
/// This is the only surface <c>MasterAdminController</c> is allowed to touch.
/// </para>
/// </summary>
public interface IMasterAdminService
{
    /// <summary>
    /// Returns every live client, ordered by name. No clients is an empty list,
    /// not a 404.
    /// </summary>
    Task<IReadOnlyList<ClientResponse>> GetClientListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or edits a client. <b>One method for both</b>, chosen by
    /// <see cref="ClientSaveRequest.ClientId"/>: absent or <c>0</c> adds a new
    /// client with the next generated code (<c>CL0001</c>, <c>CL0002</c> ...);
    /// any other value edits that client, leaving its code unchanged.
    /// </summary>
    /// <returns>The saved client, including its <c>ClientId</c> and <c>ClientCode</c>.</returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// A <c>ClientId</c> was sent, but no non-deleted client has it.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// Another live client already has that <c>BillingEmail</c>, ignoring case.
    /// </exception>
    Task<ClientResponse> SaveClientAsync(
        ClientSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a client: the row stays, marked <c>IsDeleted = 1</c> and
    /// stamped with <c>DeleteDate</c>.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live client has that id - removing an already-removed client is a
    /// 404, not a silent success.
    /// </exception>
    Task RemoveClientAsync(
        ClientDeleteRequest request,
        CancellationToken cancellationToken = default);
}
