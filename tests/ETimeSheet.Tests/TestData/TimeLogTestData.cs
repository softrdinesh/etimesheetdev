using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Shared.Enums;
using ETimeSheet.Shared.Utilities;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Shared, intention-revealing test data. Every value is anchored to
/// <see cref="Now"/> so that tests never depend on the real date.
/// </summary>
public static class TimeLogTestData
{
    /// <summary>The instant every test clock is frozen at.</summary>
    public static readonly DateTime Now = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>UTC midnight of <see cref="Now"/>.</summary>
    public static readonly DateTime Today = Now.ToUtcDate();

    public const int EmployeeUserId = 1001;
    public const int OtherEmployeeUserId = 1002;
    public const int ManagerUserId = 2001;

    /// <summary>
    /// An entry as it would already exist in the database. The API is read-only,
    /// so every scenario starts from rows that are seeded directly.
    /// <para>
    /// The real table keeps the day and the clock time in separate columns
    /// (<c>date</c> and <c>time(7)</c>), so this builder does too rather than
    /// flattening them into an instant.
    /// </para>
    /// </summary>
    public static TimeLog Existing(
        int? userId = EmployeeUserId,
        TimeLogStatus? status = TimeLogStatus.Draft,
        DateTime? startDate = null,
        TimeSpan? startTime = null,
        int durationMinutes = 180,
        string? description = "Existing entry.",
        string? sheetCode = null,
        int? taskId = null)
    {
        var date = startDate ?? Today;
        var start = startTime ?? TimeSpan.FromHours(8);

        return new TimeLog
        {
            // SheetID is an identity column, so the database assigns it.
            SheetCode = sheetCode,
            TaskId = taskId,
            UserId = userId,
            StartDate = date,
            EndDate = date,
            StartTime = start,
            EndTime = start.Add(TimeSpan.FromMinutes(durationMinutes)),
            Description = description,
            Status = status,
            CreateDate = date.Add(start)
        };
    }
}
