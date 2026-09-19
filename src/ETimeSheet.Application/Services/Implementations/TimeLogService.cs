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

    public async Task<TimeLoggedDetailsForTaskResponse> GetTimeLoggedDetailsForTaskAsync(
        TimeLoggedDetailsForTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        // No authorisation check and no current-user lookup: authentication is
        // switched off for this project for now, so the owner is whoever the
        // request names. When JWT is turned back on, the user id must come from
        // the token rather than the payload.
        var details = await _timeLogRepository.GetTimeLoggedDetailsForTaskAsync(
            request.UserId,
            request.TaskId,
            request.StartDate,
            request.EndDate,
            cancellationToken);

        var setups = await _timeLogRepository.GetTimesheetMasterSetupByUserIdAsync(
            request.UserId,
            cancellationToken);

        var summary = BuildSummary(details, setups.FirstOrDefault(), request);

        _logger.LogDebug(
            "Returned {Count} time logged details for user {UserId}, task {TaskId} " +
            "between {StartDate:yyyy-MM-dd} and {EndDate:yyyy-MM-dd}.",
            details.Count,
            request.UserId,
            request.TaskId,
            request.StartDate,
            request.EndDate);

        return new TimeLoggedDetailsForTaskResponse
        {
            Summary = summary,
            Details = details.ToResponses()
        };
    }

    /// <summary>
    /// Totals the period: what was worked, what was expected, what is left.
    /// </summary>
    private TimeLoggedSummaryResponse BuildSummary(
        IReadOnlyList<TimeLoggedDetail> details,
        TimesheetMasterSetupDetail? setup,
        TimeLoggedDetailsForTaskRequest request)
    {
        var expected = ExpectedFor(setup, request);
        var workedHours = WorkedHours(details);

        return new TimeLoggedSummaryResponse
        {
            TotalWorkInHours = workedHours,
            TotalExpected = expected,
            TotalRemaining = decimal.Round(expected - workedHours, 2)
        };
    }

    /// <summary>
    /// Total hours worked across the returned entries.
    /// <para>
    /// Summed from <c>TotalWorkingMinutes</c>, which the procedure computes with
    /// <c>DATEDIFF</c> over both the date and the time - so an entry running
    /// past midnight measures correctly rather than coming out negative. Minutes
    /// rather than the procedure's <c>TotalWorkingHours</c> because those are
    /// already rounded to two places, and summing rounded values compounds the
    /// error; this rounds once, at the end.
    /// </para>
    /// <para>
    /// A null contributes nothing (the entry is missing one of its ends), and so
    /// does a negative, which means inconsistent data rather than time running
    /// backwards - counting it would silently reduce the total.
    /// </para>
    /// </summary>
    private static decimal WorkedHours(IReadOnlyList<TimeLoggedDetail> details)
    {
        var minutes = details.Sum(detail =>
            detail.TotalWorkingMinutes is > 0 ? detail.TotalWorkingMinutes.Value : 0);

        return decimal.Round(minutes / 60m, 2);
    }

    /// <summary>
    /// What the user was expected to log across the requested period.
    /// <para>
    /// <b>This counts every calendar day in the range</b>, inclusive, multiplied
    /// by the daily maximum from <c>TimesheetMasterSetup.MaxTimeinhrs</c>. It
    /// does NOT yet exclude non-working days. That is now possible - StartDay
    /// and EndDay became <c>dbo.DayMaster.DayID</c> values on 2026-09-17, so the
    /// week is unambiguous - but changing what "expected" means would silently
    /// change every figure this endpoint has already reported. It is a decision
    /// to take deliberately, not a side effect of a column type change.
    /// </para>
    /// <para>
    /// A user with no setup row expects zero, rather than the request failing -
    /// the entries are still worth returning.
    /// </para>
    /// </summary>
    private static decimal ExpectedFor(
        TimesheetMasterSetupDetail? setup,
        TimeLoggedDetailsForTaskRequest request)
    {
        if (setup?.MaxTimeLoggedByUserInHours is not { } dailyMaximum)
        {
            return 0m;
        }

        var days = (request.EndDate.Date - request.StartDate.Date).Days + 1;

        return days <= 0 ? 0m : decimal.Round(ToHours(dailyMaximum) * days, 2);
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
        var setup = await _adminRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new BusinessException(
                $"User '{request.UserId}' has no timesheet setup, so there are no limits to check this " +
                "entry against. An administrator has to create one before they can log time.");

        await RequireDateIsOpenForLoggingAsync(request.UserId, loggedOn, setup, cancellationToken);
        RequireWorkingDay(loggedOn, setup);

        var existing = await _timeLogRepository.GetForUserOnDateAsync(
            request.UserId,
            loggedOn,
            cancellationToken);

        RequireNoOverlap(existing, startsAt, endsAt);
        RequireWithinDailyMaximum(existing, duration, loggedOn, setup);

        var timeLog = new TimeLog
        {
            UserId = request.UserId,
            TaskId = request.TaskId,
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

        var saved = await _timeLogRepository.AddAsync(timeLog, cancellationToken);

        _logger.LogInformation(
            "Time log {SheetId} recorded for user {UserId} on task {TaskId}: {Hours} hours on " +
            "{LoggedOn:yyyy-MM-dd}, status {Status}, logged by user {CreatedBy}.",
            saved.SheetId,
            request.UserId,
            request.TaskId,
            ToHours(duration),
            loggedOn,
            request.Status,
            request.CreatedBy);

        return saved.ToResponse(ToHours(duration));
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
        CancellationToken cancellationToken)
    {
        var today = _dateTimeProvider.UtcToday;

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

        if (setup.TimeEntryLockAt is { } lockedAt && _dateTimeProvider.UtcNow.TimeOfDay > lockedAt)
        {
            throw new BusinessException(
                $"The cut-off for logging time against an earlier day is {lockedAt:hh\\:mm}, " +
                $"and it has passed, so {loggedOn:yyyy-MM-dd} is now locked.");
        }
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

    public async Task<TimesheetMasterSetupResponse> GetTimesheetMasterSetupByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var setups = await _timeLogRepository.GetTimesheetMasterSetupByUserIdAsync(
            userId,
            cancellationToken);

        if (setups.Count == 0)
        {
            // A missing setup is a 404 rather than an empty 200: "this user has
            // no configured limits" is a different answer from "here are their
            // limits", and a caller that treated absent as zero would apply a
            // maximum of nothing.
            throw NotFoundException.For("Timesheet setup for user", userId);
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
}
