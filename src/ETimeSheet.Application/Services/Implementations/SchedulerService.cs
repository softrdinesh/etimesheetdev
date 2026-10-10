using System.Globalization;
using System.Net;
using ETimeSheet.Application.Common;
using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Configuration;
using ETimeSheet.Shared.Constants;
using ETimeSheet.Shared.Enums;
using ETimeSheet.Shared.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETimeSheet.Application.Services.Implementations;

/// <summary>
/// Business logic for the Scheduler module. It reaches the database only
/// through <see cref="ISchedulerRepository"/> and
/// <see cref="IEmailQueueRepository"/>, and Quartz only through
/// <see cref="IBackgroundJobService"/>.
/// <para>
/// <b>What a configuration row means is decided here</b> - which columns each
/// schedule type needs, and what a row missing one of them does (its job does
/// not run). The Infrastructure side only turns an already-checked
/// <see cref="JobSchedule"/> into a trigger.
/// </para>
/// </summary>
public class SchedulerService : ISchedulerService
{
    private readonly ISchedulerRepository _schedulerRepository;
    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateStore _emailTemplateStore;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly EmailServiceSettings _emailServiceSettings;
    private readonly ILogger<SchedulerService> _logger;

    public SchedulerService(
        ISchedulerRepository schedulerRepository,
        IEmailQueueRepository emailQueueRepository,
        IEmailSender emailSender,
        IEmailTemplateStore emailTemplateStore,
        IBackgroundJobService backgroundJobService,
        IDateTimeProvider dateTimeProvider,
        IOptions<EmailServiceSettings> emailServiceSettings,
        ILogger<SchedulerService> logger)
    {
        _schedulerRepository = schedulerRepository;
        _emailQueueRepository = emailQueueRepository;
        _emailSender = emailSender;
        _emailTemplateStore = emailTemplateStore;
        _backgroundJobService = backgroundJobService;
        _dateTimeProvider = dateTimeProvider;
        _emailServiceSettings = emailServiceSettings.Value;
        _logger = logger;
    }

    public async Task StartScheduledJobsAsync(CancellationToken cancellationToken = default)
    {
        // One query for every registered job, not one per job.
        var configurations = await _schedulerRepository.GetByIdsAsync(SchedulerJobs.Registered, cancellationToken);

        // ScheduleDateTime is UTC, so it is compared with UTC.
        var now = DateTime.SpecifyKind(_dateTimeProvider.UtcNow, DateTimeKind.Unspecified);

        foreach (var jobId in SchedulerJobs.Registered)
        {
            // One job's bad row, or a failure scheduling it, must not keep the
            // other jobs from starting.
            try
            {
                var configuration = configurations.FirstOrDefault(row => row.SchedulerConfigurationId == jobId);

                if (configuration is null)
                {
                    _logger.LogWarning(
                        "Job {JobId} will not run: dbo.SchedulerConfiguration has no row with that id.", jobId);
                    continue;
                }

                if (!configuration.IsEnabled)
                {
                    _logger.LogInformation(
                        "Job {JobId} ({SchedulerName}) is disabled; it will not run.",
                        jobId, configuration.SchedulerName);
                    continue;
                }

                var schedule = ToSchedule(configuration, now);

                if (schedule is null)
                {
                    continue;
                }

                await _backgroundJobService.ScheduleAsync(jobId, schedule, cancellationToken);

                _logger.LogInformation(
                    "Job {JobId} ({SchedulerName}) scheduled: {Schedule}.",
                    jobId, configuration.SchedulerName, schedule);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Job {JobId} could not be scheduled.", jobId);
            }
        }
    }

    // ---- queueing reminders -----------------------------------------------

    public async Task<int> RunTimeLogReminderEmailQueueAsync(CancellationToken cancellationToken = default)
    {
        var eligible = await GetEligibleTimeLogReminderUsersAsync(cancellationToken);

        return await QueueAsync(eligible, Constants.EmailType.Id.TimeLogReminder, "Time log reminder", cancellationToken);
    }

    public async Task<int> RunTimesheetReminderEmailQueueAsync(CancellationToken cancellationToken = default)
    {
        var eligible = await GetEligibleTimesheetReminderUsersAsync(cancellationToken);

        return await QueueAsync(
            eligible, Constants.EmailType.Id.SheetSubmissionReminder, "Timesheet reminder", cancellationToken);
    }

