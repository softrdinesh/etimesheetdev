using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Shared.Enums;
using ETimeSheet.Shared.Utilities;
using ETimeSheet.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Arrangements for the TimeLog endpoint integration tests.
/// <para>
/// Kept apart from <see cref="TimeLogTestData"/>, which the unit tests and the
/// factory depend on. Every date here is anchored to
/// <see cref="TimeLogTestData.Now"/> - Tuesday 2026-03-10, 12:00 UTC - which is
/// the instant the API's <c>IDateTimeProvider</c> is frozen at. The one
/// exception is the dashboard procedure, which reads <c>GETDATE()</c>; its
/// tests arrange rows against <see cref="ServerTodayAsync"/> instead.
/// </para>
/// <para>
/// Every helper reaches the database through
/// <see cref="ETimeSheetApiFactory.WithDbContextAsync(Func{ETimeSheet.Infrastructure.Data.Context, Task})"/>,
/// so the throwaway-container guard runs before any row is touched.
/// </para>
/// </summary>
public static class TimeLogEndpointTestData
{
    public const int UserId = 3101;

    public const int OtherUserId = 3102;

    public const int TaskId = 41;

    /// <summary>Who the save payload says is acting. Recorded as CreatedBy / UpdatedBy.</summary>
    public const int ActingUserId = 3101;

    /// <summary>Tuesday 2026-03-10 - the frozen clock's date, in UTC.</summary>
    public static readonly DateTime Today = TimeLogTestData.Today;

    /// <summary>Monday 2026-03-09 - the start of today's Monday-anchored week.</summary>
    public static readonly DateTime Monday = Today.AddDays(-1);

    /// <summary>Sunday 2026-03-08 - the last day of the previous Monday week.</summary>
    public static readonly DateTime PreviousSunday = Today.AddDays(-2);

    /// <summary>Friday 2026-03-06 - a working day of the previous week.</summary>
    public static readonly DateTime PreviousFriday = Today.AddDays(-4);

    /// <summary>Wednesday 2026-03-11 - the future, as far as the frozen clock is concerned.</summary>
    public static readonly DateTime Tomorrow = Today.AddDays(1);

    /// <summary>
    /// A save payload that passes validation and every setup rule against
    /// <see cref="Setup"/>; a test changes only what it is about.
    /// </summary>
    public static TimeLogSaveRequest SaveRequest(
        int timeLogId = 0,
        int userId = UserId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? startTime = "09:00:00",
        string? endTime = "11:00:00",
        TimeLogStatus status = TimeLogStatus.Save,
        bool? isProjectTask = true,
        string? description = "Integration test entry.") => new()
    {
        TimeLogId = timeLogId,
        UserId = userId,
        TaskId = TaskId,
        IsProjectTask = isProjectTask,
        Description = description,
        StartDate = startDate ?? Today,
        EndDate = endDate,
        StartTime = startTime,
        EndTime = endTime,
        Status = status,
        CreatedBy = ActingUserId,
    };

    /// <summary>
    /// A Monday-to-Friday setup at eight hours a day, with no cut-off and no
    /// time zone - so the employee's clock is UTC and nothing is ever locked.
    /// </summary>
    public static TimesheetMasterSetup Setup(
        int userId = UserId,
        int? startDay = Constants.DayMaster.DayId.Monday,
        int? endDay = Constants.DayMaster.DayId.Friday,
        int? exceptionDay = null,
        TimeSpan? maxTimeInHrs = null,
        TimeSpan? maxTimInMins = null,
        TimeSpan? timeEntryLockAt = null,
        string? timeZone = null,
        int? contractType = Constants.TimesheetMasterSetup.ContractType.FullTime,
        bool isDelete = false) => new()
    {
        UserId = userId,
        OrganizationId = EmployeeListTestData.OrganizationId,
        ContractType = contractType,
        StartDay = startDay,
        EndDay = endDay,
        ExceptionDay = exceptionDay,
        MaxTimeInHrs = maxTimeInHrs ?? TimeSpan.FromHours(8),
        MaxTimInMins = maxTimInMins ?? TimeSpan.Zero,
        TimeEntryLockAt = timeEntryLockAt,
        TimeZone = timeZone,
        IsDelete = isDelete,
    };

    /// <summary>
    /// A <c>dbo.TimeLog</c> row for <see cref="UserId"/>, on one day unless
    /// <paramref name="endDate"/> says otherwise.
    /// </summary>
    public static TimeLog Entry(
        DateTime date,
        TimeSpan start,
        TimeSpan end,
        int userId = UserId,
        string? sheetCode = "SHT0001",
        TimeLogStatus? status = TimeLogStatus.Save,
        DateTime? endDate = null,
        int? taskId = TaskId,
        bool? isProjectTask = true,
        string? description = "Seeded entry.") => new()
    {
        UserId = userId,
        SheetCode = sheetCode,
        TaskId = taskId,
        IsProjectTask = isProjectTask,
        Description = description,
        StartDate = date.Date,
        EndDate = (endDate ?? date).Date,
        StartTime = start,
        EndTime = end,
        Status = status,
    };

