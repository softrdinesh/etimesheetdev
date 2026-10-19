using System.Net;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Shared.Enums;
using ETimeSheet.Tests.Fixtures;
using ETimeSheet.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Shared arrangements for the project time log and project sheet submission
/// endpoints of <c>TimesheetProjectController</c>: the people, projects and
/// assignments an entry needs, row builders for
/// <c>dbo.TimesheetProjectTimeLog</c> and <c>dbo.TimesheetProjectSubmission</c>,
/// and seed / read-back helpers over
/// <see cref="ETimeSheetApiFactory.WithDbContextAsync(Func{ETimeSheet.Infrastructure.Data.Context, Task})"/>.
/// <para>
/// Every helper reaches the database only through <c>WithDbContextAsync</c> (or
/// the factory's own <c>SeedEmployeesAsync</c>), so every one of them passes
/// <c>ThrowawayDatabaseGuard</c> first. Rows only, never schema.
/// </para>
/// <para>
/// Rows are inserted with explicit ids under <c>IDENTITY_INSERT</c>, as
/// <c>SeedClientsAsync</c> does: the reset reseeds identity to 0, so the first
/// generated id a fresh table hands out can be <b>0</b> - which the add/edit
/// endpoint reads as "add", and the removes refuse outright.
/// </para>
/// </summary>
public static class ProjectTimeLogTestData
{
    public const string BaseUrl = "/api/v1/TimesheetProject";

    /// <summary>The organisation the seeded people and projects belong to.</summary>
    public const int OrganizationId = 31;

    /// <summary><c>dbo.TimesheetProject.ClientID</c> is NOT NULL; no foreign key, so any value serves.</summary>
    public const int ClientId = 1;

    /// <summary>The user whose time is logged in most tests. Assigned to Alpha and Beta.</summary>
    public const int UserId = 501;

    public const string UserName = "Asha Patel";

    /// <summary>A second user, assigned to Alpha only.</summary>
    public const int OtherUserId = 502;

    public const string OtherUserName = "Ben Roy";

    /// <summary>Somebody who reviews, removes or submits on behalf of others; never logs time.</summary>
    public const int ReviewerId = 900;

    public const int AlphaProjectId = 701;
    public const string AlphaProjectCode = "PRJ-ALPHA";
    public const string AlphaProjectName = "Alpha Build";

    public const int BetaProjectId = 702;
    public const string BetaProjectCode = "PRJ-BETA";
    public const string BetaProjectName = "Beta Support";

    /// <summary>A project nobody is assigned to.</summary>
    public const int UnassignedProjectId = 703;

    /// <summary>A day before the frozen clock's 2026-03-10, so nothing is "in the future".</summary>
    public static readonly DateTime LogDate = new(2026, 3, 9);

    /// <summary>
    /// The code the service generates: <c>PSH-{userId}-{sequence:0000}</c>.
    /// </summary>
    public static string SheetCode(int userId, int sequence) => $"PSH-{userId}-{sequence:0000}";

    // ================================================================ payloads

    /// <summary>
    /// An add payload that passes validation for <see cref="UserId"/> on
    /// <see cref="AlphaProjectId"/>, 09:00 to 10:30 (90 minutes). A test
    /// changes only what it is about.
    /// </summary>
    public static TimesheetProjectTimeLogSaveRequest SaveRequest(
        int? timesheetProjectTimeLogId = null,
        int userId = UserId,
        int projectId = AlphaProjectId,
        DateTime? logDate = null,
        string? startTime = "09:00:00",
        string? endTime = "10:30:00",
        string? description = "Wrote the importer",
        TimeLogStatus? status = TimeLogStatus.Save,
        int? addedBy = null) => new()
    {
        TimesheetProjectTimeLogId = timesheetProjectTimeLogId,
        UserId = userId,
        ProjectId = projectId,
        LogDate = logDate ?? LogDate,
        StartTime = startTime,
        EndTime = endTime,
        Description = description,
        Status = status,
        AddedBy = addedBy ?? userId,
    };

    // ================================================================ rows

