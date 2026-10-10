using ETimeSheet.Shared.Enums;

namespace ETimeSheet.Application.Models;

// The Scheduler module's model: every shape SchedulerService exchanges, in
// one file - see CLAUDE.md §2. The module has no controller.

/// <summary>
/// A job's schedule, read from its <c>dbo.SchedulerConfiguration</c> row and
/// checked by <c>SchedulerService</c>: everything here is already known to be
/// usable for <see cref="Type"/>. <c>IBackgroundJobService</c> turns it into a
/// trigger.
/// </summary>
/// <param name="Type">How runs are spaced.</param>
/// <param name="StartsAt">
/// The first run, in <b>UTC</b> - <c>ScheduleDateTime</c> as stored. Always set except for
/// <see cref="ScheduleType.Repeated"/>, where null means "from now".
/// </param>
/// <param name="Interval">The gap between runs - <see cref="ScheduleType.Repeated"/> only.</param>
public sealed record JobSchedule(
    ScheduleType Type,
    DateTime? StartsAt,
    TimeSpan? Interval);

/// <summary>
/// A user who might be due a reminder: a live setup with reminders on and a
/// cut-off, belonging to a live user. Whether they are due one <i>now</i> -
/// their day, their clock, their logged time, their submission - is decided
/// by <c>SchedulerService</c>, per reminder.
/// </summary>
public class ReminderCandidate
{
    public int SetupId { get; set; }

    public int UserId { get; set; }

    public int? OrganizationId { get; set; }

    /// <summary><c>dbo.Signup.Email</c>.</summary>
    public string? Email { get; set; }

    /// <summary>The setup's IANA zone id; its clock is the one every rule reads.</summary>
    public string? TimeZone { get; set; }

    public int? StartDay { get; set; }

    public int? EndDay { get; set; }

    public int? ExceptionDay { get; set; }

    /// <summary>The daily time; the time log reminder needs it, the timesheet one does not.</summary>
    public TimeSpan? MaxTimeInHrs { get; set; }

    public TimeSpan? MaxTimInMins { get; set; }

    public TimeSpan TimeEntryLockAt { get; set; }

    public TimeSpan? ReminderTimeBeforeCutoff { get; set; }
}

/// <summary>
/// One complete <c>dbo.TimeLog</c> entry, as wall-clock instants on its
/// owner's clock - the only part of an entry the reminder needs.
/// </summary>
/// <param name="UserId">The entry's owner.</param>
/// <param name="StartsAt">Start date + start time.</param>
/// <param name="EndsAt">End date + end time.</param>
public sealed record LoggedSpan(int UserId, DateTime StartsAt, DateTime EndsAt);

/// <summary>
/// When an email of some type was queued for a user, as a UTC instant -
/// <c>dbo.EmailQueue.CreatedDate</c> is the database server's local time, and
/// the repository converts it.
/// </summary>
public sealed record QueuedEmail(int UserId, DateTime CreatedAtUtc);

/// <summary>
/// What a reminder email says about its user: their name, company, and the
/// setup its figures come from. Read when the email is sent, so the figures
/// are current.
/// </summary>
public class ReminderEmailDetails
{
    public int SetupId { get; set; }

    public int UserId { get; set; }

    /// <summary><c>dbo.Signup.Name</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The company: <c>dbo.Organization.OrganizationName</c> of the setup's
    /// <c>OrganizationID</c>. Null when that organisation does not exist.
    /// </summary>
    public string? OrganizationName { get; set; }

    /// <summary>The setup's IANA zone id.</summary>
    public string? TimeZone { get; set; }

    /// <summary>First day of the week, a <c>dbo.DayMaster.DayID</c>.</summary>
    public int? StartDay { get; set; }

    public TimeSpan? MaxTimeInHrs { get; set; }

    public TimeSpan? MaxTimInMins { get; set; }

    public TimeSpan? TimeEntryLockAt { get; set; }
}

/// <summary>
/// The date of one of a user's <c>dbo.TimeLog</c> entries whose sheet code has
/// been submitted - how the timesheet reminder tells a submitted week.
/// </summary>
public sealed record SubmittedEntry(int UserId, DateTime StartDate);

/// <summary>One email, ready to send - the email service's payload.</summary>
/// <param name="EmailAddress">Where it goes.</param>
/// <param name="Subject">Its subject line.</param>
/// <param name="BodyContent">Its HTML body, placeholders filled.</param>
public sealed record EmailMessage(string EmailAddress, string Subject, string BodyContent);

/// <summary>The outcome of one send. <see cref="Error"/> is set exactly when it failed.</summary>
public sealed record EmailSendResult(bool Succeeded, string? Error)
{
    public static EmailSendResult Success { get; } = new(true, null);

    public static EmailSendResult Failed(string error) => new(false, error);
}