    /// <summary>Queues one Pending email of <paramref name="emailTypeId"/> per user, in one save.</summary>
    private async Task<int> QueueAsync(
        IReadOnlyList<ReminderCandidate> users,
        int emailTypeId,
        string reminderName,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            _logger.LogDebug("{Reminder} email queue: nobody is due one.", reminderName);
            return 0;
        }

        var emails = users
            .Select(user => new EmailQueue
            {
                UserId = user.UserId,
                EmailAddress = user.Email!.Trim(),
                OrgId = user.OrganizationId,
                EmailTypeId = emailTypeId,
                EmailStatusId = Constants.EmailQueue.Status.Pending,
                AttemptCount = 0

                // CreatedDate is left to the column's GETDATE() default.
            })
            .ToList();

        await _emailQueueRepository.AddRangeAsync(emails, cancellationToken);

        _logger.LogInformation("{Reminder} email queue: {Count} email(s) queued.", reminderName, emails.Count);

        return emails.Count;
    }

    /// <summary><c>dbo.EmailQueue.EmailAddress</c> is <c>varchar(255)</c>.</summary>
    private const int EmailAddressMaximumLength = 255;

    /// <summary>
    /// The users due a <b>time log</b> reminder right now: the rules every
    /// reminder shares (see <see cref="DueRemindersAsync"/>), plus
    /// <list type="bullet">
    /// <item>today is one of their working days - inside <c>StartDay</c>..<c>EndDay</c>,
    /// or their exception day; a setup with no usable week imposes no
    /// constraint, as on the time log save;</item>
    /// <item>they have a daily time, and have logged less than it today.</item>
    /// </list>
    /// </summary>
    private async Task<IReadOnlyList<ReminderCandidate>> GetEligibleTimeLogReminderUsersAsync(
        CancellationToken cancellationToken)
    {
        var due = await DueRemindersAsync(
            Constants.EmailType.Id.TimeLogReminder,
            reminder => IsWorkingDay(reminder.Today, reminder.Candidate) && ExpectedMinutesOf(reminder.Candidate) > 0,
            cancellationToken);

        if (due.Count == 0)
        {
            return Array.Empty<ReminderCandidate>();
        }

        var logged = (await _schedulerRepository.GetLoggedSpansAsync(
                due.Select(reminder => reminder.Candidate.UserId).ToList(),
                due.Min(reminder => reminder.Today),
                due.Max(reminder => reminder.Today),
                cancellationToken))
            .ToLookup(span => span.UserId);

        return due
            .Where(reminder =>
                LoggedMinutesOn(reminder.Today, logged[reminder.Candidate.UserId]) < ExpectedMinutesOf(reminder.Candidate))
            .Select(reminder => reminder.Candidate)
            .ToList();
    }

    /// <summary>
    /// The users due a <b>timesheet submission</b> reminder right now: the
    /// rules every reminder shares (see <see cref="DueRemindersAsync"/>), plus
    /// <list type="bullet">
    /// <item>today is the last day of their week - their <c>EndDay</c>. A setup
    /// with no usable week has no last day, so is never reminded;</item>
    /// <item>this week's timesheet is not submitted yet - none of their entries
    /// dated this week carries a sheet code that is in
    /// <c>dbo.TimesheetSubmission</c>, whatever its status.</item>
    /// </list>
    /// </summary>
    private async Task<IReadOnlyList<ReminderCandidate>> GetEligibleTimesheetReminderUsersAsync(
        CancellationToken cancellationToken)
    {
        var due = await DueRemindersAsync(
            Constants.EmailType.Id.SheetSubmissionReminder,
            reminder => IsLastDayOfWeek(reminder.Today, reminder.Candidate),
            cancellationToken);

        if (due.Count == 0)
        {
            return Array.Empty<ReminderCandidate>();
        }

        var submitted = (await _schedulerRepository.GetSubmittedEntriesAsync(
                due.Select(reminder => reminder.Candidate.UserId).ToList(),
                due.Min(reminder => WeekStartOf(reminder.Today, reminder.Candidate.StartDay)),
                due.Max(reminder => reminder.Today),
                cancellationToken))
            .ToLookup(entry => entry.UserId);

        return due
            .Where(reminder =>
            {
                var weekStarts = WeekStartOf(reminder.Today, reminder.Candidate.StartDay);

                return !submitted[reminder.Candidate.UserId].Any(entry =>
                    entry.StartDate.Date >= weekStarts && entry.StartDate.Date <= reminder.Today);
            })
            .Select(reminder => reminder.Candidate)
            .ToList();
    }

    /// <summary>
    /// The rules every reminder shares, each judged on <b>the employee's own
    /// clock</b> - the zone their setup names - never the server's:
    /// <list type="number">
    /// <item>Reminders are on for them (<c>NeedToSendReminder = 1</c>), they have
    /// a cut-off, and neither their setup nor their user is deleted - the
    /// repository's part.</item>
    /// <item>They have an address the queue can hold.</item>
    /// <item>Their reminder time has come: the cut-off minus
    /// <c>ReminderTimeBeforeCutoff</c> - a 20:00 cut-off with a one-hour lead
    /// is 19:00. At 18:59 on their clock they are not due; from 19:00 they are.</item>
    /// <item><paramref name="dayRule"/> - the reminder's own rule about today.</item>
    /// <item>No email of <paramref name="emailTypeId"/> has been queued for them
    /// today already, so a job running every few minutes queues one per day.</item>
    /// </list>
    /// </summary>
    private async Task<List<DueReminder>> DueRemindersAsync(
        int emailTypeId,
        Func<DueReminder, bool> dayRule,
        CancellationToken cancellationToken)
    {
        var candidates = await _schedulerRepository.GetReminderCandidatesAsync(cancellationToken);
        var utcNow = _dateTimeProvider.UtcNow;
        var due = new List<DueReminder>();

        foreach (var candidate in candidates)
        {
            var email = candidate.Email?.Trim();

            if (string.IsNullOrEmpty(email) || email.Length > EmailAddressMaximumLength)
            {
                _logger.LogWarning(
                    "Reminders: user {UserId} has no usable email address; nothing is queued for them.",
                    candidate.UserId);
                continue;
            }

            var zone = ZoneFor(candidate);
            var clock = EmployeeClock.At(utcNow, zone);

            if (clock.TimeOfDay < ReminderTimeOf(candidate))
            {
                continue;
            }

            var reminder = new DueReminder(candidate, clock.Today, StartOfDayUtc(clock.Today, zone));

            if (dayRule(reminder))
            {
                due.Add(reminder);
            }
        }

        if (due.Count == 0)
        {
            return due;
        }

        var queued = (await _emailQueueRepository.GetQueuedSinceAsync(
                emailTypeId,
                due.Select(reminder => reminder.Candidate.UserId).ToList(),
                due.Min(reminder => reminder.TodayStartsAtUtc),
                cancellationToken))
            .ToLookup(queuedEmail => queuedEmail.UserId);

        return due
            .Where(reminder => !queued[reminder.Candidate.UserId]
                .Any(queuedEmail => queuedEmail.CreatedAtUtc >= reminder.TodayStartsAtUtc))
            .ToList();
    }

    /// <summary>A candidate whose reminder time has come, on their own clock.</summary>
    private sealed record DueReminder(
        ReminderCandidate Candidate,
        DateTime Today,
        DateTime TodayStartsAtUtc);

    private TimeZoneInfo? ZoneFor(ReminderCandidate candidate)
    {
        var zone = EmployeeClock.FindZone(candidate.TimeZone);

        if (zone is null && !string.IsNullOrWhiteSpace(candidate.TimeZone))
        {
            _logger.LogWarning(
                "Timesheet setup {SetupId} for user {UserId} names time zone {TimeZone}, which this system " +
                "does not recognise. Their reminders are judged on UTC.",
                candidate.SetupId, candidate.UserId, candidate.TimeZone);
        }

        return zone;
    }

    /// <summary>The same working-day rule the time log save applies.</summary>
    private static bool IsWorkingDay(DateTime today, ReminderCandidate candidate)
    {
        if (TimesheetWeek.Parse(candidate.StartDay) is not { } weekStarts ||
            TimesheetWeek.Parse(candidate.EndDay) is not { } weekEnds)
        {
            return true;
        }

        return TimesheetWeek.Span(weekStarts, weekEnds).Contains(today.DayOfWeek)
            || TimesheetWeek.Parse(candidate.ExceptionDay) == today.DayOfWeek;
    }

    /// <summary>Whether today is the setup's <c>EndDay</c>. No usable <c>EndDay</c>, no last day.</summary>
    private static bool IsLastDayOfWeek(DateTime today, ReminderCandidate candidate) =>
        TimesheetWeek.Parse(candidate.EndDay) is { } weekEnds && today.DayOfWeek == weekEnds;

    /// <summary>
    /// The first day of the week <paramref name="date"/> is in, from the setup's
    /// <c>StartDay</c> - Monday when there is none - exactly as the time log
    /// save groups a week's entries under one sheet code.
    /// </summary>
    private static DateTime WeekStartOf(DateTime date, int? startDay) =>
        TimesheetWeek.StartOf(date.Date, TimesheetWeek.Parse(startDay) ?? DayOfWeek.Monday);

    /// <summary>
    /// The cut-off minus the lead time. No lead time means "at the cut-off"; a
    /// lead time longer than the cut-off stops at midnight rather than wrapping
    /// into yesterday.
    /// </summary>
    private static TimeSpan ReminderTimeOf(ReminderCandidate candidate)
    {
        var lead = candidate.ReminderTimeBeforeCutoff ?? TimeSpan.Zero;

        return lead >= candidate.TimeEntryLockAt
            ? TimeSpan.Zero
            : candidate.TimeEntryLockAt - lead;
    }

    /// <summary>
    /// The setup's daily time in minutes: <c>MaxTimeinhrs</c> hours and minutes,
    /// plus the minutes of <c>MaxTiminmins</c> - the same figure the dashboard
    /// procedures compute. 0 when there is no daily time.
    /// </summary>
    private static int ExpectedMinutesOf(ReminderCandidate candidate) =>
        candidate.MaxTimeInHrs is { } maxTimeInHrs
            ? ExpectedMinutesOf(maxTimeInHrs, candidate.MaxTimInMins)
            : 0;

    private static int ExpectedMinutesOf(TimeSpan maxTimeInHrs, TimeSpan? maxTimInMins) =>
        maxTimeInHrs.Hours * 60 + maxTimeInHrs.Minutes + (maxTimInMins?.Minutes ?? 0);

    /// <summary>
    /// Minutes of <paramref name="spans"/> that fall on <paramref name="today"/>.
    /// An entry crossing midnight counts only its part inside the day, as the
    /// dashboard counts it.
    /// </summary>
    private static int LoggedMinutesOn(DateTime today, IEnumerable<LoggedSpan> spans)
    {
        var dayStarts = today.Date;
        var dayEnds = dayStarts.AddDays(1);

        return (int)spans
            .Where(span => span.EndsAt > span.StartsAt && span.EndsAt > dayStarts && span.StartsAt < dayEnds)
            .Sum(span => ((span.EndsAt < dayEnds ? span.EndsAt : dayEnds)
                          - (span.StartsAt > dayStarts ? span.StartsAt : dayStarts)).TotalMinutes);
    }

    /// <summary>
    /// The UTC instant the employee's <paramref name="today"/> began. A
    /// midnight that daylight saving skips falls back to the first hour after it.
    /// </summary>
    private static DateTime StartOfDayUtc(DateTime today, TimeZoneInfo? zone)
    {
        var midnight = DateTime.SpecifyKind(today.Date, DateTimeKind.Unspecified);

        if (zone is null)
        {
            return DateTime.SpecifyKind(midnight, DateTimeKind.Utc);
        }

        if (zone.IsInvalidTime(midnight))
        {
            midnight = midnight.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(midnight, zone);
    }

    // ---- sending ----------------------------------------------------------

    /// <summary>
    /// The email types the <c>SendEmail</c> job can build, with their template
    /// and subject. A type missing here is never picked up - it waits in the
    /// queue until it is added.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, (string TemplateName, string Subject)> EmailTemplates =
        new Dictionary<int, (string, string)>
        {
            [Constants.EmailType.Id.TimeLogReminder] = ("TimeLogTemplate.html", "Time Log Entry Reminder"),
            [Constants.EmailType.Id.SheetSubmissionReminder] = ("SheetSubmissionTemplate.html", "Timesheet Submission Reminder")
        };

    /// <summary>
    /// The statuses an email is sent from: waiting, or failed with attempts
    /// left. <c>Sent</c> - including one closed by a setup delete - is done;
    /// <c>Processing</c> is not used by this job.
    /// </summary>
    private static readonly byte[] SendableStatuses =
    {
        Constants.EmailQueue.Status.Pending,
        Constants.EmailQueue.Status.Error
    };

    /// <summary><c>dbo.EmailQueue.ErrorMessage</c> is <c>varchar(2000)</c>.</summary>
    private const int ErrorMessageMaximumLength = 2000;

    public async Task<int> RunSendEmailAsync(CancellationToken cancellationToken = default)
    {
        var emails = await _emailQueueRepository.GetSendableAsync(
            SendableStatuses,
            _emailServiceSettings.MaxAttempts,
            EmailTemplates.Keys.ToList(),
            cancellationToken);

        if (emails.Count == 0)
        {
            _logger.LogDebug("Send email: nothing to send.");
            return 0;
        }

        // The row's dates are the database server's local time, like its
        // CreatedDate default - so the attempt is stamped on that clock too.
        var serverOffset = await _emailQueueRepository.GetServerUtcOffsetAsync(cancellationToken);

        // Everything the emails say, read once for all of them.
        var context = await ReminderEmailContextAsync(emails, serverOffset, cancellationToken);

        var sent = 0;

        // One by one, and each outcome saved before the next send: a run that
        // stops halfway never sends an email twice.
        foreach (var email in emails)
        {
            string? error;

            try
            {
                var message = await BuildMessageAsync(email, context, cancellationToken);
                error = message.Error ?? (await _emailSender.SendAsync(message.Message!, cancellationToken)).Error;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                error = exception.Message;
            }

            var attemptedAt = DateTime.SpecifyKind(_dateTimeProvider.UtcNow, DateTimeKind.Unspecified) + serverOffset;

            email.AttemptCount++;
            email.LastAttemptDate = attemptedAt;

            if (error is null)
            {
                email.EmailStatusId = Constants.EmailQueue.Status.Sent;
                email.SentDate = attemptedAt;
                email.ErrorMessage = null;
                sent++;
            }
            else
            {
                // Stays Error. With attempts left it is tried again on the next
                // run; at MaxAttempts it is no longer picked up.
                email.EmailStatusId = Constants.EmailQueue.Status.Error;
                email.ErrorMessage = error.Length > ErrorMessageMaximumLength
                    ? error[..ErrorMessageMaximumLength]
                    : error;

                _logger.LogWarning(
                    "Send email: email {EmailQueueId} failed, attempt {Attempt} of {MaxAttempts}: {Error}",
                    email.EmailQueueId, email.AttemptCount, _emailServiceSettings.MaxAttempts, email.ErrorMessage);
            }

            await _emailQueueRepository.UpdateAsync(email, cancellationToken);
        }

        _logger.LogInformation(
            "Send email: {Sent} of {Total} email(s) sent.", sent, emails.Count);

        return sent;
    }

    /// <summary>What every email in a run needs, read in one go.</summary>
    private sealed record ReminderEmailContext(
        IReadOnlyDictionary<int, ReminderEmailDetails> Details,
        ILookup<int, LoggedSpan> Logged,
        TimeSpan ServerOffset);

    private async Task<ReminderEmailContext> ReminderEmailContextAsync(
        IReadOnlyCollection<EmailQueue> emails,
        TimeSpan serverOffset,
        CancellationToken cancellationToken)
    {
        var userIds = UserIdsOf(emails, _ => true);

        var details = userIds.Count == 0
            ? new Dictionary<int, ReminderEmailDetails>()
            : (await _schedulerRepository.GetReminderEmailDetailsAsync(userIds, cancellationToken))
                .ToDictionary(detail => detail.UserId);

        // Logged time is only in the time log reminder. The day an email is
        // about is the day it was queued, on its user's clock; a day either
        // side covers every zone.
        var timeLogUserIds = UserIdsOf(emails, email => email.EmailTypeId == Constants.EmailType.Id.TimeLogReminder);

        var logged = timeLogUserIds.Count == 0
            ? Array.Empty<LoggedSpan>().ToLookup(span => span.UserId)
            : (await _schedulerRepository.GetLoggedSpansAsync(
                    timeLogUserIds,
                    emails.Min(email => email.CreatedDate).Date.AddDays(-1),
                    DateTime.SpecifyKind(_dateTimeProvider.UtcNow, DateTimeKind.Unspecified).Date.AddDays(1),
                    cancellationToken))
                .ToLookup(span => span.UserId);

        return new ReminderEmailContext(details, logged, serverOffset);
    }

    private static List<int> UserIdsOf(IEnumerable<EmailQueue> emails, Func<EmailQueue, bool> predicate) =>
        emails
            .Where(email => email.UserId is not null && predicate(email))
            .Select(email => email.UserId!.Value)
            .Distinct()
            .ToList();

    /// <summary>
    /// Fills the email's template. Returns the message, or - when it cannot be
    /// built - why, which is recorded against the email like a failed send.
    /// </summary>
    private async Task<(EmailMessage? Message, string? Error)> BuildMessageAsync(
        EmailQueue email,
        ReminderEmailContext context,
        CancellationToken cancellationToken)
    {
        var (templateName, subject) = EmailTemplates[email.EmailTypeId];

        if (email.UserId is not { } userId || !context.Details.TryGetValue(userId, out var details))
        {
            return (null, "The user has no live timesheet setup, or their user record is deleted.");
        }

        // The day the email is about: the day it was queued, on the user's
        // clock. CreatedDate is the database server's local time.
        var zone = EmployeeClock.FindZone(details.TimeZone);
        var queuedAtUtc = DateTime.SpecifyKind(email.CreatedDate - context.ServerOffset, DateTimeKind.Utc);
        var queuedOn = EmployeeClock.At(queuedAtUtc, zone).Today;

        var values = email.EmailTypeId switch
        {
            Constants.EmailType.Id.TimeLogReminder => TimeLogReminderValues(details, queuedOn, context.Logged[userId]),
            Constants.EmailType.Id.SheetSubmissionReminder => (SheetSubmissionReminderValues(details, queuedOn), null),
            _ => (null, $"Email type {email.EmailTypeId} has no template values.")
        };

        if (values.Error is not null)
        {
            return (null, values.Error);
        }

        var body = await _emailTemplateStore.GetAsync(templateName, cancellationToken);

        // Every value is HTML-encoded: a name with a '<' in it is text, not markup.
        foreach (var (placeholder, value) in values.Values!)
        {
            body = body.Replace($"[{placeholder}]", WebUtility.HtmlEncode(value), StringComparison.Ordinal);
        }

        return (new EmailMessage(email.EmailAddress.Trim(), subject, body), null);
    }

    /// <summary>
    /// The placeholders of <c>TimeLogTemplate.html</c>. Figures are read now,
    /// so <c>[LoggedHours]</c> is what the user has logged by the time the
    /// email goes out. Every date and time is on the user's clock, and no zone
    /// is named.
    /// </summary>
    private static (IReadOnlyDictionary<string, string>? Values, string? Error) TimeLogReminderValues(
        ReminderEmailDetails details,
        DateTime queuedOn,
        IEnumerable<LoggedSpan> logged)
    {
        if (details.MaxTimeInHrs is not { } maxTimeInHrs)
        {
            return (null, "The user's timesheet setup has no daily time (MaxTimeinhrs).");
        }

        var companyName = details.OrganizationName?.Trim() ?? string.Empty;

        return (new Dictionary<string, string>
        {
            ["UserName"] = details.Name.Trim(),

            // No project is known for a time log reminder, so the timesheet
            // is named after the company.
            ["ProjectName"] = companyName,
            ["TimeLogDate"] = FormatDate(queuedOn),
            ["ExpectedHours"] = FormatHours(ExpectedMinutesOf(maxTimeInHrs, details.MaxTimInMins)),
            ["LoggedHours"] = FormatHours(LoggedMinutesOn(queuedOn, logged)),
            ["TimeEntryLockAt"] = Deadline(queuedOn, details.TimeEntryLockAt),
            ["CompanyName"] = companyName
        }, null);
    }

    /// <summary>
    /// The placeholders of <c>SheetSubmissionTemplate.html</c>. The email is
    /// queued on the last day of the user's week, so that day ends the period
    /// and its cut-off is the deadline - on the user's clock, no zone named.
    /// </summary>
    private static IReadOnlyDictionary<string, string> SheetSubmissionReminderValues(
        ReminderEmailDetails details,
        DateTime queuedOn) =>
        new Dictionary<string, string>
        {
            ["UserName"] = details.Name.Trim(),

            // Reads "... submit your timesheet for the week 06 Oct 2026 - 10 Oct 2026".
            ["Period"] = $"week {FormatDate(WeekStartOf(queuedOn, details.StartDay))} - {FormatDate(queuedOn)}",
            ["DueDateTime"] = Deadline(queuedOn, details.TimeEntryLockAt),
            ["CompanyName"] = details.OrganizationName?.Trim() ?? string.Empty
        };

    /// <summary>"08 Oct 2026 08:00 PM" - the date with the cut-off, or the date alone with no cut-off.</summary>
    private static string Deadline(DateTime date, TimeSpan? cutoff) =>
        cutoff is { } time
            ? $"{FormatDate(date)} {DateTime.MinValue.Add(time).ToString("hh:mm tt", CultureInfo.InvariantCulture)}"
            : FormatDate(date);

    /// <summary>"08 Oct 2026" - culture-invariant, so the server's locale never changes an email.</summary>
    private static string FormatDate(DateTime date) =>
        date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"8h 30m", or "8h" when there are no minutes - as the dashboard writes it.</summary>
    private static string FormatHours(int minutes) =>
        minutes % 60 > 0 ? $"{minutes / 60}h {minutes % 60}m" : $"{minutes / 60}h";

    /// <summary>
    /// Reads a row as a <see cref="JobSchedule"/>, or returns
    /// <see langword="null"/> - and says why in the log - when the row cannot
    /// run: a column its type needs is missing, or a one-off run is already in
    /// the past.
    /// <para>
    /// The repeat columns are read for <see cref="ScheduleType.Repeated"/> only.
    /// Daily, weekly and monthly jobs run once at their time; the repeat columns
    /// on those rows are ignored.
    /// </para>
    /// </summary>
    private JobSchedule? ToSchedule(SchedulerConfiguration row, DateTime now)
    {
        var startsAt = row.ScheduleDateTime is { } value
            ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified)
            : (DateTime?)null;

        switch (row.ScheduleTypeId)
        {
            case ScheduleType.Once:
                if (startsAt is null)
                {
                    return Unusable(row, "a one-off schedule needs ScheduleDateTime");
                }

                if (startsAt <= now)
                {
                    // Already run, or missed while the API was down. Not run on
                    // startup instead: with an in-memory store, that would run it
                    // again after every restart.
                    return Unusable(row, $"its one-off run at {startsAt:yyyy-MM-dd HH:mm:ss} has passed");
                }

                return new JobSchedule(ScheduleType.Once, startsAt, null);

            case ScheduleType.Repeated:
                if (!row.IsRepeatEnabled)
                {
                    return Unusable(row, "a repeated schedule needs IsRepeatEnabled = 1");
                }

                var interval = IntervalOf(row);

                return interval is null
                    ? Unusable(row, "a repeated schedule needs RepeatInterval > 0 and RepeatIntervalType H, M or S")
                    : new JobSchedule(ScheduleType.Repeated, startsAt, interval);

            case ScheduleType.Daily:
            case ScheduleType.Weekly:
            case ScheduleType.Monthly:
                return startsAt is null
                    ? Unusable(row, $"a {row.ScheduleTypeId} schedule needs ScheduleDateTime for its time of day")
                    : new JobSchedule(row.ScheduleTypeId, startsAt, null);

            default:
                return Unusable(row, $"ScheduleTypeID {(byte)row.ScheduleTypeId} is not a known schedule type");
        }
    }

    private static TimeSpan? IntervalOf(SchedulerConfiguration row)
    {
        if (row.RepeatInterval is not ({ } count and > 0))
        {
            return null;
        }

        // char(1) under a case-insensitive collation: 'm' passes the table's
        // check constraint, so it is accepted here too.
        return row.RepeatIntervalType?.Trim().ToUpperInvariant() switch
        {
            "H" => TimeSpan.FromHours(count),
            "M" => TimeSpan.FromMinutes(count),
            "S" => TimeSpan.FromSeconds(count),
            _ => null
        };
    }

    private JobSchedule? Unusable(SchedulerConfiguration row, string reason)
    {
        _logger.LogWarning(
            "Job {JobId} ({SchedulerName}) will not run: {Reason}.",
            row.SchedulerConfigurationId, row.SchedulerName, reason);
        return null;
    }
}
