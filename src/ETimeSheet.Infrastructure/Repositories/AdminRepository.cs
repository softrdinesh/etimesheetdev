using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core data access for the Admin module, over the
/// existing <c>dbo.TimesheetMasterSetup</c> table.
/// <para>
/// Queries, filters and saves only. No permission checks, no insert-or-update
/// decision, no audit stamping - those are <c>AdminService</c>'s.
/// </para>
/// <para>
/// Every query below runs through the entity's global query filter, so
/// soft-deleted rows are excluded without a single method mentioning
/// <c>IsDelete</c> - with one deliberate exception,
/// <see cref="FindForSaveByUserIdAsync"/>, which the save path needs in order to
/// revive a deleted setup instead of duplicating it.
/// </para>
/// </summary>
public class AdminRepository : IAdminRepository
{
    private readonly Context _db;

    public AdminRepository(Context db)
    {
        _db = db;
    }

    public async Task<TimesheetMasterSetup?> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        await _db.TimesheetMasterSetup
            .AsNoTracking()
            .Where(setup => setup.UserId == userId)
            // Ordered, not just "first row the server happens to return":
            // nothing in the database stops a second row existing, and an
            // unordered FirstOrDefault could answer differently between calls.
            .OrderBy(setup => setup.SetupId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<TimesheetMasterSetup?> FindForSaveByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        await _db.TimesheetMasterSetup
            // The one query in this class that looks past the soft-delete
            // filter. Without IgnoreQueryFilters a user whose setup was deleted
            // would look like a user who never had one, and the save would
            // insert a second row for them.
            .IgnoreQueryFilters()
            .Where(setup => setup.UserId == userId)
            // Live rows first: false sorts before true, so a user who somehow
            // has both a live and a deleted row gets the live one updated rather
            // than the deleted one revived. Then by key, so the answer cannot
            // change between two identical calls.
            .OrderBy(setup => setup.IsDelete == true)
            .ThenBy(setup => setup.SetupId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<TimesheetMasterSetup?> GetForUpdateAsync(
        int setupId,
        CancellationToken cancellationToken = default) =>
        // Tracked deliberately: the caller mutates this instance and the change
        // tracker is what turns those mutations into an UPDATE statement. The
        // query filter still applies here, so an already-deleted row is not
        // found - deleting one twice is a 404, not a silent success.
        await _db.TimesheetMasterSetup
            .FirstOrDefaultAsync(setup => setup.SetupId == setupId, cancellationToken);

    public async Task<TimesheetMasterSetup> AddAsync(
        TimesheetMasterSetup setup,
        CancellationToken cancellationToken = default)
    {
        await _db.TimesheetMasterSetup.AddAsync(setup, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // SetupID is an identity column, so this is only populated after the
        // save - the caller needs it to tell the client what it created.
        return setup;
    }

    public async Task UpdateAsync(
        TimesheetMasterSetup setup,
        CancellationToken cancellationToken = default)
    {
        // The expected path is a tracked entity from GetForUpdateAsync, where
        // EF Core already knows which columns changed and writes only those.
        // Update() is called only for a detached instance, where there is no
        // before-image to compare against and every column has to be written -
        // calling it unconditionally would turn every edit into a full-row
        // update and overwrite columns the caller never touched.
        if (_db.Entry(setup).State == EntityState.Detached)
        {
            _db.TimesheetMasterSetup.Update(setup);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
