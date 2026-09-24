using ETimeSheet.Application.Common.Mapping;
using System.Globalization;
using ETimeSheet.Application.Common;
using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Exceptions;
using ETimeSheet.Shared.Utilities;
using Microsoft.Extensions.Logging;

namespace ETimeSheet.Application.Services.Implementations;

/// <summary>
/// Business logic for the TimeLog module.
/// <para>
/// This class owns the rules about what a request is allowed to ask for and how
/// it is translated into a data-access call - including every rule the timesheet
/// setup imposes on logging time. It reaches the database only through
/// repository interfaces and never sees <c>Context</c>.
/// </para>
/// <para>
/// <b>Every rule about a time of day is judged on the employee's clock</b>, read
/// from the time zone their setup names - see <see cref="EmployeeClock"/>. The
/// injected <c>IDateTimeProvider</c> supplies the instant; the zone decides what
/// that instant reads as, and "today", "the future" and the day's cut-off all
/// follow from it.
/// </para>
/// </summary>
public class TimeLogService : ITimeLogService
{
    private readonly ITimeLogRepository _timeLogRepository;

    /// <summary>
    /// The write path validates against the whole setup row, and the stored
    /// procedure returns only seven of its columns - <c>Exceptionday</c> and
    /// <c>TimeEntryLockAt</c> are not among them. Reading the table through the
    /// Admin repository is what makes those two rules possible at all.
    /// <para>
    /// A service combining several repositories is expected; what would not be
    /// is reaching for the other module's <b>service</b>, which would drag its
    /// rules in with it.
    /// </para>
    /// </summary>
    private readonly IAdminRepository _adminRepository;

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<TimeLogService> _logger;

    public TimeLogService(
        ITimeLogRepository timeLogRepository,
        IAdminRepository adminRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<TimeLogService> logger)
    {
        _timeLogRepository = timeLogRepository;
        _adminRepository = adminRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<TimeLoggedDetailResponse>> GetLoggedTimeListByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        // No authorisation check and no current-user lookup: authentication is
        // switched off for this project for now, so the owner is whoever the
        // route names. When JWT is turned back on, the user id must come from
        // the token rather than the route.
        var details = await _timeLogRepository.GetTimeLoggedDetailsByUserIdAsync(userId, cancellationToken);

        _logger.LogDebug(
            "Returned {Count} time log entries for user {UserId}.",
            details.Count,
            userId);

        // Newest first, as the procedure orders them - by CreateDate, i.e. when
        // the entry was logged, not the day it was logged against.
        return details.ToResponses();
    }

    private static decimal ToHours(TimeSpan value) =>
        decimal.Round((decimal)value.TotalHours, 2);

