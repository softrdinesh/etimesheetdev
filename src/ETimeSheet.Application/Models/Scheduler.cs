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
/// the repository converts it - and the project it concerns, when it names one.
/// </summary>
public sealed record QueuedEmail(int UserId, DateTime CreatedAtUtc, int? ProjectId);

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

    /// <summary>First day of the week, a <c>dbo.DayMaster_Lookup.DayID</c>.</summary>
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

/// <summary>
/// A project and its timesheet approver - one per project: the live
/// <c>dbo.Timesheet_Approver_setup</c> row with <c>IsTimesheetApprover = 1</c>,
/// joined to its project and to the approver's live <c>dbo.Signup</c> row.
/// Only projects with a start date and a known submission frequency are read
/// - without them there is no period to remind about. Whether a period of it
/// has just ended is decided by <c>SchedulerService</c>.
/// </summary>
public class ApproverReminderProject
{
    public int ApproverId { get; set; }

    /// <summary>The approver's <c>dbo.Signup.Name</c>.</summary>
    public string ApproverName { get; set; } = string.Empty;

    /// <summary>The approver's <c>dbo.Signup.Email</c>.</summary>
    public string? ApproverEmail { get; set; }

    public int ProjectId { get; set; }

    /// <summary><c>dbo.TimesheetProject.Code</c> - <c>PR0001</c>.</summary>
    public string ProjectCode { get; set; } = string.Empty;

    public string? ProjectName { get; set; }

    /// <summary>The project's organisation, or the approver setup's when the project has none.</summary>
    public int? OrganizationId { get; set; }

    /// <summary>That organisation's name - the email's sign-off. Null when it does not exist.</summary>
    public string? OrganizationName { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    /// <summary>A <c>Constants.TimesheetProject.SubmissionFrequency</c> value.</summary>
    public int SubmissionFrequency { get; set; }
}

/// <summary>
/// One user's hours on one project for one day, under one sheet code - the
/// live <c>dbo.TimesheetProjectTimeLog</c> rows summed in the database.
/// </summary>
public sealed record ProjectLoggedDay(
    int ProjectId,
    int UserId,
    DateTime LogDate,
    string SheetSubmissionCode,
    int HoursLogged);

/// <summary>A live <c>dbo.TimesheetProjectSubmission</c> row, as the approver reminder reads it.</summary>
public sealed record ProjectSheetSubmissionRow(
    int ProjectId,
    int UserId,
    DateTime PeriodStartDate,
    string SheetSubmissionCode);

/// <summary>
/// A sheet the <c>AutoApproveProjectSheet</c> job may be due to approve: a
/// live <c>dbo.TimesheetProjectSubmission</c> still <c>Submitted</c>, on a
/// project that has an <c>AutoApproveInDays</c>.
/// <para>
/// Whether it is actually due - whether <see cref="SubmittedDate"/> plus
/// <see cref="AutoApproveInDays"/> has passed - is decided by
/// <c>SchedulerService</c>, not by the query.
/// </para>
/// </summary>
public sealed record AutoApproveCandidate(
    int TimesheetProjectSubmissionId,
    int ProjectId,
    int UserId,
    string SheetSubmissionCode,
    DateTime? SubmittedDate,
    int AutoApproveInDays);

/// <summary>A user's id and <c>dbo.Signup.Name</c>.</summary>
public sealed record UserName(int UserId, string Name);

/// <summary>
/// A user's working week and daily time, from their live
/// <c>dbo.TimesheetMasterSetup</c> (the lowest <c>SetupID</c>) - what the
/// approver reminder's expected hours are worked out from.
/// </summary>
public class WorkingWeekSetup
{
    public int SetupId { get; set; }

    public int UserId { get; set; }

    /// <summary>First working day, a <c>dbo.DayMaster_Lookup.DayID</c>.</summary>
    public int? StartDay { get; set; }

    /// <summary>Last working day, a <c>dbo.DayMaster_Lookup.DayID</c>.</summary>
    public int? EndDay { get; set; }

    /// <summary>An extra working day outside <see cref="StartDay"/>..<see cref="EndDay"/>.</summary>
    public int? ExceptionDay { get; set; }

    public TimeSpan? MaxTimeInHrs { get; set; }

    public TimeSpan? MaxTimInMins { get; set; }
}
