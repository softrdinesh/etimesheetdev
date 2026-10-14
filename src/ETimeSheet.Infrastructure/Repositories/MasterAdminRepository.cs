using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;
using ETimeSheet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core data access for the Master Admin module, over the
/// existing <c>dbo.TimesheetClientSetup</c> table.
/// <para>
/// The list and the add/edit go through stored procedures; the soft delete goes
/// through the <c>TimesheetClientSetup</c> entity. No permission checks -
/// those are <c>MasterAdminService</c>'s.
/// </para>
/// </summary>
public class MasterAdminRepository : IMasterAdminRepository
{
    private readonly Context _db;

    public MasterAdminRepository(Context db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ClientListDetail>> GetClientListAsync(
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The procedure filters by organisation, drops deleted clients and does
        // the ordering; no LINQ is composed on top, because SQL Server cannot
        // wrap an EXEC.
        await _db.spc_GetClientListForMasterAdmin.FromSqlInterpolated($"EXEC dbo.spc_GetClientListForMasterAdmin @POrgID = {organizationId}").ToListAsync(cancellationToken);

    public async Task<ClientSaveDetail?> SaveClientAsync(
        ClientSaveRequest request,
        string? avatar,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do. The interpolated holes become
        // SqlParameters, and a null becomes DBNull.
        var rows = await _db.spc_AddEditClientSetup
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditClientSetup
                @PClientID = {request.ClientId},
                @PClientName = {request.ClientName},
                @PAvatar = {avatar},
                @PBillingEmail = {request.BillingEmail},
                @PPhoneNumber = {request.PhoneNumber},
                @PAlternatePhoneNumber = {request.AlternatePhoneNumber},
                @PAlternateEmail = {request.AlternateEmail},
                @PBillingAddress = {request.BillingAddress},
                @PPaymentTermsDays = {request.PaymentTermsDays},
                @PCountryID = {request.CountryId},
                @PTaxIdentifier = {request.TaxIdentifier},
                @POrgID = {request.OrganizationId}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<TimesheetClientSetup?> GetForUpdateAsync(
        int clientId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter still
        // applies, so a client that is already deleted is not found.
        await _db.TimesheetClientSetup
            .FirstOrDefaultAsync(client => client.ClientId == clientId, cancellationToken);

    public async Task UpdateAsync(
        TimesheetClientSetup client,
        CancellationToken cancellationToken = default) =>
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<bool> HasCurrencyForCountryAsync(
        int countryId,
        CancellationToken cancellationToken = default) =>
        await _db.CountryCurrency
            .AsNoTracking()
            .AnyAsync(currency => currency.CountryId == countryId, cancellationToken);

    public async Task<TimesheetClientSetup?> GetByIdAsync(
        int clientId,
        CancellationToken cancellationToken = default) =>
        // AsNoTracking: a read. The query filter drops deleted clients.
        await _db.TimesheetClientSetup
            .AsNoTracking()
            .FirstOrDefaultAsync(client => client.ClientId == clientId, cancellationToken);

    public async Task<bool> BillingEmailExistsAsync(
        string billingEmail,
        int? excludeClientId,
        CancellationToken cancellationToken = default) =>
        // The query filter drops deleted clients. The comparison runs in SQL
        // Server under the column's case-insensitive collation, which is what
        // makes "A@x.com" and "a@x.com" the same address here.
        await _db.TimesheetClientSetup
            .AsNoTracking()
            .AnyAsync(
                client => client.BillingEmail == billingEmail
                    && client.ClientId != excludeClientId,
                cancellationToken);

    public async Task<IReadOnlyList<OrganizationListDetail>> GetOrganizationListAsync(
        CancellationToken cancellationToken = default) =>
        // The procedure does the ordering; no LINQ is composed on top, because
        // SQL Server cannot wrap an EXEC.
        await _db.spc_GetOrganizationList.FromSqlInterpolated($"EXEC dbo.spc_GetOrganizationList").ToListAsync(cancellationToken);
}