    public async Task<TimeLogResponse> SaveTimeLogAsync(
        TimeLogSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // Both ends arrive as hh:mm:ss strings and are read here rather than in
        // the validator, because TimeOfDay is the one place in the application
        // that decides what a time of day is. A malformed value is a
        // ValidationException naming the field, so the caller is told "StartTime
        // must be ... hh:mm:ss" rather than being handed a binding error about a
        // type it never sent.
        var startTime = TimeOfDay.Parse(request.StartTime, nameof(TimeLogSaveRequest.StartTime));
        var endTime = TimeOfDay.Parse(request.EndTime, nameof(TimeLogSaveRequest.EndTime));

        // Both ends as full instants, so every rule below reads the same way for
        // an ordinary entry and for one that runs past midnight.
        var loggedOn = request.StartDate.Date;
        var startsAt = loggedOn + startTime;
        var endsAt = (request.EndDate?.Date ?? loggedOn) + endTime;
        var duration = endsAt - startsAt;

        // The entry has to cover some time, and it has to run forwards. Checked
        // across both ends including their dates, so an overnight shift - 22:00
        // on Monday to 06:00 on Tuesday - passes, while 17:00 to 09:00 on a
        // single day does not. This is the one rule about the times that the
        // validator cannot state, because it needs them parsed.
        if (duration <= TimeSpan.Zero)
        {
            throw new ValidationException(
                nameof(TimeLogSaveRequest.EndTime),
                "EndTime must be after StartTime, once both dates are taken into account.");
        }

        // No authorisation check and no current-user lookup, as on the reads:
        // authentication is switched off for this project for now, so the entry
        // belongs to whoever the request names. When JWT is turned back on, the
        // user id must come from the token rather than the payload.
        //
        // An edit names the entry it changes. Loaded first, before any rule
        // runs, because two of the rules below need to know what it was.
        var original = request.TimeLogId > 0
            ? await LoadForEditAsync(request, cancellationToken)
            : null;

        var setup = await _adminRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new BusinessException(
                $"User '{request.UserId}' has no timesheet setup, so there are no limits to check this " +
                "entry against. An administrator has to create one before they can log time.");

        // Every rule below that asks "what time is it?" reads this clock and not
        // the server's. The employee's setup names their time zone, and a
        // cut-off written as 19:00 means seven in the evening where they are.
        var clock = ClockFor(setup, request.UserId);

        // An edit has to pass the same cut-off TWICE: once for where the entry
        // is now, and once for where it is going. Checking only the new values
        // would let a locked 19:00 block be dragged to 22:00 after the cut-off
        // - which is editing locked time, just with a different end result.
        if (original is not null)
        {
            await RequireEntryIsStillEditableAsync(original, setup, clock, cancellationToken);
        }

        await RequireDateIsOpenForLoggingAsync(request.UserId, loggedOn, setup, clock, cancellationToken);
        RequireWorkingDay(loggedOn, setup);
        RequireEntryIsNotPastTheCutOff(startTime, setup, clock);

        // The entry being edited is left out of its own day: it is about to be
        // replaced, so it can neither overlap the new values nor count twice
        // towards the daily maximum.
        var existing = (await _timeLogRepository.GetForUserOnDateAsync(
                request.UserId,
                loggedOn,
                cancellationToken))
            .Where(entry => entry.SheetId != original?.SheetId)
            .ToList();

        RequireNoOverlap(existing, startsAt, endsAt);
        RequireWithinDailyMaximum(existing, duration, loggedOn, setup);

        var saved = original is null
            ? await AddTimeLogAsync(request, loggedOn, startTime, endTime, cancellationToken)
            : await UpdateTimeLogAsync(original, request, loggedOn, startTime, endTime, cancellationToken);

        _logger.LogInformation(
            "Time log {SheetId} {Action} for user {UserId} on task {TaskId}: {Hours} hours on " +
            "{LoggedOn:yyyy-MM-dd}, status {Status}, by user {ActingUser}.",
            saved.SheetId,
            original is null ? "recorded" : "edited",
            request.UserId,
            request.TaskId,
            ToHours(duration),
            loggedOn,
            request.Status,
            request.CreatedBy);

        return saved.ToResponse(ToHours(duration));
    }

    /// <summary>
    /// Loads the entry an edit names, tracked, and confirms it belongs to the
    /// user the request is for.
    /// <para>
    /// The owner check matters even with authentication off: every rule is
    /// judged against <b>the request's</b> user - their setup, their day, their
    /// other entries - and an entry owned by someone else would be validated
    /// against the wrong person's limits and then moved into their timesheet.
    /// </para>
    /// </summary>
    private async Task<TimeLog> LoadForEditAsync(
        TimeLogSaveRequest request,
        CancellationToken cancellationToken)
    {
        var original = await _timeLogRepository.GetForUpdateAsync(request.TimeLogId, cancellationToken)
            ?? throw NotFoundException.For("Time log", request.TimeLogId);

        if (original.UserId != request.UserId)
        {
            throw new BusinessException(
                $"Time log '{request.TimeLogId}' does not belong to user '{request.UserId}', so it " +
                "cannot be edited as theirs.");
        }

        return original;
    }

