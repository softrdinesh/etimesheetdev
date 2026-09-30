using ETimeSheet.Application.Common;
using ETimeSheet.Application.Common.Mapping;
using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Exceptions;
using ETimeSheet.Shared.Utilities;
using Microsoft.Extensions.Logging;

namespace ETimeSheet.Application.Services.Implementations;

/// <summary>
/// Business logic for the SheetSubmission module. It reaches the database only
/// through <see cref="ISheetSubmissionRepository"/>, and never sees
/// <c>Context</c>.
/// <para>
/// <b>No authorisation check</b>, like the rest of the API while authentication
/// is switched off: the submitting user comes from the payload.
/// </para>
/// </summary>
public class SheetSubmissionService : ISheetSubmissionService
{
    private readonly ISheetSubmissionRepository _sheetSubmissionRepository;

    // Read-only use of the other modules' repositories - the user's setup, and
    // the sheet code their week's entries carry - so the week here can never
    // disagree with the one the time log save uses.
    private readonly IAdminRepository _adminRepository;
    private readonly ITimeLogRepository _timeLogRepository;

    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<SheetSubmissionService> _logger;

    public SheetSubmissionService(
        ISheetSubmissionRepository sheetSubmissionRepository,
        IAdminRepository adminRepository,
        ITimeLogRepository timeLogRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<SheetSubmissionService> logger)
    {
        _sheetSubmissionRepository = sheetSubmissionRepository;
        _adminRepository = adminRepository;
        _timeLogRepository = timeLogRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<SheetSubmissionResponse> SubmitAsync(
        SheetSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        // The validator guarantees a code; trimmed so " TS-00121" and
        // "TS-00121" are the same sheet.
        var sheetCode = request.SheetCode!.Trim();

        if (await _sheetSubmissionRepository.ExistsAsync(sheetCode, cancellationToken))
        {
            throw new ConflictException($"Timesheet '{sheetCode}' has already been submitted.");
        }

        var totalMinutes = await _sheetSubmissionRepository.GetSavedMinutesAsync(sheetCode, request.UserId, cancellationToken);

        if (totalMinutes is null)
        {
            throw new BusinessException(
                $"User '{request.UserId}' has no saved time logged on timesheet '{sheetCode}'.");
        }

        var now = _dateTimeProvider.UtcNow;

        var submission = new TimesheetSubmission
        {
            Timesheetcode = sheetCode,

            // Both columns hold the WHOLE total - hours rounded to two places
            // the way spc_GetTimeLoggedDetailsForTask rounds TotalWorkingHours
            // (SQL ROUND is away from zero; Math.Round defaults to banker's).
            TotalHours = ToHours(totalMinutes.Value),
            TotalMins = totalMinutes.Value,

            SubmittedBy = request.UserId,
            SubmittedDate = now,
            StatusId = Constants.TimesheetSubmission.Status.Submitted
        };

        // Staged first, written by the AddAsync save below - so the submission
        // and its audit entry land together or not at all.
        LogStatusChange(sheetCode, submission.StatusId.Value, request.UserId, now);

        await _sheetSubmissionRepository.AddAsync(submission, cancellationToken);

        _logger.LogInformation(
            "Timesheet {SheetCode} submitted by user {UserId}: {TotalMinutes} minutes.",
            sheetCode, request.UserId, totalMinutes.Value);

        return submission.ToResponse();
    }

    public async Task<SheetSubmissionResponse> ReviewAsync(
        SheetSubmissionReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        // The validator guarantees a code and a status of 2 or 3.
        var sheetCode = request.SheetCode!.Trim();

        var submission = await _sheetSubmissionRepository.GetForUpdateAsync(sheetCode, cancellationToken)
            ?? throw new NotFoundException($"Timesheet '{sheetCode}' has not been submitted.");

        // A sheet can be reviewed any number of times - rejected, then approved
        // later, or the other way round. The row holds only the LATEST decision:
        // the opposite pair is cleared, so an approved sheet never still shows
        // who rejected it. Every decision, in order, is in the audit log.
        var now = _dateTimeProvider.UtcNow;

        submission.StatusId = request.Status;

        if (request.Status == Constants.TimesheetSubmission.Status.Approved)
        {
            submission.ApprovedBy = request.AdminId;
            submission.ApprovedDate = now;
            submission.RejectedBy = null;
            submission.RejectedDate = null;
        }
        else
        {
            submission.RejectedBy = request.AdminId;
            submission.RejectedDate = now;
            submission.ApprovedBy = null;
            submission.ApprovedDate = null;
        }

        // Staged first, written by the UpdateAsync save below - the status
        // change and its audit entry land together or not at all.
        LogStatusChange(sheetCode, request.Status, request.AdminId, now);

        await _sheetSubmissionRepository.UpdateAsync(submission, cancellationToken);

        _logger.LogInformation(
            "Timesheet {SheetCode} reviewed by admin {AdminId}: status {Status}.",
            sheetCode, request.AdminId, request.Status);

        return submission.ToResponse();
    }

    public async Task<CurrentWeekSheetDetailResponse> GetCurrentWeekSheetDetailsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var setup = await _adminRepository.GetByUserIdAsync(userId, cancellationToken);

        // Today where the user is, as the time log save sees it - otherwise,
        // near midnight on the week's first day, this would report a different
        // week from the one their entries are going into.
        var today = ClockFor(setup, userId).Today;

        var weekStartsOn = TimesheetWeek.Parse(setup?.StartDay);
        var weekEndsOn = TimesheetWeek.Parse(setup?.EndDay);

        // Same anchor and fallback as TimeLogService.WeekStartOf.
        var weekStart = TimesheetWeek.StartOf(today, weekStartsOn ?? DayOfWeek.Monday);

        // The configured working days, wrapping past Sunday. Null when either
        // end is missing, in which case the week is shown as its seven days
        // and nothing is expected of it.
        var workingDays = weekStartsOn is { } start && weekEndsOn is { } end
            ? TimesheetWeek.Span(start, end).Count
            : (int?)null;

        var weekEnd = weekStart.AddDays((workingDays ?? 7) - 1);

        // The sheet code spans the full seven days, whatever EndDay says -
        // the save shares one code across the whole week.
        var sheetCode = await _timeLogRepository.GetSheetCodeForUserBetweenAsync(
            userId, weekStart, weekStart.AddDays(6), cancellationToken: cancellationToken);

        var workedMinutes = sheetCode is null
            ? 0
            : await _sheetSubmissionRepository.GetSavedMinutesAsync(sheetCode, userId, cancellationToken) ?? 0;

        var expectedMinutes = workingDays * DailyMinutesOf(setup);

        return new CurrentWeekSheetDetailResponse
        {
            UserId = userId,
            SheetCode = sheetCode,
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
            // Straight from the setup: the stored ids, and the names they
            // stand for - not the days the dates fall on.
            StartDay = setup?.StartDay,
            StartDayName = weekStartsOn?.ToString(),
            EndDay = setup?.EndDay,
            EndDayName = weekEndsOn?.ToString(),
            TotalHoursWorked = ToHours(workedMinutes),
            TotalHoursExpected = expectedMinutes is { } expected ? ToHours(expected) : null,
            TotalHoursDrift = expectedMinutes is { } expectedForDrift
                ? ToHours(workedMinutes - expectedForDrift)
                : null
        };
    }

