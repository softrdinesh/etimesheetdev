using ETimeSheet.Application.Common.Mapping;
using ETimeSheet.Application.DTOs.TimeLogs;
using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models.Results;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace ETimeSheet.Application.Services.Implementations;

/// <summary>
/// Business logic for the TimeLog module.
/// <para>
/// This class owns the rules about what a request is allowed to ask for and how
/// it is translated into a data-access call. It reaches the database only
/// through <see cref="ITimeLogRepository"/> and never sees
/// <c>Context</c>.
/// </para>
/// </summary>
public class TimeLogService : ITimeLogService
{
    private readonly ITimeLogRepository _timeLogRepository;
    private readonly ILogger<TimeLogService> _logger;

    public TimeLogService(
        ITimeLogRepository timeLogRepository,
        ILogger<TimeLogService> logger)
    {
        _timeLogRepository = timeLogRepository;
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
    /// does NOT yet exclude non-working days: the setup's StartDay/EndDay are
    /// <c>char(2)</c> codes whose vocabulary is not established, so honouring
    /// them would be guesswork. Confirm the codes and this method becomes a
    /// working-day count.
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
