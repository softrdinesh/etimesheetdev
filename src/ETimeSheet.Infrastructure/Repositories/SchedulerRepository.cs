using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core data access for the Scheduler module, over the
/// existing <c>dbo.SchedulerConfiguration</c> table and the setups, users and
/// time logs a reminder is decided from.
/// </summary>
public class SchedulerRepository : ISchedulerRepository
{
    private readonly Context _db;

    public SchedulerRepository(Context db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SchedulerConfiguration>> GetByIdsAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancellationToken = default) =>
        await _db.SchedulerConfiguration
            .AsNoTracking()
            .Where(configuration => ids.Contains(configuration.SchedulerConfigurationId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ReminderCandidate>> GetReminderCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        // The setup's query filter already drops deleted setups. Projected, so
        // only the columns the reminder needs are read.
        var candidates = await (
                from setup in _db.TimesheetMasterSetup.AsNoTracking()
                join user in _db.Signup.AsNoTracking() on setup.UserId equals (int?)user.UserId
                where setup.NeedToSendReminder == true
                      && setup.TimeEntryLockAt != null
                      && user.IsDelete == 0
                select new ReminderCandidate
                {
                    SetupId = setup.SetupId,
                    UserId = user.UserId,
                    OrganizationId = setup.OrganizationId,
                    Email = user.Email,
                    TimeZone = setup.TimeZone,
                    StartDay = setup.StartDay,
                    EndDay = setup.EndDay,
                    ExceptionDay = setup.ExceptionDay,
                    MaxTimeInHrs = setup.MaxTimeInHrs,
                    MaxTimInMins = setup.MaxTimInMins,
                    TimeEntryLockAt = setup.TimeEntryLockAt!.Value,
                    ReminderTimeBeforeCutoff = setup.ReminderTimeBeforeCutoff
                })
            .ToListAsync(cancellationToken);

        // One setup per user - the lowest SetupID, as the procedures pick.
        return candidates
            .GroupBy(candidate => candidate.UserId)
            .Select(setups => setups.OrderBy(candidate => candidate.SetupId).First())
            .ToList();
    }

    public async Task<IReadOnlyList<ReminderEmailDetails>> GetReminderEmailDetailsAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default)
    {
        // The setup's query filter drops deleted setups. The company is the
        // setup's organisation; a LEFT join, so a setup naming an organisation
        // that does not exist still gets its email, with no company name.
        var details = await (
                from setup in _db.TimesheetMasterSetup.AsNoTracking()
                join user in _db.Signup.AsNoTracking() on setup.UserId equals (int?)user.UserId
                join organization in _db.Organization.AsNoTracking()
                    on setup.OrganizationId equals (int?)organization.OrganizationId into organizations
                from organization in organizations.DefaultIfEmpty()
                where userIds.Contains(user.UserId) && user.IsDelete == 0
                select new ReminderEmailDetails
                {
                    SetupId = setup.SetupId,
                    UserId = user.UserId,
                    Name = user.Name,
                    OrganizationName = organization.OrganizationName,
                    TimeZone = setup.TimeZone,
                    StartDay = setup.StartDay,
                    MaxTimeInHrs = setup.MaxTimeInHrs,
                    MaxTimInMins = setup.MaxTimInMins,
                    TimeEntryLockAt = setup.TimeEntryLockAt
                })
            .ToListAsync(cancellationToken);

        return details
            .GroupBy(detail => detail.UserId)
            .Select(setups => setups.OrderBy(detail => detail.SetupId).First())
            .ToList();
    }

    public async Task<IReadOnlyList<LoggedSpan>> GetLoggedSpansAsync(
        IReadOnlyCollection<int> userIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        // The query filter drops deleted entries. An entry overlaps the range
        // when it starts on or before its last day and ends on or after its
        // first - which catches a shift that crosses midnight into it.
        var entries = await _db.TimeLog
            .AsNoTracking()
            .Where(entry => entry.UserId != null
                && userIds.Contains(entry.UserId.Value)
                && entry.StartDate != null
                && entry.StartTime != null
                && entry.EndDate != null
                && entry.EndTime != null
                && entry.StartDate <= toDate.Date
                && entry.EndDate >= fromDate.Date)
            .Select(entry => new
            {
                entry.UserId,
                entry.StartDate,
                entry.StartTime,
                entry.EndDate,
                entry.EndTime
            })
            .ToListAsync(cancellationToken);

        return entries
            .Select(entry => new LoggedSpan(
                entry.UserId!.Value,
                entry.StartDate!.Value.Date + entry.StartTime!.Value,
                entry.EndDate!.Value.Date + entry.EndTime!.Value))
            .ToList();
    }

    public async Task<IReadOnlyList<SubmittedEntry>> GetSubmittedEntriesAsync(
        IReadOnlyCollection<int> userIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        // The query filter drops deleted entries. A sheet is submitted when its
        // code has a row in dbo.TimesheetSubmission - the table's key.
        var entries = await (
                from entry in _db.TimeLog.AsNoTracking()
                join submission in _db.TimesheetSubmission.AsNoTracking()
                    on entry.SheetCode equals submission.Timesheetcode
                where entry.UserId != null
                      && userIds.Contains(entry.UserId.Value)
                      && entry.StartDate != null
                      && entry.StartDate >= fromDate.Date
                      && entry.StartDate <= toDate.Date
                select new { entry.UserId, entry.StartDate })
            .Distinct()
            .ToListAsync(cancellationToken);

        return entries
            .Select(entry => new SubmittedEntry(entry.UserId!.Value, entry.StartDate!.Value))
            .ToList();
    }
}
