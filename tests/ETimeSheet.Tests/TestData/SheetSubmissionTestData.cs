using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Shared.Utilities;
using ETimeSheet.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Arrangements for the SheetSubmission endpoint integration tests.
/// <para>
/// Submit, review and the current-week read all run on the API's frozen clock
/// (<see cref="TimeLogTestData.Now"/>, Tuesday 2026-03-10 12:00 UTC);
/// <c>spc_GetSubmittedSheetList</c> reads no clock at all, so every date here is
/// fixed rather than relative to the real one.
/// </para>
/// </summary>
public static class SheetSubmissionTestData
{
    public const int UserId = 4101;

    public const int SecondUserId = 4102;

    /// <summary>A user in another organisation, to prove the list's tenant filter.</summary>
    public const int OtherOrganizationUserId = 4103;

    /// <summary>A user whose <c>dbo.Signup</c> row is soft-deleted.</summary>
    public const int DeletedUserId = 4104;

    public const int AdminId = 4900;

    public const int SecondAdminId = 4901;

    public const int OrganizationId = 910;

    public const int OtherOrganizationId = 920;

    public const string SheetCode = "SHT0001";

    public const string SecondSheetCode = "SHT0002";

    public const string ThirdSheetCode = "SHT0003";

    public static SheetSubmissionRequest SubmitRequest(
        string? sheetCode = SheetCode,
        int userId = UserId) => new()
    {
        SheetCode = sheetCode,
        UserId = userId,
        OrgId = OrganizationId,
    };

    public static SheetSubmissionReviewRequest ReviewRequest(
        int status,
        string? sheetCode = SheetCode,
        int adminId = AdminId) => new()
    {
        AdminId = adminId,
        SheetCode = sheetCode,
        Status = status,
        OrgId = OrganizationId,
    };

    public static SubmittedSheetListRequest ListRequest(
        int userId = 0,
        int orgId = OrganizationId,
        int adminId = AdminId) => new()
    {
        AdminId = adminId,
        UserId = userId,
        OrgId = orgId,
    };

    /// <summary>A person in <see cref="OrganizationId"/> unless told otherwise.</summary>
    public static EmployeeListTestData.SignupRow Person(
        int userId,
        int organizationId = OrganizationId,
        bool isDelete = false) =>
        new(userId,
            $"Submitter {userId}",
            $"submitter{userId}@etimesheet.test",
            EmployeeListTestData.SignupEmployeeRoleId,
            organizationId,
            isDelete);

    /// <summary>
    /// A <c>dbo.TimesheetSubmission</c> row as submit-timesheet would have
    /// written it: status 1, with the totals given.
    /// </summary>
    public static TimesheetSubmission Submission(
        string sheetCode = SheetCode,
        int submittedBy = UserId,
        int totalMinutes = 2100,
        DateTime? submittedDate = null,
        int? statusId = Constants.TimesheetSubmission.Status.Submitted) => new()
    {
        Timesheetcode = sheetCode,
        TotalMins = totalMinutes,
        TotalHours = Math.Round(totalMinutes / 60m, 2, MidpointRounding.AwayFromZero),
        SubmittedBy = submittedBy,
        SubmittedDate = submittedDate ?? TimeLogTestData.Now.AddDays(-1),
        StatusId = statusId,
    };

    // ================================================================ factory helpers

    public static Task SeedSubmissionsAsync(
        this ETimeSheetApiFactory factory,
        params TimesheetSubmission[] submissions) =>
        factory.WithDbContextAsync(async db =>
        {
            db.TimesheetSubmission.AddRange(submissions);
            await db.SaveChangesAsync();
        });

    /// <summary>Every submission row, in code order.</summary>
    public static Task<List<TimesheetSubmission>> SubmissionRowsAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.TimesheetSubmission
            .AsNoTracking()
            .OrderBy(submission => submission.Timesheetcode)
            .ToListAsync());

    /// <summary>Every audit entry, in the order they were written.</summary>
    public static Task<List<TimeSheetSubmissionAuditLog>> AuditRowsAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.TimeSheetSubmissionAuditLog
            .AsNoTracking()
            .OrderBy(entry => entry.TimeSheetSubmissionAuditLogId)
            .ToListAsync());
}
