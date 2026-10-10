using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access contract for the <c>dbo.EmailQueue</c> table. Every public
/// operation of <c>EmailQueueRepository</c> is declared here.
/// <para>
/// Not a feature module's repository - there is no EmailQueue controller or
/// service. The table is written by more than one module (the scheduler queues
/// emails, the Admin module closes them), so it has a repository of its own
/// rather than living inside either.
/// </para>
/// </summary>
public interface IEmailQueueRepository
{
    /// <summary>
    /// Inserts <paramref name="emails"/> and saves - one save, so one
    /// transaction: all of them are queued, or none.
    /// </summary>
    Task AddRangeAsync(
        IReadOnlyCollection<EmailQueue> emails,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <paramref name="userId"/>'s queued emails whose
    /// <c>EmailStatusID</c> is one of <paramref name="statuses"/>, <b>tracked</b>.
    /// <b>It does not save.</b> Changes made to the returned rows are written by
    /// the next save on the request's context - whichever repository makes it -
    /// in the same transaction as whatever else that save writes.
    /// </summary>
    Task<IReadOnlyList<EmailQueue>> GetForUpdateByUserIdAsync(
        int userId,
        IReadOnlyCollection<byte> statuses,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns when each email of <paramref name="emailTypeId"/> was queued for
    /// <paramref name="userIds"/> at or after <paramref name="sinceUtc"/>,
    /// whatever its status, as UTC instants.
    /// <para>
    /// <c>CreatedDate</c> is the database server's local time (its
    /// <c>GETDATE()</c> default); the implementation reads the server's offset
    /// and converts both ways, so callers deal in UTC only.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<QueuedEmail>> GetQueuedSinceAsync(
        int emailTypeId,
        IReadOnlyCollection<int> userIds,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns, <b>tracked</b> and oldest first, every email of one of
    /// <paramref name="emailTypeIds"/> whose status is one of
    /// <paramref name="statuses"/> and that has had fewer than
    /// <paramref name="maxAttempts"/> attempts. Save each one's outcome with
    /// <see cref="UpdateAsync"/>.
    /// </summary>
    Task<IReadOnlyList<EmailQueue>> GetSendableAsync(
        IReadOnlyCollection<byte> statuses,
        int maxAttempts,
        IReadOnlyCollection<int> emailTypeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Saves the changes made to an email from <see cref="GetSendableAsync"/>.</summary>
    Task UpdateAsync(
        EmailQueue email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The database server's current offset from UTC - what its
    /// <c>GETDATE()</c>, and so <c>CreatedDate</c>, is ahead of UTC by.
    /// </summary>
    Task<TimeSpan> GetServerUtcOffsetAsync(
        CancellationToken cancellationToken = default);
}
