using System.Globalization;
using System.Net;
using System.Text;
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

    public async Task<int> RunApproverReminderEmailQueueAsync(CancellationToken cancellationToken = default)
    {
        // The job runs once a day; the day is its UTC date. ScheduleDateTime
        // is UTC too, so the row's time is set to the UTC equivalent of 7 AM
        // where the approvers are - 01:30 for 07:00 IST.
        var runDate = _dateTimeProvider.UtcToday.Date;

        var digests = await ApproverReminderDigestsAsync(new[] { runDate }, null, cancellationToken);

        if (digests.Count == 0)
        {
            _logger.LogDebug(
                "Approver reminder email queue: no project has submitted timesheets in a period that ended yesterday.");
            return 0;
        }

        var due = new List<ApproverReminderDigest>();

        foreach (var digest in digests.Values)
        {
            var email = digest.ApproverEmail?.Trim();

            if (string.IsNullOrEmpty(email) || email.Length > EmailAddressMaximumLength)
            {
                _logger.LogWarning(
                    "Approver reminders: approver {ApproverId} of project {ProjectId} has no usable email address; " +
                    "nothing is queued for them.",
                    digest.ApproverId, digest.Project.ProjectId);
                continue;
            }

            due.Add(digest);
        }

        if (due.Count == 0)
        {
            return 0;
        }

        // One reminder per approver, project and day: a second run on the same
        // day - a manual trigger, a changed row - queues nothing twice.
        var todayStartsAtUtc = DateTime.SpecifyKind(runDate, DateTimeKind.Utc);

        var alreadyQueued = (await _emailQueueRepository.GetQueuedSinceAsync(
                Constants.EmailType.Id.ApproverSubmissionReminder,
                due.Select(digest => digest.ApproverId).Distinct().ToList(),
                todayStartsAtUtc,
                cancellationToken))
            .Where(queued => queued.ProjectId is not null)
            .Select(queued => (queued.UserId, queued.ProjectId!.Value))
            .ToHashSet();

        var emails = due
            .Where(digest => !alreadyQueued.Contains((digest.ApproverId, digest.Project.ProjectId)))
            .Select(digest => new EmailQueue
            {
                UserId = digest.ApproverId,
                EmailAddress = digest.ApproverEmail!.Trim(),
                OrgId = digest.Project.OrganizationId,
                ProjectId = digest.Project.ProjectId,
                EmailTypeId = Constants.EmailType.Id.ApproverSubmissionReminder,
                EmailStatusId = Constants.EmailQueue.Status.Pending,
                AttemptCount = 0

                // CreatedDate is left to the column's GETDATE() default. The
                // SendEmail job reads the day back from it, and rebuilds the
                // project's grid from live data for the period that ended the
                // day before.
            })
            .ToList();

        if (emails.Count == 0)
        {
            _logger.LogDebug("Approver reminder email queue: every reminder due today was already queued.");
            return 0;
        }

        await _emailQueueRepository.AddRangeAsync(emails, cancellationToken);

        _logger.LogInformation("Approver reminder email queue: {Count} email(s) queued.", emails.Count);

        return emails.Count;
    }

    /// <summary>
    /// The <c>ErrorMessage</c> of an approver reminder closed without sending:
    /// by the time it was sent, none of its project's sheets for the period
    /// was still Submitted.
    /// </summary>
    private const string NoSubmittedSheetsLeftMessage = "No submitted timesheets left for the period";

    /// <summary>
    /// One submitted sheet in an approver reminder grid.
    /// <see cref="ExpectedMinutes"/> is null when the user has no setup to
    /// work it out from.
    /// </summary>
    private sealed record ApproverReminderRow(
        int UserId,
        string UserName,
        string SheetSubmissionCode,
        int? ExpectedMinutes,
        int HoursLogged);

    /// <summary>
    /// Everything one approver reminder says: one project, the period of it
    /// that ended the day before <see cref="RunDate"/>, and the sheets of that
    /// period still awaiting approval.
    /// </summary>
    private sealed record ApproverReminderDigest(
        int ApproverId,
        string ApproverName,
        string? ApproverEmail,
        ApproverReminderProject Project,
        DateTime RunDate,
        DateTime PeriodStart,
        DateTime PeriodEnd,
        IReadOnlyList<ApproverReminderRow> Rows);

    /// <summary>
    /// The approver reminders due on each of <paramref name="runDates"/>, one
    /// per approver and project, keyed by approver, project and day - used both
    /// to decide what is queued and, later, to build what each email says, so
    /// the email always shows live data.
    /// <para>
    /// A project is reminded on a day when the day before it is the <b>last
    /// day</b> of one of its submission periods
    /// (<see cref="ProjectSubmissionPeriod"/>): weekly periods ending on day 7
    /// are reminded on day 8, a monthly period from the 1st is reminded on the
    /// 1st of the next month. A period starting after the project's end date
    /// is not reminded.
    /// </para>
    /// <para>
    /// Only sheets of that period whose status is <b>Submitted</b> are listed -
    /// not approved, rejected, pending or never-submitted ones. A project with
    /// none is left out entirely.
    /// </para>
    /// </summary>
    /// <param name="runDates">The days the reminders are for.</param>
    /// <param name="projectIds">Only these projects; null for all of them.</param>
    private async Task<IReadOnlyDictionary<(int ApproverId, int ProjectId, DateTime RunDate), ApproverReminderDigest>>
        ApproverReminderDigestsAsync(
            IReadOnlyCollection<DateTime> runDates,
            IReadOnlyCollection<int>? projectIds,
            CancellationToken cancellationToken)
    {
        var projects = (await _schedulerRepository.GetApproverReminderProjectsAsync(cancellationToken))
            .Where(project => projectIds is null || projectIds.Contains(project.ProjectId))
            .ToList();

        // Every (approver, project, day) whose period ended the day before.
        var ended = new List<(ApproverReminderProject Project, DateTime RunDate, DateTime Start, DateTime End)>();

        foreach (var runDate in runDates.Select(date => date.Date).Distinct())
        {
            var periodEnd = runDate.AddDays(-1);

            foreach (var project in projects)
            {
                var projectStartDate = project.StartDate.Date;

                if (periodEnd < projectStartDate)
                {
                    continue;
                }

                var (start, end) = ProjectSubmissionPeriod.Of(periodEnd, projectStartDate, project.SubmissionFrequency);

                if (end != periodEnd || (project.EndDate is { } projectEndDate && start > projectEndDate.Date))
                {
                    continue;
                }

                ended.Add((project, runDate, start, end));
            }
        }

        if (ended.Count == 0)
        {
            return new Dictionary<(int, int, DateTime), ApproverReminderDigest>();
        }

        // One read of each kind for every project and period together.
        var endedProjectIds = ended.Select(item => item.Project.ProjectId).Distinct().ToList();
        var fromDate = ended.Min(item => item.Start);
        var toDate = ended.Max(item => item.End);

        var submissions = (await _schedulerRepository.GetProjectSubmissionsAsync(
                endedProjectIds,
                Constants.TimesheetProjectSubmission.Status.Submitted,
                fromDate,
                toDate,
                cancellationToken))
            .ToLookup(submission => submission.ProjectId);

        if (submissions.Count == 0)
        {
            return new Dictionary<(int, int, DateTime), ApproverReminderDigest>();
        }

        var loggedDays = (await _schedulerRepository.GetProjectLoggedDaysAsync(
                endedProjectIds, fromDate, toDate, cancellationToken))
            .ToLookup(day => day.ProjectId);

        // Per project and period: each submitted sheet, with the hours its
        // user logged on the project in the period.
        var periodSheets = new Dictionary<(int ProjectId, DateTime Start), List<(int UserId, string Code, int Hours)>>();

        foreach (var (project, _, start, end) in ended)
        {
            var key = (project.ProjectId, start);

            if (periodSheets.ContainsKey(key))
            {
                continue;
            }

            var hours = loggedDays[project.ProjectId]
                .Where(day => day.LogDate.Date >= start && day.LogDate.Date <= end)
                .GroupBy(day => day.UserId)
                .ToDictionary(days => days.Key, days => days.Sum(day => day.HoursLogged));

            periodSheets[key] = submissions[project.ProjectId]
                .Where(submission => submission.PeriodStartDate.Date == start)
                .GroupBy(submission => submission.UserId)
                .Select(sheets => sheets.OrderBy(sheet => sheet.SheetSubmissionCode, StringComparer.Ordinal).First())
                .Select(sheet => (
                    sheet.UserId,
                    sheet.SheetSubmissionCode.Trim(),
                    hours.TryGetValue(sheet.UserId, out var logged) ? logged : 0))
                .ToList();
        }

        var userIds = periodSheets.Values.SelectMany(sheets => sheets.Select(sheet => sheet.UserId)).Distinct().ToList();

        var names = userIds.Count == 0
            ? new Dictionary<int, string>()
            : (await _schedulerRepository.GetUserNamesAsync(userIds, cancellationToken))
                .ToDictionary(user => user.UserId, user => user.Name.Trim());

        var workingWeeks = userIds.Count == 0
            ? new Dictionary<int, WorkingWeekSetup>()
            : (await _schedulerRepository.GetWorkingWeekSetupsAsync(userIds, cancellationToken))
                .ToDictionary(setup => setup.UserId);

        var digests = new Dictionary<(int, int, DateTime), ApproverReminderDigest>();

        foreach (var (project, runDate, start, end) in ended)
        {
            var rows = periodSheets[(project.ProjectId, start)]
                .Select(sheet => new ApproverReminderRow(
                    sheet.UserId,
                    names.TryGetValue(sheet.UserId, out var name) ? name : string.Empty,
                    sheet.Code,
                    workingWeeks.TryGetValue(sheet.UserId, out var workingWeek)
                        ? ExpectedMinutesInPeriod(workingWeek, start, end)
                        : null,
                    sheet.Hours))
                .OrderBy(row => row.UserName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.UserId)
                .ToList();

            if (rows.Count == 0)
            {
                continue;
            }

            digests[(project.ApproverId, project.ProjectId, runDate)] = new ApproverReminderDigest(
                project.ApproverId,
                project.ApproverName.Trim(),
                project.ApproverEmail,
                project,
                runDate,
                start,
                end,
                rows);
        }

        return digests;
    }

    /// <summary>
    /// The minutes <paramref name="setup"/> expects from
    /// <paramref name="periodStart"/> to <paramref name="periodEnd"/>
    /// inclusive: its daily time - <c>MaxTimeinhrs</c> plus the minutes of
    /// <c>MaxTiminmins</c>, as the time log reminder reads it - times its
    /// working days in the period, which are <c>StartDay</c>..<c>EndDay</c>
    /// and its exception day, as the time log save counts them.
    /// <para>
    /// Null when the setup has no daily time or no usable working week: an
    /// expectation it cannot state is shown as unknown, not as zero.
    /// Holidays are not taken off.
    /// </para>
    /// </summary>
    private static int? ExpectedMinutesInPeriod(WorkingWeekSetup setup, DateTime periodStart, DateTime periodEnd)
    {
        if (setup.MaxTimeInHrs is not { } maxTimeInHrs
            || TimesheetWeek.Parse(setup.StartDay) is not { } weekStarts
            || TimesheetWeek.Parse(setup.EndDay) is not { } weekEnds)
        {
            return null;
        }

        var dailyMinutes = ExpectedMinutesOf(maxTimeInHrs, setup.MaxTimInMins);

        if (dailyMinutes <= 0)
        {
            return null;
        }

        var workingDays = TimesheetWeek.Span(weekStarts, weekEnds).ToHashSet();

        if (TimesheetWeek.Parse(setup.ExceptionDay) is { } exceptionDay)
        {
            workingDays.Add(exceptionDay);
        }

        var days = 0;

        for (var date = periodStart.Date; date <= periodEnd.Date; date = date.AddDays(1))
        {
            if (workingDays.Contains(date.DayOfWeek))
            {
                days++;
            }
        }

        return days * dailyMinutes;
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
            [Constants.EmailType.Id.SheetSubmissionReminder] = ("SheetSubmissionTemplate.html", "Timesheet Submission Reminder"),
            [Constants.EmailType.Id.ApproverSubmissionReminder] = ("ApproverSubmissionReminderTemplate.html", "Timesheet Approval Reminder")
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
            string? closedReason = null;

            try
            {
                var built = await BuildMessageAsync(email, context, cancellationToken);
                closedReason = built.ClosedReason;
                error = built.Error
                        ?? (closedReason is null
                            ? (await _emailSender.SendAsync(built.Message!, cancellationToken)).Error
                            : null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                error = exception.Message;
            }

            if (closedReason is not null)
            {
                // Nothing left to say, so nothing is sent: closed the way a
                // setup delete closes an email - Sent, with SentDate left NULL
                // and the reason in ErrorMessage. Not an attempt, and never
                // retried.
                email.EmailStatusId = Constants.EmailQueue.Status.Sent;
                email.SentDate = null;
                email.ErrorMessage = closedReason;

                _logger.LogInformation(
                    "Send email: email {EmailQueueId} closed without sending: {Reason}.",
                    email.EmailQueueId, closedReason);

                await _emailQueueRepository.UpdateAsync(email, cancellationToken);
                continue;
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
        IReadOnlyDictionary<(int ApproverId, int ProjectId, DateTime RunDate), ApproverReminderDigest> ApproverDigests,
        TimeSpan ServerOffset);

    private async Task<ReminderEmailContext> ReminderEmailContextAsync(
        IReadOnlyCollection<EmailQueue> emails,
        TimeSpan serverOffset,
        CancellationToken cancellationToken)
    {
        // An approver needs no timesheet setup of their own, so their
        // reminders are built from the approver digest instead.
        var userIds = UserIdsOf(emails, email => email.EmailTypeId != Constants.EmailType.Id.ApproverSubmissionReminder);

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

        // An approver reminder is about its project's period that ended the
        // day before it was queued - its UTC date, the date the queue job ran
        // on - and is rebuilt here from live data.
        var approverEmails = emails
            .Where(email => email.EmailTypeId == Constants.EmailType.Id.ApproverSubmissionReminder
                && email.UserId is not null
                && email.ProjectId is not null)
            .ToList();

        var approverDigests = approverEmails.Count == 0
            ? new Dictionary<(int, int, DateTime), ApproverReminderDigest>()
            : await ApproverReminderDigestsAsync(
                approverEmails.Select(email => ApproverRunDateOf(email, serverOffset)).Distinct().ToList(),
                approverEmails.Select(email => email.ProjectId!.Value).Distinct().ToList(),
                cancellationToken);

        return new ReminderEmailContext(details, logged, approverDigests, serverOffset);
    }

    /// <summary>The UTC date an email was queued on - <c>CreatedDate</c> is the database server's local time.</summary>
    private static DateTime ApproverRunDateOf(EmailQueue email, TimeSpan serverOffset) =>
        (email.CreatedDate - serverOffset).Date;

    private static List<int> UserIdsOf(IEnumerable<EmailQueue> emails, Func<EmailQueue, bool> predicate) =>
        emails
            .Where(email => email.UserId is not null && predicate(email))
            .Select(email => email.UserId!.Value)
            .Distinct()
            .ToList();

    /// <summary>
    /// What building an email came to - exactly one of the three is set: the
    /// message to send; why it could not be built, recorded like a failed
    /// send; or why it should not be sent at all, which closes it.
    /// </summary>
    private sealed record BuiltEmail(EmailMessage? Message, string? Error, string? ClosedReason)
    {
        public static BuiltEmail Ready(EmailMessage message) => new(message, null, null);

        public static BuiltEmail Failed(string error) => new(null, error, null);

        public static BuiltEmail Closed(string reason) => new(null, null, reason);
    }

    /// <summary>
    /// Fills the email's template. Returns the message; or - when it cannot be
    /// built - why, which is recorded against the email like a failed send;
    /// or - when there is nothing left to tell - why it is closed unsent.
    /// </summary>
    private async Task<BuiltEmail> BuildMessageAsync(
        EmailQueue email,
        ReminderEmailContext context,
        CancellationToken cancellationToken)
    {
        var (templateName, subject) = EmailTemplates[email.EmailTypeId];

        if (email.EmailTypeId == Constants.EmailType.Id.ApproverSubmissionReminder)
        {
            return await ApproverReminderMessageAsync(email, templateName, subject, context, cancellationToken);
        }

        if (email.UserId is not { } userId || !context.Details.TryGetValue(userId, out var details))
        {
            return BuiltEmail.Failed("The user has no live timesheet setup, or their user record is deleted.");
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
            return BuiltEmail.Failed(values.Error);
        }

        var body = await _emailTemplateStore.GetAsync(templateName, cancellationToken);

        // Every value is HTML-encoded: a name with a '<' in it is text, not markup.
        foreach (var (placeholder, value) in values.Values!)
        {
            body = body.Replace($"[{placeholder}]", WebUtility.HtmlEncode(value), StringComparison.Ordinal);
        }

        return BuiltEmail.Ready(new EmailMessage(email.EmailAddress.Trim(), subject, body));
    }

    /// <summary>
    /// Fills <c>ApproverSubmissionReminderTemplate.html</c> for one project:
    /// the period and the number of sheets awaiting approval in the text, then
    /// a grid titled with the project - user id, name, submission code,
    /// expected hours and hours logged, with totals. The grid is built here, every value in it
    /// HTML-encoded, and dropped into <c>[SubmissionGrid]</c> as markup.
    /// <para>
    /// Built from live data: when none of the period's sheets is still
    /// Submitted, the email is closed without sending rather than failed.
    /// </para>
    /// </summary>
    private async Task<BuiltEmail> ApproverReminderMessageAsync(
        EmailQueue email,
        string templateName,
        string subject,
        ReminderEmailContext context,
        CancellationToken cancellationToken)
    {
        if (email.UserId is not { } approverId || email.ProjectId is not { } projectId)
        {
            return BuiltEmail.Failed("An approver reminder needs both a UserID and a ProjectID.");
        }

        var runDate = ApproverRunDateOf(email, context.ServerOffset);

        if (!context.ApproverDigests.TryGetValue((approverId, projectId, runDate), out var digest))
        {
            return BuiltEmail.Closed(NoSubmittedSheetsLeftMessage);
        }

        var projectTitle = ProjectTitleOf(digest.Project);

        var values = new Dictionary<string, string>
        {
            ["ApproverName"] = digest.ApproverName,
            ["ProjectTitle"] = projectTitle,
            ["PeriodStartDate"] = FormatDate(digest.PeriodStart),
            ["PeriodEndDate"] = FormatDate(digest.PeriodEnd),
            ["SheetCount"] = digest.Rows.Count.ToString(CultureInfo.InvariantCulture),
            ["CompanyName"] = digest.Project.OrganizationName?.Trim() ?? string.Empty
        };

        var body = await _emailTemplateStore.GetAsync(templateName, cancellationToken);

        foreach (var (placeholder, value) in values)
        {
            body = body.Replace($"[{placeholder}]", WebUtility.HtmlEncode(value), StringComparison.Ordinal);
        }

        // Already markup, every value inside it encoded - so not encoded again.
        body = body.Replace("[SubmissionGrid]", ApproverReminderGridHtml(digest.Rows), StringComparison.Ordinal);

        // The subject is plain text, so the title goes in as it is.
        return BuiltEmail.Ready(new EmailMessage(email.EmailAddress.Trim(), $"{subject} - {projectTitle}", body));
    }

    /// <summary>"Website Revamp (PR0004)", or the code alone when the project has no name.</summary>
    private static string ProjectTitleOf(ApproverReminderProject project) =>
        string.IsNullOrWhiteSpace(project.ProjectName)
            ? project.ProjectCode.Trim()
            : $"{project.ProjectName!.Trim()} ({project.ProjectCode.Trim()})";

    /// <summary>The submitted sheets of an approver reminder, as an email-safe HTML table with inline styles.</summary>
    private static string ApproverReminderGridHtml(IReadOnlyList<ApproverReminderRow> rows)
    {
        static string Encode(string value) => WebUtility.HtmlEncode(value);

        const string headCell =
            "padding:8px 10px; border:1px solid #d0d7de; background-color:#1f4e78; color:#ffffff; " +
            "font-size:13px; text-align:left; white-space:nowrap;";
        const string cell = "padding:8px 10px; border:1px solid #d0d7de; font-size:13px; color:#333333;";

        var html = new StringBuilder()
            .Append("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" ")
            .Append("style=\"border-collapse:collapse;\">")
            .Append("<tr>");

        foreach (var heading in new[] { "#", "User ID", "User Name", "Submission Code", "Expected Hours", "Logged Hours" })
        {
            html.Append("<th style=\"").Append(headCell).Append("\">").Append(Encode(heading)).Append("</th>");
        }

        html.Append("</tr>");

        var number = 0;

        foreach (var row in rows)
        {
            number++;
            var background = number % 2 == 0 ? "#f8f9fa" : "#ffffff";

            html.Append("<tr style=\"background-color:").Append(background).Append(";\">")
                .Append("<td style=\"").Append(cell).Append("\">").Append(number).Append("</td>")
                .Append("<td style=\"").Append(cell).Append("\">").Append(row.UserId).Append("</td>")
                .Append("<td style=\"").Append(cell).Append("\">").Append(Encode(row.UserName)).Append("</td>")
                .Append("<td style=\"").Append(cell).Append(" white-space:nowrap;\">").Append(Encode(row.SheetSubmissionCode)).Append("</td>")
                .Append("<td style=\"").Append(cell).Append(" text-align:right;\">")
                .Append(Encode(row.ExpectedMinutes is { } expected ? FormatHours(expected) : "-")).Append("</td>")
                .Append("<td style=\"").Append(cell).Append(" text-align:right;\">").Append(Encode(FormatHours(row.HoursLogged * 60))).Append("</td>")
                .Append("</tr>");
        }

        html.Append("<tr style=\"background-color:#eef2f6;\">")
            .Append("<td colspan=\"4\" style=\"").Append(cell).Append(" font-weight:bold; text-align:right;\">Total</td>")
            .Append("<td style=\"").Append(cell).Append(" font-weight:bold; text-align:right;\">")
            .Append(Encode(rows.Any(row => row.ExpectedMinutes is not null)
                ? FormatHours(rows.Sum(row => row.ExpectedMinutes ?? 0))
                : "-"))
            .Append("</td>")
            .Append("<td style=\"").Append(cell).Append(" font-weight:bold; text-align:right;\">")
            .Append(Encode(FormatHours(rows.Sum(row => row.HoursLogged) * 60)))
            .Append("</td></tr>")
            .Append("</table>");

        return html.ToString();
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