    /// <summary>
    /// Rejects an edit to an entry that is already locked where it stands.
    /// <para>
    /// The same two rules a new entry faces, applied to the entry's
    /// <b>current</b> date and start: its day must still be open, and once the
    /// cut-off has passed, a block that started before it can no longer be
    /// changed. Before the cut-off, every entry of the day can be edited.
    /// </para>
    /// <para>
    /// A half-recorded entry is judged on whatever it has - no stored date means
    /// no day to be locked, and no stored start means nothing to place against
    /// the cut-off - so the edit that completes it is not refused for the gap
    /// it is fixing.
    /// </para>
    /// </summary>
    private async Task RequireEntryIsStillEditableAsync(
        TimeLog original,
        TimesheetMasterSetup setup,
        EmployeeClock clock,
        CancellationToken cancellationToken)
    {
        if (original.StartDate is { } originalDate)
        {
            await RequireDateIsOpenForLoggingAsync(
                original.UserId ?? 0, originalDate.Date, setup, clock, cancellationToken);
        }

        if (original.StartTime is { } originalStart)
        {
            RequireEntryIsNotPastTheCutOff(originalStart, setup, clock);
        }
    }

    private async Task<TimeLog> AddTimeLogAsync(
        TimeLogSaveRequest request,
        DateTime loggedOn,
        TimeSpan startTime,
        TimeSpan endTime,
        CancellationToken cancellationToken)
    {
        var timeLog = new TimeLog
        {
            UserId = request.UserId,
            TaskId = request.TaskId,
            IsProjectTask = request.IsProjectTask,
            Description = request.Description,

            // Generated, never supplied. Read as late as possible - after every
            // rule has passed - so a rejected request does not consume a number
            // and leave a gap in the sequence.
            SheetCode = NextSheetCode(
                await _timeLogRepository.GetLatestSheetCodeAsync(cancellationToken)),
            StartDate = loggedOn,
            // Always written, even when the caller left it out: a row with a
            // start date and no end date cannot have its duration computed, and
            // spc_GetTimeLoggedDetailsForTask needs both to DATEDIFF across them.
            EndDate = request.EndDate?.Date ?? loggedOn,
            StartTime = startTime,
            EndTime = endTime,
            Status = request.Status,

            // Set here rather than left to AuditableEntityInterceptor. The
            // interceptor takes the acting user from ICurrentUserService, which
            // has nobody to report while authentication is off, so without this
            // every entry would record a null author. It still defers to an
            // authenticated identity once there is one - see the interceptor.
            CreatedBy = request.CreatedBy
        };

        return await _timeLogRepository.AddAsync(timeLog, cancellationToken);
    }

    /// <summary>
    /// Overwrites the edited entry with the payload.
    /// <para>
    /// What an edit leaves alone is as deliberate as what it changes: the
    /// <c>SheetCode</c> stays, because a code names one entry for life; the
    /// <c>UserId</c> stays, because the owner check already required it to
    /// match; and <c>CreatedBy</c> / <c>CreateDate</c> stay, because they say
    /// who created the entry, not who last touched it.
    /// </para>
    /// </summary>
    private async Task<TimeLog> UpdateTimeLogAsync(
        TimeLog original,
        TimeLogSaveRequest request,
        DateTime loggedOn,
        TimeSpan startTime,
        TimeSpan endTime,
        CancellationToken cancellationToken)
    {
        original.TaskId = request.TaskId;
        original.IsProjectTask = request.IsProjectTask;
        original.Description = request.Description;
        original.StartDate = loggedOn;
        original.EndDate = request.EndDate?.Date ?? loggedOn;
        original.StartTime = startTime;
        original.EndTime = endTime;
        original.Status = request.Status;

        // As CreatedBy on an add: the interceptor stamps UpdateDate, but has
        // no authenticated user to record while authentication is off.
        original.UpdatedBy = request.CreatedBy;

        return await _timeLogRepository.UpdateAsync(original, cancellationToken);
    }

    // ---- sheet code generation -------------------------------------------

    /// <summary>The letter every generated sheet code starts with.</summary>
    private const string SheetCodePrefix = "T";

    /// <summary>
    /// How many digits the first generation uses, which is what makes the very
    /// first code <c>T0001</c> rather than <c>T1</c>.
    /// </summary>
    private const int SheetCodeInitialDigits = 4;

