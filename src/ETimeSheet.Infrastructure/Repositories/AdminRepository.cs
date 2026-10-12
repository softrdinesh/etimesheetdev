using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;
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

    public async Task<IReadOnlyList<EmployeeListDetail>> GetEmployeeListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // @POrgID is spelled exactly as the procedure declares it. The
        // interpolated hole becomes a real SqlParameter, so the value can never
        // be parsed as SQL.
        await _db.spc_GetEmployeeListByPOrgID
            .FromSqlInterpolated(
                $"EXEC dbo.spc_GetEmployeeListByPOrgID @POrgID = {organizationId}")
            .ToListAsync(cancellationToken);

    public async Task<AdminDashboardSummaryDetail?> GetAdminDashboardSummaryByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_GetAdminDashboardSummaryByOrgID
            .FromSqlInterpolated(
                $"EXEC dbo.spc_GetAdminDashboardSummaryByOrgID @POrgID = {organizationId}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<IReadOnlyList<UserRoleDetail>> GetUserRoleListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The procedure filters out deleted roles and does the ordering; no
        // LINQ is composed on top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetUserRoleListByOrgID
            .FromSqlInterpolated($"EXEC dbo.spc_GetUserRoleListByOrgID @POrgID = {organizationId}")
            .ToListAsync(cancellationToken);

    public async Task<bool> UserRoleNameExistsAsync(
        int organizationId,
        string roleName,
        int? excludeRoleId,
        CancellationToken cancellationToken = default) =>
        // The query filter drops deleted roles, so a deleted role's name is
        // free to reuse. The comparison runs in SQL Server under the column's
        // case-insensitive collation, which is what "ignoring case" means here.
        await _db.TimesheetUserRole
            .AsNoTracking()
            .AnyAsync(
                role => role.OrganizationId == organizationId
                    && role.RoleName == roleName
                    && role.RoleId != excludeRoleId,
                cancellationToken);

    public async Task<UserRoleSaveDetail?> SaveUserRoleAsync(
        int? roleId,
        int organizationId,
        string roleName,
        int createdBy,
        DateTime createDate,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditUserRoleByAdmin
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditUserRoleByAdmin
                @PRoleID = {roleId},
                @POrgID = {organizationId},
                @PRoleName = {roleName},
                @PCreatedBy = {createdBy},
                @PCreateDate = {createDate}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<TimesheetUserRole?> GetUserRoleForUpdateAsync(
        int roleId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter still
        // applies, so a role deleted already is not found, and the organisation
        // is part of the match, so another organisation's role is not either.
        await _db.TimesheetUserRole
            .FirstOrDefaultAsync(
                role => role.RoleId == roleId && role.OrganizationId == organizationId,
                cancellationToken);

    public async Task UpdateUserRoleAsync(
        TimesheetUserRole role,
        CancellationToken cancellationToken = default) =>
        await _db.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectCategoryDetail>> GetProjectCategoryListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // The procedure filters out deleted categories and does the ordering;
        // no LINQ is composed on top, because SQL Server cannot wrap an EXEC.
        await _db.spc_GetTimesheetProjectCategory.FromSqlInterpolated($"EXEC dbo.spc_GetTimesheetProjectCategory @POrgID = {organizationId}").ToListAsync(cancellationToken);

    public async Task<ProjectCategorySaveDetail?> SaveProjectCategoryAsync(
        int? categoryId,
        int organizationId,
        string categoryName,
        int createdBy,
        DateTime createDate,
        CancellationToken cancellationToken = default)
    {
        // Materialised as a list, then the first row taken in memory:
        // FirstOrDefault on the query would try to compose SELECT TOP(1) around
        // the EXEC, which SQL Server cannot do.
        var rows = await _db.spc_AddEditTimesheetProjectCategory
            .FromSqlInterpolated($@"EXEC dbo.spc_AddEditTimesheetProjectCategory
                @PCategoryID = {categoryId},
                @POrgID = {organizationId},
                @PCategoryName = {categoryName},
                @PCreatedBy = {createdBy},
                @PCreateDate = {createDate}")
            .ToListAsync(cancellationToken);

        return rows.FirstOrDefault();
    }

    public async Task<TimesheetProjectCategory?> GetProjectCategoryForUpdateAsync(
        int categoryId,
        int organizationId,
        CancellationToken cancellationToken = default) =>
        // Tracked on purpose - this is the write path. The query filter still
        // applies, so a category deleted already is not found, and the
        // organisation is part of the match, so another organisation's
        // category is not either.
        await _db.TimesheetProjectCategory
            .FirstOrDefaultAsync(
                category => category.CategoryId == categoryId && category.OrganizationId == organizationId,
                cancellationToken);

    public async Task UpdateProjectCategoryAsync(
        TimesheetProjectCategory category,
        CancellationToken cancellationToken = default) =>
        await _db.SaveChangesAsync(cancellationToken);
}