    public static TimeSpan At(int hours, int minutes = 0) => new(hours, minutes, 0);

    // ================================================================ factory helpers

    /// <summary>
    /// Inserts one entry with an id the test picks.
    /// <para>
    /// <c>IDENTITY_INSERT</c> rather than a generated id, because the edit path
    /// reads <c>timeLogId: 0</c> as "add": the reset reseeds identity to 0, and
    /// the first row a never-used table receives after that is given <b>0</b>.
    /// </para>
    /// </summary>
    public static Task SeedTimeLogWithIdAsync(
        this ETimeSheetApiFactory factory,
        int sheetId,
        TimeLog entry,
        bool isDeleted = false) =>
        factory.WithDbContextAsync(async db =>
        {
            int? status = entry.Status is { } value ? (int)value : null;
            var deleted = isDeleted ? 1 : 0;
            var createDate = factory.Clock.UtcNow;

            await db.Database.ExecuteSqlInterpolatedAsync($@"
                SET IDENTITY_INSERT dbo.TimeLog ON;
                INSERT INTO dbo.TimeLog
                    (SheetID, SheetCode, TaskID, [Description], StartTime, EndTime, StartDate, EndDate,
                     CreatedBy, CreateDate, [Status], IsDeleted, UserID, IsProjectTask)
                VALUES
                    ({sheetId}, {entry.SheetCode}, {entry.TaskId}, {entry.Description}, {entry.StartTime},
                     {entry.EndTime}, {entry.StartDate}, {entry.EndDate}, {entry.CreatedBy}, {createDate},
                     {status}, {deleted}, {entry.UserId}, {entry.IsProjectTask});
                SET IDENTITY_INSERT dbo.TimeLog OFF;");
        });

    /// <summary>
    /// Overwrites an entry's <c>CreateDate</c>.
    /// <para>
    /// Needed because the audit interceptor stamps every insert with the frozen
    /// clock, so seeded rows would otherwise all share one <c>CreateDate</c> -
    /// and <c>CreateDate DESC</c> is the order the list endpoint promises.
    /// </para>
    /// </summary>
    public static Task SetTimeLogCreateDateAsync(
        this ETimeSheetApiFactory factory,
        int sheetId,
        DateTime createDate) =>
        factory.WithDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.TimeLog SET CreateDate = {createDate} WHERE SheetID = {sheetId};"));

    /// <summary>Every row a user has in <c>dbo.TimeLog</c>, deleted or not, in id order.</summary>
    public static Task<List<TimeLog>> TimeLogRowsAsync(this ETimeSheetApiFactory factory, int userId) =>
        factory.WithDbContextAsync(db => db.TimeLog
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(entry => entry.UserId == userId)
            .OrderBy(entry => entry.SheetId)
            .ToListAsync());

    /// <summary>One entry straight from the table, deleted or not.</summary>
    public static Task<TimeLog?> FindTimeLogAsync(this ETimeSheetApiFactory factory, int sheetId) =>
        factory.WithDbContextAsync(db => db.TimeLog
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(entry => entry.SheetId == sheetId));

    /// <summary>Gives an already-seeded <c>dbo.Signup</c> row a country.</summary>
    public static Task SetSignupCountryAsync(this ETimeSheetApiFactory factory, int userId, int countryId) =>
        factory.WithDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.Signup SET CountryID = {countryId} WHERE UserID = {userId};"));

    /// <summary>
    /// Today <b>as SQL Server sees it</b> - <c>CAST(GETDATE() AS DATE)</c>, the
    /// expression <c>spc_GetUserDashboardSummaryByUserID</c> starts from. Asked
    /// of the database for the reason given on
    /// <see cref="ETimeSheetApiFactory.CurrentWeekStartAsync"/>.
    /// </summary>
    public static Task<DateTime> ServerTodayAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(async db =>
        {
            var connection = db.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CAST(GETDATE() AS DATE);";

            return (DateTime)(await command.ExecuteScalarAsync())!;
        });

    /// <summary>
    /// <c>dbo.DayMaster.DayID</c> of <paramref name="date"/>: 1 = Monday ... 7 =
    /// Sunday, as the procedures number days.
    /// </summary>
    public static int DayIdOf(DateTime date) => ((int)date.DayOfWeek + 6) % 7 + 1;
}