    /// <summary>
    /// <c>dbo.TimeLog.SheetCode</c> is <c>varchar(15)</c>. A longer value is
    /// silently truncated by SQL Server rather than rejected, which would store
    /// a code nobody generated and quietly duplicate an existing one.
    /// </summary>
    private const int SheetCodeMaximumLength = 15;

    /// <summary>
    /// The code that follows <paramref name="latest"/>.
    /// <para>
    /// Codes run <c>T0001</c>, <c>T0002</c> … <c>T9999</c>. When a width runs
    /// out the sequence does not stop and does not overflow into a ragged
    /// number - it starts a <b>new generation one digit wider, back at one</b>:
    /// <c>T9999</c> is followed by <c>T00001</c>, and <c>T99999</c> by
    /// <c>T000001</c>.
    /// </para>
    /// <para>
    /// That is why the limit can never be reached. Each generation is 9x the
    /// previous one, and because the widths differ, no code from one generation
    /// can ever equal a code from another - <c>T0001</c> and <c>T00001</c> are
    /// different strings. The width simply grows on demand, up to the fourteen
    /// digits the column can hold, which is a hundred million million codes.
    /// </para>
    /// <para>
    /// <paramref name="latest"/> is <see langword="null"/> when nothing has been
    /// generated yet - an empty table, or one holding only hand-entered
    /// references - and the sequence starts at <c>T0001</c>.
    /// </para>
    /// </summary>
    private static string NextSheetCode(string? latest)
    {
        if (string.IsNullOrWhiteSpace(latest))
        {
            return Format(1, SheetCodeInitialDigits);
        }

        // The repository only returns codes of exactly this shape, so the digits
        // parse. TryParse rather than Parse all the same: this decides what goes
        // into the column, and falling back to the start of the sequence is
        // better than throwing on data nobody can correct.
        var digits = latest.Trim()[SheetCodePrefix.Length..];

        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return Format(1, SheetCodeInitialDigits);
        }

        var width = digits.Length;

        // All nines at this width - 9999, 99999 - is the end of the generation.
        // The next one is a digit wider and begins again at 1, which is what
        // makes T00001 follow T9999.
        if (number >= HighestAt(width))
        {
            return Format(1, width + 1);
        }

