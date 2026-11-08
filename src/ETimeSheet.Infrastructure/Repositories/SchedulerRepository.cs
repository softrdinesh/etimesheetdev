using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Infrastructure.Data;
using ETimeSheet.Shared.Utilities;
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

    public async Task<IReadOnlyList<ApproverReminderProject>> GetApproverReminderProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        // The approver setup's query filter drops deleted setups. The company
        // is a LEFT join, so a missing organisation only costs the sign-off.
        var frequencies = new[]
        {
            Constants.TimesheetProject.SubmissionFrequency.Weekly,
            Constants.TimesheetProject.SubmissionFrequency.FifteenDays,
            Constants.TimesheetProject.SubmissionFrequency.Monthly
        };

        var rows = await (
                from setup in _db.Timesheet_Approver_setup.AsNoTracking()
                join project in _db.TimesheetProject.AsNoTracking() on setup.ProjectId equals (int?)project.ProjectId
                join user in _db.Signup.AsNoTracking() on setup.ApproverId equals (int?)user.UserId
                join organization in _db.Organization.AsNoTracking()
                    on (project.OrganizationId ?? setup.OrganizationId) equals (int?)organization.OrganizationId
                    into organizations
                from organization in organizations.DefaultIfEmpty()
                where setup.IsTimesheetApprover == true
                      && user.IsDelete == 0
                      && project.StartDate != null
                      && project.SubmissionFrequency != null
                      && frequencies.Contains(project.SubmissionFrequency!.Value)
                select new ApproverReminderProject
                {
                    ApproverId = user.UserId,
                    ApproverName = user.Name,
                    ApproverEmail = user.Email,
                    ProjectId = project.ProjectId,
                    ProjectCode = project.Code,
                    ProjectName = project.ProjectName,
                    OrganizationId = project.OrganizationId ?? setup.OrganizationId,
                    OrganizationName = organization.OrganizationName,
                    StartDate = project.StartDate!.Value,
                    EndDate = project.EndDate,
                    SubmissionFrequency = project.SubmissionFrequency!.Value
                })
            .ToListAsync(cancellationToken);

        // Nothing in the table stops the same approver being set up twice on
        // one project; they get the project once.
        return rows
            .GroupBy(row => (row.ApproverId, row.ProjectId))
            .Select(group => group.First())
            .ToList();
    }

    public async Task<IReadOnlyList<ProjectLoggedDay>> GetProjectLoggedDaysAsync(
        IReadOnlyCollection<int> projectIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        // The query filter drops deleted rows. Summed in the database: a day
        // may hold several rows.
        var days = await _db.TimesheetProjectTimeLog
            .AsNoTracking()
            .Where(timeLog => projectIds.Contains(timeLog.ProjectId)
                && timeLog.LogDate >= fromDate.Date
                && timeLog.LogDate <= toDate.Date)
            .GroupBy(timeLog => new { timeLog.ProjectId, timeLog.UserId, timeLog.LogDate, timeLog.SheetSubmissionCode })
            .Select(group => new
            {
                group.Key.ProjectId,
                group.Key.UserId,
                group.Key.LogDate,
                group.Key.SheetSubmissionCode,
                HoursLogged = group.Sum(timeLog => timeLog.HoursLogged)
            })
            .ToListAsync(cancellationToken);

        return days
            .Select(day => new ProjectLoggedDay(day.ProjectId, day.UserId, day.LogDate, day.SheetSubmissionCode, day.HoursLogged))
            .ToList();
    }

    public async Task<IReadOnlyList<ProjectSheetSubmissionRow>> GetProjectSubmissionsAsync(
        IReadOnlyCollection<int> projectIds,
        int statusId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default) =>
        // The query filter drops deleted submissions.
        await _db.TimesheetProjectSubmission
            .AsNoTracking()
            .Where(submission => projectIds.Contains(submission.ProjectId)
                && submission.StatusId == statusId
                && submission.PeriodStartDate >= fromDate.Date
                && submission.PeriodStartDate <= toDate.Date)
            .Select(submission => new ProjectSheetSubmissionRow(
                submission.ProjectId,
                submission.UserId,
                submission.PeriodStartDate,
                submission.SheetSubmissionCode))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserName>> GetUserNamesAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default) =>
        await _db.Signup
            .AsNoTracking()
            .Where(user => userIds.Contains(user.UserId))
            .Select(user => new UserName(user.UserId, user.Name))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkingWeekSetup>> GetWorkingWeekSetupsAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default)
    {
        // The setup's query filter drops deleted setups. Projected, so only
        // the columns the expected hours need are read.
        var setups = await _db.TimesheetMasterSetup
            .AsNoTracking()
            .Where(setup => setup.UserId != null && userIds.Contains(setup.UserId.Value))
            .Select(setup => new WorkingWeekSetup
            {
                SetupId = setup.SetupId,
                UserId = setup.UserId!.Value,
                StartDay = setup.StartDay,
                EndDay = setup.EndDay,
                ExceptionDay = setup.ExceptionDay,
                MaxTimeInHrs = setup.MaxTimeInHrs,
                MaxTimInMins = setup.MaxTimInMins
            })
            .ToListAsync(cancellationToken);

        // One setup per user - the lowest SetupID, as the procedures pick.
        return setups
            .GroupBy(setup => setup.UserId)
            .Select(group => group.OrderBy(setup => setup.SetupId).First())
            .ToList();
    }
}