    public async Task<IReadOnlyCollection<SubmittedSheetResponse>> GetSubmittedSheetListAsync(
        SubmittedSheetListRequest request,
        CancellationToken cancellationToken = default)
    {
        var details = await _sheetSubmissionRepository.GetSubmittedSheetListAsync(
            request.AdminId, request.UserId, request.OrgId, cancellationToken);

        _logger.LogDebug(
            "Returned {Count} submitted sheets for organisation {OrgId}, user {UserId}.",
            details.Count,
            request.OrgId,
            request.UserId);

        return details.Select(detail => detail.ToResponse()).ToList();
    }

    /// <summary>
    /// The setup's daily time in minutes - <c>MaxTimeinhrs</c> in hours and
    /// minutes, plus the minutes of <c>MaxTiminmins</c> - as
    /// <c>spc_GetUserDashboardSummaryByUserID</c> computes it. Null when there
    /// is no setup or no <c>MaxTimeinhrs</c>.
    /// </summary>
    private static int? DailyMinutesOf(TimesheetMasterSetup? setup) =>
        setup?.MaxTimeInHrs is { } hours
            ? hours.Hours * 60 + hours.Minutes + (setup.MaxTimInMins?.Minutes ?? 0)
            : null;

    /// <summary>
    /// Minutes as hours to two places, rounded away from zero like SQL ROUND -
    /// the same rounding the submission's <c>TotalHours</c> uses.
    /// </summary>
    private static decimal ToHours(int minutes) =>
        Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The user's clock, in the time zone their setup names. No setup, or a zone
    /// this system does not recognise, falls back to UTC - logged in the second
    /// case, as <c>TimeLogService</c> does, so the bad zone gets corrected.
    /// </summary>
    private EmployeeClock ClockFor(TimesheetMasterSetup? setup, int userId)
    {
        var zone = EmployeeClock.FindZone(setup?.TimeZone);

        if (zone is null && !string.IsNullOrWhiteSpace(setup?.TimeZone))
        {
            _logger.LogWarning(
                "Timesheet setup {SetupId} for user {UserId} names time zone {TimeZone}, which this " +
                "system does not recognise. The current week falls back to UTC.",
                setup!.SetupId,
                userId,
                setup.TimeZone);
        }

        return EmployeeClock.At(_dateTimeProvider.UtcNow, zone);
    }

    /// <summary>
    /// Records a status change on a sheet in <c>dbo.TimeSheetSubmissionAuditLog</c>.
    /// <b>Every</b> method that changes a submission's status calls this -
    /// submit and review today.
    /// <para>
    /// It stages the entry and does not save it: the caller's own repository
    /// write (<c>AddAsync</c> or <c>UpdateAsync</c>) writes it in the same
    /// transaction as the change it describes. So call it <b>before</b> that
    /// write, never after.
    /// </para>
    /// </summary>
    /// <param name="at">
    /// When the change happened - passed in, not read from the clock here, so
    /// the audit entry carries exactly the instant stamped on the submission.
    /// </param>
    private void LogStatusChange(string sheetCode, int statusId, int changedBy, DateTime at) =>
        _sheetSubmissionRepository.AddAuditLog(new TimeSheetSubmissionAuditLog
        {
            TimeSheetSubmissionId = sheetCode,
            StatusId = statusId,
            CreatedBy = changedBy,
            CreateDate = at
        });
}
