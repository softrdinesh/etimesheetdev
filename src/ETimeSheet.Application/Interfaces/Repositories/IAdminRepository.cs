using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access contract for <see cref="TimesheetMasterSetup"/> - the
/// administrative CRUD surface over <c>dbo.TimesheetMasterSetup</c>. Every
/// public operation of <c>AdminRepository</c> is declared here.
/// <para>
/// Soft-deleted rows are invisible to every method here except
/// <see cref="FindForSaveByUserIdAsync"/>, which exists precisely because the
/// save path has to see them. Everywhere else the entity's global query filter
/// on <c>IsDelete</c> makes "not found" and "deleted" the same answer.
/// </para>
/// <para>
/// This interface answers only "what data" questions - never "is the caller
/// allowed to", and never "should this be an insert or an update".
/// </para>
/// </summary>
public interface IAdminRepository
{
    /// <summary>
    /// Returns the live setup belonging to one user, untracked. Null when the
    /// user has none.
    /// <para>
    /// A user has at most one setup, so this returns a single row rather than a
    /// list. Should the data ever hold more than one - the save path makes that
    /// impossible, but no database constraint does - the lowest <c>SetupID</c>
    /// wins, so the answer is at least deterministic.
    /// </para>
    /// </summary>
    Task<TimesheetMasterSetup?> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the row a save should write to for one user, <b>tracked</b>,
    /// including a soft-deleted one. Null only when the user has never had a
    /// setup at all - which is the single case that means "insert".
    /// <para>
    /// <b>Query filters are ignored deliberately.</b> A user whose setup was
    /// deleted must not get a second row on their next save; they get the old
    /// one back, overwritten and undeleted. A live row always wins over a
    /// deleted one, so the ordering here is what makes that precedence a
    /// property of the data rather than of two separate round trips.
    /// </para>
    /// </summary>
    Task<TimesheetMasterSetup?> FindForSaveByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one live setup row <b>tracked</b>, ready to be mutated and saved.
    /// Keyed by <c>SetupID</c>, because a delete names the exact row it means.
    /// <para>
    /// Separate from the read above so that read paths never pay for change
    /// tracking, and so a write path cannot silently get an untracked entity
    /// whose changes would be dropped.
    /// </para>
    /// </summary>
    Task<TimesheetMasterSetup?> GetForUpdateAsync(
        int setupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new setup row and saves, returning the same instance with its
    /// database-generated <c>SetupID</c> populated.
    /// </summary>
    Task<TimesheetMasterSetup> AddAsync(
        TimesheetMasterSetup setup,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes made to a tracked entity obtained from
    /// <see cref="FindForSaveByUserIdAsync"/> or <see cref="GetForUpdateAsync"/>.
    /// Used for an edit, a revival and a soft delete alike - all three are an
    /// update, and deciding what makes a row "deleted" is the service's rule,
    /// not this layer's.
    /// </summary>
    Task UpdateAsync(
        TimesheetMasterSetup setup,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns everyone in one organisation, by executing
    /// <c>dbo.spc_GetEmployeeListByPOrgID</c>.
    /// <para>
    /// <b>"Everyone", not "every employee".</b> The procedure filtered
    /// <c>dbo.Signup</c> on <c>RoleID = 2</c> until 2026-09-22, when that
    /// predicate was commented out; it now selects on the organisation and
    /// <c>isdelete = 0</c> alone, so administrators and managers are in these
    /// rows too. The name of this method is the one it was given when the filter
    /// existed.
    /// </para>
    /// <para>
    /// The procedure computes the contracted and logged weekly time itself.
    /// Nothing here re-derives any of that; the rows come back as the procedure
    /// produced them.
    /// </para>
    /// <para>
    /// Somebody with no timesheet setup is <b>included</b>, with a null
    /// <c>SetupID</c>: the procedure LEFT JOINs the setup table. Counting those
    /// is the service's job, not this one's.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<EmployeeListDetail>> GetEmployeeListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one organisation's dashboard figures by executing
    /// <c>dbo.spc_GetAdminDashboardSummaryByOrgID</c>, or
    /// <see langword="null"/> if the procedure returns no row - which it is
    /// written never to do.
    /// </summary>
    Task<AdminDashboardSummaryDetail?> GetAdminDashboardSummaryByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);
}
