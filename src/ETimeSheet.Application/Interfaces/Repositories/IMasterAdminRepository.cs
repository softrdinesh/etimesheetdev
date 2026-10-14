using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access contract for the Master Admin module, over
/// <c>dbo.TimesheetClientSetup</c>. Every public operation of
/// <c>MasterAdminRepository</c> is declared here.
/// </summary>
public interface IMasterAdminRepository
{
    /// <summary>
    /// Returns every live client, ordered by name, via
    /// <c>spc_GetClientListForMasterAdmin</c>. The procedure excludes
    /// soft-deleted clients (<c>IsDeleted = 1</c>).
    /// </summary>
    Task<IReadOnlyList<ClientListDetail>> GetClientListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or edits a client via <c>spc_AddEditClientSetup</c> and returns the
    /// saved row.
    /// <para>
    /// A <c>ClientId</c> of <see langword="null"/> or <c>0</c> adds, and the
    /// procedure generates the client code under a table lock, so two adds at
    /// once cannot share a code. Any other id edits that client.
    /// </para>
    /// </summary>
    /// <returns>
    /// The saved client, or <see langword="null"/> when an edit names a client
    /// that does not exist or is deleted.
    /// </returns>
    /// <param name="avatar">
    /// What to store in <c>Avatar</c> - the uploaded logo's path, or the
    /// client's current value when no logo was sent.
    /// </param>
    Task<ClientSaveDetail?> SaveClientAsync(
        ClientSaveRequest request,
        string? avatar,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one live client, <b>tracked</b> for editing, or
    /// <see langword="null"/> when no non-deleted client has that id.
    /// </summary>
    Task<TimesheetClientSetup?> GetForUpdateAsync(
        int clientId,
        CancellationToken cancellationToken = default);

    /// <summary>Saves the changes made to a client loaded by <see cref="GetForUpdateAsync"/>.</summary>
    Task UpdateAsync(
        TimesheetClientSetup client,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <c>dbo.CountryCurrency</c> has a currency for the country - the
    /// row <c>spc_AddEditClientSetup</c> derives a client's currency from.
    /// </summary>
    Task<bool> HasCurrencyForCountryAsync(
        int countryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one live client, untracked, or <see langword="null"/> when no
    /// non-deleted client has that id.
    /// </summary>
    Task<TimesheetClientSetup?> GetByIdAsync(
        int clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a live client other than <paramref name="excludeClientId"/>
    /// already has this billing email, ignoring case. Deleted clients do not
    /// count, so a removed client's email can be used again.
    /// </summary>
    Task<bool> BillingEmailExistsAsync(
        string billingEmail,
        int? excludeClientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every organisation, ordered by name, via
    /// <c>spc_GetOrganizationList</c>.
    /// </summary>
    Task<IReadOnlyList<OrganizationListDetail>> GetOrganizationListAsync(
        CancellationToken cancellationToken = default);
}
