using ETimeSheet.Application.Common.Mapping;
using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Constants;
using ETimeSheet.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ETimeSheet.Application.Services.Implementations;

/// <summary>
/// Business logic for the Master Admin module. It reaches the database only
/// through <see cref="IMasterAdminRepository"/>, and never sees <c>Context</c>.
/// <para>
/// <b>No authorisation check</b>, because authentication is switched off for
/// this project for now - as on <c>AdminService</c>.
/// </para>
/// </summary>
public class MasterAdminService : IMasterAdminService
{
    /// <summary>
    /// <c>dbo.TimesheetClientSetup.Avatar</c> is <c>nvarchar(500)</c>, and
    /// <c>spc_AddEditClientSetup</c>'s <c>@PAvatar</c> is 500 too.
    /// </summary>
    private const int AvatarMaximumLength = 500;

    private readonly IMasterAdminRepository _masterAdminRepository;
    private readonly IDocumentUploader _documentUploader;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<MasterAdminService> _logger;

    public MasterAdminService(
        IMasterAdminRepository masterAdminRepository,
        IDocumentUploader documentUploader,
        IDateTimeProvider dateTimeProvider,
        ILogger<MasterAdminService> logger)
    {
        _masterAdminRepository = masterAdminRepository;
        _documentUploader = documentUploader;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClientResponse>> GetClientListAsync(
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        // Checked here rather than by a route constraint, so a caller who sends
        // 0 is told what is wrong with it - as on AdminService.
        if (organizationId <= 0)
        {
            throw new ValidationException(
                "orgID",
                "orgID is required and must be greater than 0.");
        }

        var clients = await _masterAdminRepository.GetClientListAsync(organizationId, cancellationToken);
        return clients.Select(client => client.ToClientResponse()).ToList();
    }

    public async Task<ClientResponse> SaveClientAsync(
        ClientSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // The procedure derives the currency from the country, and refuses a
        // country with no currency - but only with a SQL error, which would
        // reach the caller as a 500. Asked here first, so it is a 400 naming
        // the field. The validator has already made CountryId non-null.
        var countryId = request.CountryId!.Value;

        if (!await _masterAdminRepository.HasCurrencyForCountryAsync(countryId, cancellationToken))
        {
            throw new ValidationException(
                nameof(ClientSaveRequest.CountryId),
                $"Country '{countryId}' has no currency set up, so a client cannot be saved against it.");
        }

        // An edit of a client that is missing or deleted is refused BEFORE the
        // logo is uploaded, so a request that can only fail never leaves a
        // file behind on the document service. The client's current avatar is
        // read at the same time: the payload carries no avatar, and an edit
        // without a new logo must not clear the one already stored.
        string? currentAvatar = null;

        if (request.ClientId is > 0)
        {
            var existing = await _masterAdminRepository.GetByIdAsync(request.ClientId.Value, cancellationToken)
                ?? throw NotFoundException.For("Client", request.ClientId.Value);

            currentAvatar = existing.Avatar;
        }

        // One billing email per live client. Checked before the upload, like
        // the 404 above, so a refused save leaves no file behind. The client
        // being edited is excluded, so it can be saved with its own email.
        var billingEmail = request.BillingEmail!.Trim();

        if (await _masterAdminRepository.BillingEmailExistsAsync(
                billingEmail,
                request.ClientId is > 0 ? request.ClientId : null,
                cancellationToken))
        {
            throw new ConflictException(
                $"A client with the billing email '{billingEmail}' already exists.");
        }

        var avatar = request.ClientLogo is null
            ? currentAvatar
            : await UploadLogoAsync(request.ClientLogo, cancellationToken);

        // Add or edit is decided by ClientId inside spc_AddEditClientSetup,
        // which also generates the code for a new client. Only an edit can
        // come back empty - checked above, so this is a backstop for a client
        // removed in between.
        var saved = await _masterAdminRepository.SaveClientAsync(request, avatar, cancellationToken) ?? throw NotFoundException.For("Client", request.ClientId ?? 0);

        return saved.ToClientResponse();
    }

    public async Task RemoveClientAsync(
        ClientDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        // The repository only returns non-deleted clients, so removing one
        // twice is a 404 rather than a second stamp over the first DeleteDate.
        var client = await _masterAdminRepository.GetForUpdateAsync(request.ClientId, cancellationToken) ?? throw NotFoundException.For("Client", request.ClientId);

        // A soft delete, never a DELETE statement: the row stays, and the
        // entity's query filter and both procedures stop returning it.
        client.IsDeleted = true;
        client.DeleteDate = _dateTimeProvider.UtcNow;

        await _masterAdminRepository.UpdateAsync(client, cancellationToken);

        _logger.LogInformation("Client {ClientId} soft-deleted.", client.ClientId);
    }

    /// <summary>
    /// Uploads the client's logo under the <c>TSCLT</c> project code and
    /// returns the <c>path</c> to store in <c>Avatar</c>.
    /// </summary>
    /// <exception cref="ExternalServiceException">
    /// The upload failed, or the path it returned does not fit the column.
    /// </exception>
    private async Task<string> UploadLogoAsync(IFormFile logo, CancellationToken cancellationToken)
    {
        await using var content = logo.OpenReadStream();

        var reference = await _documentUploader.UploadAsync(
            DocumentProjectCodes.ClientSetup,
            content,
            logo.FileName,
            logo.ContentType,
            cancellationToken);

        // Truncating it would store a reference to a file that does not exist.
        if (reference.Length > AvatarMaximumLength)
        {
            throw new ExternalServiceException(
                $"The logo was uploaded, but its path is {reference.Length} characters long and the " +
                $"client's Avatar column holds at most {AvatarMaximumLength}. Nothing was saved.");
        }

        _logger.LogInformation(
            "Client logo {FileName} ({Length} bytes) uploaded as {Reference}.",
            logo.FileName,
            logo.Length,
            reference);

        return reference;
    }

    public async Task<IReadOnlyList<OrganizationResponse>> GetOrganizationListAsync(
        CancellationToken cancellationToken = default)
    {
        var organizations = await _masterAdminRepository.GetOrganizationListAsync(cancellationToken);
        return organizations.Select(organization => organization.ToOrganizationResponse()).ToList();
    }
}