    /// <summary>
    /// A <c>dbo.TimesheetProjectTimeLog</c> row. <c>EndTime</c> is worked out
    /// from <paramref name="startTime"/> and <paramref name="durationMinutes"/>
    /// so the two can never disagree in an arrangement.
    /// </summary>
    public static TimesheetProjectTimeLog Entry(
        int id,
        string sheetSubmissionCode,
        DateTime? logDate = null,
        int durationMinutes = 60,
        string startTime = "09:00:00",
        int userId = UserId,
        int projectId = AlphaProjectId,
        string? description = "Seeded work",
        int status = 1,
        bool isDeleted = false)
    {
        var start = TimeSpan.Parse(startTime, System.Globalization.CultureInfo.InvariantCulture);
        var end = TimeSpan.FromTicks((start + TimeSpan.FromMinutes(durationMinutes)).Ticks % TimeSpan.TicksPerDay);

        return new TimesheetProjectTimeLog
        {
            TimesheetProjectTimeLogId = id,
            UserId = userId,
            ProjectId = projectId,
            SheetSubmissionCode = sheetSubmissionCode,
            LogDate = (logDate ?? LogDate).Date,
            StartTime = start,
            EndTime = end,
            DurationMinutes = durationMinutes,
            Description = description,
            Status = status,
            IsDelete = isDeleted ? 1 : 0,
            CreateDate = SeededCreateDate,
            CreatedBy = userId,
            DeletedDate = isDeleted ? SeededCreateDate : null,
            DeletedBy = isDeleted ? userId : null,
        };
    }

    /// <summary>When every seeded row says it was created: well before the frozen clock.</summary>
    public static readonly DateTime SeededCreateDate = new(2026, 3, 1, 8, 0, 0);

    /// <summary>
    /// A <c>dbo.TimesheetProjectSubmission</c> row whose review stamps match
    /// its status: approved carries the approval pair, rejected the rejection
    /// pair and a reason, submitted neither.
    /// </summary>
    public static TimesheetProjectSubmission Sheet(
        int id,
        string sheetSubmissionCode,
        int statusId = 1,
        int submittedBy = UserId,
        decimal totalMinutes = 0,
        string? rejectionReason = "Seeded rejection",
        bool isDeleted = false) => new()
    {
        TimesheetProjectSubmissionId = id,
        SheetSubmissionCode = sheetSubmissionCode,
        TotalMinutes = totalMinutes,
        TotalHours = Math.Round(totalMinutes / 60m, 2, MidpointRounding.AwayFromZero),
        StatusId = statusId,
        SubmittedBy = submittedBy,
        SubmittedDate = SeededCreateDate.AddDays(1),
        ApprovedBy = statusId == 2 ? ReviewerId : null,
        ApprovedDate = statusId == 2 ? SeededCreateDate.AddDays(2) : null,
        RejectedBy = statusId == 3 ? ReviewerId : null,
        RejectedDate = statusId == 3 ? SeededCreateDate.AddDays(2) : null,
        RejectionReason = statusId == 3 ? rejectionReason : null,
        IsDelete = isDeleted ? 1 : 0,
    };

    // ================================================================ seeding

    /// <summary>
    /// Seeds the cast every test needs: <see cref="UserId"/> and
    /// <see cref="OtherUserId"/> in <c>dbo.Signup</c>; Alpha, Beta and an
    /// unassigned project in <c>dbo.TimesheetProject</c>; and live assignments
    /// of <see cref="UserId"/> to Alpha and Beta and of
    /// <see cref="OtherUserId"/> to Alpha.
    /// </summary>
    public static async Task SeedTimeLogUsersAndProjectsAsync(this ETimeSheetApiFactory factory)
    {
        await factory.SeedEmployeesAsync(
            EmployeeListTestData.Employee(UserId, UserName, organizationId: OrganizationId),
            EmployeeListTestData.Employee(OtherUserId, OtherUserName, organizationId: OrganizationId));

        await factory.SeedTimeLogProjectAsync(AlphaProjectId, AlphaProjectCode, AlphaProjectName);
        await factory.SeedTimeLogProjectAsync(BetaProjectId, BetaProjectCode, BetaProjectName);
        await factory.SeedTimeLogProjectAsync(UnassignedProjectId, "PRJ-GAMMA", "Gamma Research");

        await factory.SeedTimeLogAssignmentAsync(UserId, AlphaProjectId);
        await factory.SeedTimeLogAssignmentAsync(UserId, BetaProjectId);
        await factory.SeedTimeLogAssignmentAsync(OtherUserId, AlphaProjectId);
    }