        return Format(number + 1, width);
    }

    /// <summary>The largest number that fits in <paramref name="width"/> digits: 4 gives 9999.</summary>
    private static long HighestAt(int width)
    {
        var highest = 1L;

        for (var digit = 0; digit < width; digit++)
        {
            highest *= 10;
        }

        return highest - 1;
    }

    /// <summary>
    /// Renders one code, zero-padded to <paramref name="width"/>.
    /// </summary>
    /// <exception cref="BusinessException">
    /// The code would not fit the column. Unreachable in practice - it takes a
    /// hundred million million entries - but truncation would silently duplicate
    /// an existing code, so it fails loudly instead.
    /// </exception>
    private static string Format(long number, int width)
    {
        var code = SheetCodePrefix + number.ToString(new string('0', width), CultureInfo.InvariantCulture);

        if (code.Length > SheetCodeMaximumLength)
        {
            throw new BusinessException(
                $"The sheet code sequence has run out of room: '{code}' is longer than the " +
                $"{SheetCodeMaximumLength} characters dbo.TimeLog.SheetCode can hold.");
        }

        return code;
    }

    /// <summary>
    /// Rejects a date the user is not allowed to log against: the future
    /// outright, and the past unless their setup still leaves it open.
    /// <para>
    /// Two separate permissions govern the past, and both have to hold.
    /// <c>CanUserLoggedPreDayTime</c> says whether this user may back-date at
    /// all; <c>TimeEntryLockAt</c> says how late in the day they may still do it.
    /// The first is derived by <c>spc_GetTimesheetMasterSetupByUserID</c> and is
    /// not a column, so it can only be had from the procedure - which is why
    /// this is the one rule that reads the setup twice, and why it only does so
    /// when the entry is actually back-dated.
    /// </para>
    /// </summary>
    private async Task RequireDateIsOpenForLoggingAsync(
        int userId,
        DateTime loggedOn,
        TimesheetMasterSetup setup,
        EmployeeClock clock,
        CancellationToken cancellationToken)
    {
        // The employee's today, not the server's. They are different dates for
        // part of every day: at 09:00 in Auckland it is still yesterday in UTC,
        // and judging "the future" on the server's date would reject an
        // employee logging the morning they are actually living through.
        var today = clock.Today;

        if (loggedOn > today)
        {
            throw new BusinessException(
                $"Time cannot be logged against {loggedOn:yyyy-MM-dd} because it has not happened yet.");
        }

        if (loggedOn == today)
        {
            return;
        }

        var setups = await _timeLogRepository.GetTimesheetMasterSetupByUserIdAsync(userId, cancellationToken);

        // Absent is treated as "no", not as "yes": the flag exists to restrict
        // back-dating, and a missing answer is not permission to ignore it.
        if (setups.FirstOrDefault()?.CanUserLoggedPreDayTime is not true)
        {
            throw new BusinessException(
                $"User '{userId}' is not allowed to log time against an earlier day, so " +
                $"{loggedOn:yyyy-MM-dd} cannot be used.");
        }

        if (setup.TimeEntryLockAt is { } lockedAt && clock.TimeOfDay > lockedAt)
        {
            throw new BusinessException(
                $"The cut-off for logging time against an earlier day is {lockedAt:hh\\:mm} " +
                $"{clock.ZoneName} time, and it is now {clock.Now:HH:mm} there, so " +
                $"{loggedOn:yyyy-MM-dd} is locked.");
        }
    }

    /// <summary>
    /// Rejects an entry that begins before the day's cut-off once that cut-off
    /// has passed on the employee's own clock.
    /// <para>
    /// <c>TimeEntryLockAt</c> is the moment the day's earlier hours stop being
    /// editable. With a cut-off of 21:00: an employee logging at noon, at 17:00
    /// or at 19:00 is fine, because the cut-off has not arrived. The same
    /// employee at 22:00 may still log the evening they are working - an entry
    /// starting at 22:00 is <b>after</b> the cut-off - but may no longer add the
    /// 19:00 block they forgot. That one needs an administrator.
    /// </para>
    /// <para>
    /// <b>Both halves matter.</b> Blocking on the clock alone would stop a late
    /// shift from recording the hours it is working, which is not what a cut-off
    /// is for; blocking on the entry's time alone would refuse a 19:00 block at
    /// nine in the morning, when the day is still wide open.
    /// </para>
    /// <para>
    /// The entry's <b>start</b> is what places it: a block running 20:00 to
    /// 23:00 began before the cut-off and is refused with the rest of the
    /// evening. Splitting it at the cut-off would be inventing an entry the
    /// employee did not send.
    /// </para>
    /// <para>
    /// No cut-off configured means no deadline at all - the column is nullable
    /// and most rows leave it null.
    /// </para>
    /// </summary>
    private static void RequireEntryIsNotPastTheCutOff(
        TimeSpan startTime,
        TimesheetMasterSetup setup,
        EmployeeClock clock)
    {
        if (setup.TimeEntryLockAt is not { } lockedAt)
        {
            return;
        }

        // Still before the cut-off: the whole day is open, whatever the entry
        // covers. Exactly on it counts as open - the deadline is the last
        // moment that works, not the first that does not.
        if (clock.TimeOfDay <= lockedAt)
        {
            return;
        }

        // The cut-off has passed, but this entry belongs to the part of the day
        // that comes after it - the hours the employee is working right now.
        if (startTime >= lockedAt)
        {
            return;
        }

        throw new BusinessException(
            $"It is {clock.Now:HH:mm} {clock.ZoneName} time, past the {lockedAt:hh\\:mm} cut-off, " +
            $"so time starting before {lockedAt:hh\\:mm} can no longer be added or changed. " +
            "Ask an administrator to record it for you.");
    }

    /// <summary>
    /// Builds the employee's clock from the time zone on their setup.
    /// <para>
    /// An unusable zone - none recorded, or one this system does not recognise -
    /// falls back to UTC and is logged rather than refused. The zone is data an
    /// administrator fills in, and an employee should not lose the ability to
    /// log time because somebody mistyped it; the warning is what gets it
    /// corrected. Rows written before <c>TimeZone</c> existed are the ordinary
    /// case of this.
    /// </para>
    /// </summary>
    private EmployeeClock ClockFor(TimesheetMasterSetup setup, int userId)
    {
        var zone = EmployeeClock.FindZone(setup.TimeZone);

        if (zone is null && !string.IsNullOrWhiteSpace(setup.TimeZone))
        {
            _logger.LogWarning(
                "Timesheet setup {SetupId} for user {UserId} names time zone {TimeZone}, which this " +
                "system does not recognise. Time-of-day rules for this entry fall back to UTC.",
                setup.SetupId,
                userId,
                setup.TimeZone);
        }

        return EmployeeClock.At(_dateTimeProvider.UtcNow, zone);
    }

    /// <summary>
    /// Rejects a date outside the user's timesheet week.
    /// <para>
    /// The week runs from <c>StartDay</c> to <c>EndDay</c> and may wrap - a
    /// Sunday-to-Thursday week is normal in some of this data - and
    /// <c>Exceptionday</c> is allowed <b>in addition</b> to it, as a day this
    /// contract works that the ordinary week does not cover. All three are
    /// <c>dbo.DayMaster.DayID</c> values.
    /// </para>
    /// <para>
    /// A week that is not configured, or is configured with day ids outside the
    /// seven in <c>dbo.DayMaster</c>, imposes no constraint. Refusing to log
    /// time because someone left the setup half-filled would block the employee
    /// for a mistake they cannot fix.
    /// </para>
    /// </summary>
    private static void RequireWorkingDay(DateTime loggedOn, TimesheetMasterSetup setup)
    {
        if (TimesheetWeek.Parse(setup.StartDay) is not { } weekStarts ||
            TimesheetWeek.Parse(setup.EndDay) is not { } weekEnds)
        {
            return;
        }

        var workingDays = TimesheetWeek.Span(weekStarts, weekEnds);

        if (workingDays.Contains(loggedOn.DayOfWeek) ||
            TimesheetWeek.Parse(setup.ExceptionDay) == loggedOn.DayOfWeek)
        {
            return;
        }

        throw new BusinessException(
            $"{loggedOn:yyyy-MM-dd} is a {loggedOn.DayOfWeek}, which is not a working day in this " +
            $"user's timesheet week ({TimesheetWeek.Describe(setup.StartDay)} to " +
            $"{TimesheetWeek.Describe(setup.EndDay)}).");
    }

    /// <summary>
    /// Rejects an entry that covers time the user has already logged that day.
    /// <para>
    /// A conflict rather than a validation error: the payload is well formed,
    /// and it is the entries already in the table that make it impossible.
    /// Half-recorded rows - one end missing - are skipped, because an entry with
    /// no end does not describe a span that anything can overlap.
    /// </para>
    /// </summary>
    private static void RequireNoOverlap(
        IReadOnlyList<TimeLog> existing,
        DateTime startsAt,
        DateTime endsAt)
    {
        foreach (var entry in existing)
        {
            if (SpanOf(entry) is not { } span)
            {
                continue;
            }

            // Touching ends do not overlap: an entry ending at 12:00 and the
            // next starting at 12:00 are adjacent, which is how a day is
            // normally filled in.
            if (startsAt < span.EndsAt && span.StartsAt < endsAt)
            {
                throw new ConflictException(
                    $"This overlaps time log {entry.SheetId}, which already covers " +
                    $"{span.StartsAt:HH\\:mm} to {span.EndsAt:HH\\:mm} that day.");
            }
        }
    }

    /// <summary>
    /// Rejects an entry that would take the day past the maximum in the user's
    /// setup.
    /// <para>
    /// Counted against every live entry already on that date, drafts included: a
    /// draft still occupies the time, and excluding them would let a user reach
    /// any total by drafting first. A setup with no maximum imposes no limit
    /// rather than a limit of zero.
    /// </para>
    /// </summary>
    private static void RequireWithinDailyMaximum(
        IReadOnlyList<TimeLog> existing,
        TimeSpan duration,
        DateTime loggedOn,
        TimesheetMasterSetup setup)
    {
        if (setup.MaxTimeInHrs is not { } dailyMaximum)
        {
            return;
        }

        var alreadyLogged = existing.Aggregate(
            TimeSpan.Zero,
            (running, entry) => running + (DurationOf(entry) ?? TimeSpan.Zero));

        var total = alreadyLogged + duration;

        if (total <= dailyMaximum)
        {
            return;
        }

        throw new BusinessException(
            $"Logging {ToHours(duration)} hours would bring {loggedOn:yyyy-MM-dd} to " +
            $"{ToHours(total)} hours, above the {ToHours(dailyMaximum)} hour daily maximum in this " +
            $"user's timesheet setup ({ToHours(alreadyLogged)} hours are already logged).");
    }

    /// <summary>
    /// Both ends of a stored entry as instants, or null when either end is
    /// missing. Dates are included so an overnight entry spans correctly instead
    /// of appearing to run backwards.
    /// </summary>
    private static (DateTime StartsAt, DateTime EndsAt)? SpanOf(TimeLog entry)
    {
        if (entry.StartDate is not { } startDate ||
            entry.StartTime is not { } startTime ||
            entry.EndTime is not { } endTime)
        {
            return null;
        }

        // A missing end date means the entry ends on the day it started, which
        // is what every single-day row in this table looks like.
        var endDate = entry.EndDate ?? startDate;

        return (startDate.Date + startTime, endDate.Date + endTime);
    }

    private static TimeSpan? DurationOf(TimeLog entry)
    {
        if (SpanOf(entry) is not { } span)
        {
            return null;
        }

        // A negative span is inconsistent data rather than time running
        // backwards, and counting it would quietly reduce the day's total.
        var duration = span.EndsAt - span.StartsAt;

        return duration > TimeSpan.Zero ? duration : null;
    }

    public async Task<TimesheetMasterSetupResponse?> GetTimesheetMasterSetupByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var setups = await _timeLogRepository.GetTimesheetMasterSetupByUserIdAsync(
            userId,
            cancellationToken);

        if (setups.Count == 0)
        {
            // Null, which the controller turns into a 200 carrying
            // success: true and data: null. A read that found nothing has not
            // failed - it has answered - and an envelope saying success: false
            // tells a client to look for a mistake it did not make.
            //
            // Still not the same answer as an empty setup, though, and the
            // contract keeps them apart: data: null means "no setup row",
            // whereas a row whose columns happen to be null comes back as an
            // object. A caller must not read null as "no limits configured, log
            // what you like" - the write path enforces the limits itself, and
            // refuses a user who has no setup at all.
            return null;
        }

        if (setups.Count > 1)
        {
            // Not an error - the procedure does not guarantee uniqueness - but
            // the data has more than one answer and the caller only sees the
            // first, so it has to be visible somewhere.
            _logger.LogWarning(
                "User {UserId} has {Count} timesheet setup rows; returning the first (SetupID {SetupId}).",
                userId,
                setups.Count,
                setups[0].SetupId);
        }

        return setups[0].ToResponse();
    }

    public async Task<UserTaskListResponse> GetUserTaskListByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        // No authorisation check, as on the other reads: authentication is
        // switched off for now, so the user is whoever the route names.
        var tasks = await _timeLogRepository.GetUserTaskListByUserIdAsync(userId, cancellationToken);

        _logger.LogDebug(
            "Returned {ProjectCount} project tasks and {SprintCount} sprint tasks for user {UserId}.",
            tasks.ProjectTasks.Count,
            tasks.SprintTasks.Count,
            userId);

        return tasks.ToResponse();
    }
}
