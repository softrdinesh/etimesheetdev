using ETimeSheet.Application.Models.Entities;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access contract for <see cref="TimesheetMasterSetup"/> - the
/// administrative CRUD surface over <c>dbo.TimesheetMasterSetup</c>. Every
/// public operation of <c>AdminSetupRepository</c> is declared here.
/// <para>
/// Soft-deleted rows are invisible to every method here: the entity carries a
/// global query filter on <c>IsDelete</c>, so "not found" and "deleted" are the
/// same answer as far as this interface is concerned.
/// </para>
/// <para>
/// This interface answers only "what data" questions - never "is the caller
/// allowed to", and never "should this be an insert or an update".
/// </para>
/// </summary>
public interface IAdminSetupRepository
{
    /// <summary>
    /// Returns the live setup belonging to one user, untracked. Null when the
    /// user has none.
    /// <para>
    /// A user has at most one setup, so this returns a single row rather than a
    /// list. Should the data ever hold more than one - this is enforced by the
    /// service, not by a database constraint - the lowest <c>SetupID</c> wins,
    /// so the answer is at least deterministic.
    /// </para>
    /// </summary>
    Task<TimesheetMasterSetup?> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one setup row <b>tracked</b>, ready to be mutated and saved.
    /// Keyed by <c>SetupID</c>, because an edit and a delete both name the exact
    /// row they mean.
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
    /// How many live setup rows one user already has, ignoring
    /// <paramref name="excludingSetupId"/> when it is supplied.
    /// <para>
    /// This is what enforces one-setup-per-user. Counted in the database rather
    /// than by loading the rows: the caller only needs the number.
    /// </para>
    /// </summary>
    Task<int> CountForUserAsync(
        int userId,
        int? excludingSetupId = null,
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
    /// <see cref="GetForUpdateAsync"/>. Used for both an edit and a soft delete -
    /// a soft delete is an update, and deciding what makes a row "deleted" is
    /// the service's rule, not this layer's.
    /// </summary>
    Task UpdateAsync(
        TimesheetMasterSetup setup,
        CancellationToken cancellationToken = default);
}
