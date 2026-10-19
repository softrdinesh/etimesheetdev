using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Shared arrangements for the user access setup tests of
/// <c>TimesheetProjectController</c> - the module assignments in
/// <c>dbo.Timesheet_role_assignment</c> and the <c>dbo.Signup</c> users they
/// belong to.
/// <para>
/// Assignments are seeded with explicit ids under <c>IDENTITY_INSERT</c>: the
/// reset reseeds identity to 0, and a generated id of 0 would be read as
/// "add" by the endpoint and skipped by the one-assignment-per-user check.
/// </para>
/// </summary>
public static class UserAccessTestData
{
    /// <summary>The organisation the tests act for.</summary>
    public const int OrganizationId = 31;

    /// <summary>A second organisation, whose users must never leak into the first.</summary>
    public const int OtherOrganizationId = 32;

    // The users. The names are chosen so "ordered by user name" differs from
    // "ordered by id": Alice (712) sorts before Charlie (711).
    public const int CharlieUserId = 711;
    public const string CharlieName = "Charlie Brown";
    public const int AliceUserId = 712;
    public const string AliceName = "Alice Smith";

    /// <summary>A live user, but of <see cref="OtherOrganizationId"/>.</summary>
    public const int OtherOrganizationUserId = 713;

    /// <summary>A user of <see cref="OrganizationId"/> who has been removed (<c>isdelete = 1</c>).</summary>
    public const int RemovedUserId = 714;

    /// <summary>A user id with no <c>dbo.Signup</c> row at all.</summary>
    public const int UnknownUserId = 799;

    /// <summary>Who created a seeded assignment - deliberately not anyone a test sends.</summary>
    public const int SeedCreatorUserId = 50;

    /// <summary>Who a test says is saving.</summary>
    public const int LoginUserId = 51;

    /// <summary>When a seeded assignment was created - well before the frozen clock.</summary>
    public static readonly DateTime SeededCreateDate = new(2026, 1, 20, 8, 30, 0);

    /// <summary>The email <see cref="SeedAccessUsersAsync"/> gives a user.</summary>
    public static string EmailOf(int userId) => $"user{userId}@access.test";

    private static EmployeeListTestData.SignupRow User(
        int userId,
        string name,
        int organizationId = OrganizationId,
        bool isDelete = false) =>
        new(userId, name, EmailOf(userId), RoleId: 2, organizationId, isDelete);

    /// <summary>Inserts the four <c>dbo.Signup</c> users above, through the factory's own helper.</summary>
    public static Task SeedAccessUsersAsync(this ETimeSheetApiFactory factory) =>
        factory.SeedEmployeesAsync(
            User(CharlieUserId, CharlieName),
            User(AliceUserId, AliceName),
            User(OtherOrganizationUserId, "Bob Other", OtherOrganizationId),
            User(RemovedUserId, "Removed Person", isDelete: true));

    /// <summary>
    /// An assignment row as it would sit in <c>dbo.Timesheet_role_assignment</c>:
    /// only the client setup and time log modules, unless a test says otherwise.
    /// </summary>
    public static TimesheetRoleAssignment Assignment(int id, int userId, bool isProject = false) => new()
    {
        AssignmentId = id,
        UserId = userId,
        IsClientSetup = true,
        IsProject = isProject,
        IsProjectBudget = false,
        IsProjectCategory = false,
        IsStatusSetup = false,
        IsRoleSetup = false,
        IsUserSetup = false,
        IsTimeLog = true,
        CreateDate = SeededCreateDate,
        CreatedBy = SeedCreatorUserId,
    };

    /// <summary>
    /// A save payload for <see cref="CharlieUserId"/> that passes validation:
    /// the project, budget and status modules, which <see cref="Assignment"/>
    /// does not grant, so an edit visibly changes every flag.
    /// </summary>
    public static TimesheetRoleAssignmentSaveRequest AccessRequest(
        int? assignmentId = null,
        int userId = CharlieUserId,
        int organizationId = OrganizationId) => new()
    {
        AssignmentId = assignmentId,
        OrganizationId = organizationId,
        UserId = userId,
        IsClientSetup = false,
        IsProject = true,
        IsProjectBudget = true,
        IsProjectCategory = false,
        IsStatusSetup = true,
        IsRoleSetup = false,
        IsUserSetup = false,
        IsTimeLog = false,
        LoginUserId = LoginUserId,
    };

    /// <summary>Inserts assignments directly, with the ids they carry.</summary>
    public static Task SeedAccessAssignmentsAsync(
        this ETimeSheetApiFactory factory,
        params TimesheetRoleAssignment[] assignments) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var assignment in assignments)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.Timesheet_role_assignment ON;
                    INSERT INTO dbo.Timesheet_role_assignment
                        (AssignmentID, UserID,
                         Mod_IsclientSetup, Mod_Isproject, Mod_IsProjectbudget, Mod_IsProjectCategory,
                         Mod_IsStatusSetup, Mod_IsRoleSetup, Mod_IsUsersetup, Mod_IsTimeLog,
                         CreateDate, CreatedBy)
                    VALUES
                        ({assignment.AssignmentId}, {assignment.UserId},
                         {assignment.IsClientSetup}, {assignment.IsProject}, {assignment.IsProjectBudget},
                         {assignment.IsProjectCategory}, {assignment.IsStatusSetup}, {assignment.IsRoleSetup},
                         {assignment.IsUserSetup}, {assignment.IsTimeLog},
                         {assignment.CreateDate}, {assignment.CreatedBy});
                    SET IDENTITY_INSERT dbo.Timesheet_role_assignment OFF;");
            }
        });

    /// <summary>Reads one assignment straight from the table.</summary>
    public static Task<TimesheetRoleAssignment?> FindAccessAssignmentAsync(this ETimeSheetApiFactory factory, int id) =>
        factory.WithDbContextAsync(db => db.Timesheet_role_assignment
            .AsNoTracking()
            .FirstOrDefaultAsync(assignment => assignment.AssignmentId == id));

    /// <summary>Every assignment row in the table.</summary>
    public static Task<List<TimesheetRoleAssignment>> AllAccessAssignmentsAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.Timesheet_role_assignment
            .AsNoTracking()
            .ToListAsync());
}