    /// <summary>One <c>dbo.TimesheetProject</c> row with an explicit id.</summary>
    public static Task SeedTimeLogProjectAsync(
        this ETimeSheetApiFactory factory,
        int projectId,
        string code,
        string projectName) =>
        factory.WithDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($@"
            SET IDENTITY_INSERT dbo.TimesheetProject ON;
            INSERT INTO dbo.TimesheetProject (ProjectID, Code, Projectname, OrganizationID, ClientID)
            VALUES ({projectId}, {code}, {projectName}, {OrganizationId}, {ClientId});
            SET IDENTITY_INSERT dbo.TimesheetProject OFF;"));

    /// <summary>
    /// One <c>dbo.Timesheet_Users_Project_setup</c> row. Its own id is
    /// generated: nothing in these tests refers to it.
    /// </summary>
    public static Task SeedTimeLogAssignmentAsync(
        this ETimeSheetApiFactory factory,
        int userId,
        int projectId,
        bool isDeleted = false) =>
        factory.WithDbContextAsync(db => db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO dbo.Timesheet_Users_Project_setup (UserID, ProjectID, IsDelete, CreateDate, Createdby)
            VALUES ({userId}, {projectId}, {(isDeleted ? 1 : 0)}, {SeededCreateDate}, {ReviewerId});"));

    /// <summary>Inserts <c>dbo.TimesheetProjectTimeLog</c> rows with the ids they carry.</summary>
    public static Task SeedTimeLogEntriesAsync(
        this ETimeSheetApiFactory factory,
        params TimesheetProjectTimeLog[] entries) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var entry in entries)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetProjectTimeLog ON;
                    INSERT INTO dbo.TimesheetProjectTimeLog
                        (TimesheetProjectTimeLogID, UserID, ProjectID, SheetSubmissionCode, LogDate,
                         StartTime, EndTime, DurationMinutes, Description, Status, IsDelete,
                         CreateDate, CreatedBy, DeletedDate, DeletedBy)
                    VALUES
                        ({entry.TimesheetProjectTimeLogId}, {entry.UserId}, {entry.ProjectId},
                         {entry.SheetSubmissionCode}, {entry.LogDate}, {entry.StartTime}, {entry.EndTime},
                         {entry.DurationMinutes}, {entry.Description}, {entry.Status}, {entry.IsDelete},
                         {entry.CreateDate}, {entry.CreatedBy}, {entry.DeletedDate}, {entry.DeletedBy});
                    SET IDENTITY_INSERT dbo.TimesheetProjectTimeLog OFF;");
            }
        });

    /// <summary>Inserts <c>dbo.TimesheetProjectSubmission</c> rows with the ids they carry.</summary>
    public static Task SeedTimeLogSubmissionsAsync(
        this ETimeSheetApiFactory factory,
        params TimesheetProjectSubmission[] sheets) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var sheet in sheets)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetProjectSubmission ON;
                    INSERT INTO dbo.TimesheetProjectSubmission
                        (TimesheetProjectSubmissionID, SheetSubmissionCode, TotalHours, TotalMinutes, StatusID,
                         SubmittedBy, SubmittedDate, ApprovedBy, ApprovedDate, RejectedBy, RejectedDate,
                         RejectionReason, IsDelete)
                    VALUES
                        ({sheet.TimesheetProjectSubmissionId}, {sheet.SheetSubmissionCode}, {sheet.TotalHours},
                         {sheet.TotalMinutes}, {sheet.StatusId}, {sheet.SubmittedBy}, {sheet.SubmittedDate},
                         {sheet.ApprovedBy}, {sheet.ApprovedDate}, {sheet.RejectedBy}, {sheet.RejectedDate},
                         {sheet.RejectionReason}, {sheet.IsDelete});
                    SET IDENTITY_INSERT dbo.TimesheetProjectSubmission OFF;");
            }
        });

    /// <summary>One audit row against a sheet; its own id is generated.</summary>
    public static Task SeedTimeLogSubmissionAuditAsync(
        this ETimeSheetApiFactory factory,
        int timesheetProjectSubmissionId,
        int statusId) =>
        factory.WithDbContextAsync(async db =>
        {
            db.TimesheetProjectSubmissionAuditLog.Add(new TimesheetProjectSubmissionAuditLog
            {
                TimesheetProjectSubmissionId = timesheetProjectSubmissionId,
                StatusId = statusId,
                CreatedBy = UserId,
                CreateDate = SeededCreateDate.AddDays(1),
            });

            await db.SaveChangesAsync();
        });

    // ================================================================ read-back

    /// <summary>One entry straight from the table, deleted or not.</summary>
    public static Task<TimesheetProjectTimeLog?> ReadTimeLogEntryAsync(
        this ETimeSheetApiFactory factory,
        int timesheetProjectTimeLogId) =>
        factory.WithDbContextAsync(db => db.TimesheetProjectTimeLog
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(entry => entry.TimesheetProjectTimeLogId == timesheetProjectTimeLogId));

    /// <summary>Every entry in the table, deleted or not, in id order.</summary>
    public static Task<List<TimesheetProjectTimeLog>> ReadTimeLogEntriesAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.TimesheetProjectTimeLog
            .AsNoTracking()
            .IgnoreQueryFilters()
            .OrderBy(entry => entry.TimesheetProjectTimeLogId)
            .ToListAsync());

    /// <summary>One sheet straight from the table, deleted or not.</summary>
    public static Task<TimesheetProjectSubmission?> ReadTimeLogSubmissionAsync(
        this ETimeSheetApiFactory factory,
        int timesheetProjectSubmissionId) =>
        factory.WithDbContextAsync(db => db.TimesheetProjectSubmission
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(sheet => sheet.TimesheetProjectSubmissionId == timesheetProjectSubmissionId));

    /// <summary>Every sheet in the table, deleted or not, in id order.</summary>
    public static Task<List<TimesheetProjectSubmission>> ReadTimeLogSubmissionsAsync(
        this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.TimesheetProjectSubmission
            .AsNoTracking()
            .IgnoreQueryFilters()
            .OrderBy(sheet => sheet.TimesheetProjectSubmissionId)
            .ToListAsync());

    /// <summary>The whole audit trail, in the order it was written.</summary>
    public static Task<List<TimesheetProjectSubmissionAuditLog>> ReadTimeLogSubmissionAuditsAsync(
        this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.TimesheetProjectSubmissionAuditLog
            .AsNoTracking()
            .OrderBy(audit => audit.TimesheetProjectSubmissionAuditLogId)
            .ToListAsync());

    // ================================================================ assertions

    /// <summary>
    /// Asserts a 400 in the standard envelope that names every one of
    /// <paramref name="fields"/>. The service reports each failure as
    /// <c>"Field: message"</c>, so the prefix is what identifies the field.
    /// </summary>
    public static async Task<IReadOnlyCollection<string>> AssertTimeLogValidationFailureAsync(
        this HttpResponseMessage response,
        params string[] fields)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<object>();

        Assert.False(envelope.Success);

        foreach (var field in fields)
        {
            Assert.Contains(envelope.Errors, error => error.StartsWith(field + ":", StringComparison.Ordinal));
        }

        return envelope.Errors;
    }

    /// <summary>Asserts a failure status in the standard envelope.</summary>
    public static async Task AssertTimeLogFailureAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);

        var envelope = await response.ReadEnvelopeAsync<object>();

        Assert.False(envelope.Success);
        Assert.Null(envelope.Data);
    }
}
