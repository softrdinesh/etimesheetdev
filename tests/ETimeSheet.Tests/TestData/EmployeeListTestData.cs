using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;
using ETimeSheet.Shared.Utilities;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Shared data for the employee-list endpoint.
/// <para>
/// <b>These dates are anchored to the real clock, not to
/// <see cref="TimeLogTestData.Now"/>.</b> <c>spc_GetEmployeeListByPOrgID</c>
/// derives "this week" from <c>GETDATE()</c> inside the procedure, where no
/// substituted <c>IDateTimeProvider</c> can reach it - exactly as the JWT
/// lifetime validator reads <c>DateTime.UtcNow</c>. An entry arranged against
/// the frozen test clock would fall outside the procedure's window and count as
/// nothing, so the week has to be worked out the same way the procedure does.
/// </para>
/// </summary>
public static class EmployeeListTestData
{
    /// <summary>The organisation under test. A second one exists so the filter has something to exclude.</summary>
    public const int OrganizationId = 700;

    public const int OtherOrganizationId = 800;

    /// <summary>
    /// The role the procedure <b>used</b> to treat as "employee" -
    /// <c>Signup.RoleID = 2</c>. Kept as the default for seeded rows so existing
    /// arrangements read unchanged.
    /// <para>
    /// <b>It no longer selects anything.</b> The procedure's <c>RoleID = 2</c>
    /// predicate was commented out on 2026-09-22, so every non-deleted person in
    /// the organisation is returned whatever their role.
    /// </para>
    /// <para>
    /// <b>Not <see cref="ETimeSheet.Shared.Enums.RoleType"/>.</b> That enum calls
    /// 2 a Manager. The procedure's vocabulary and the JWT's genuinely differ,
    /// and this constant exists so a test states which one it means rather than
    /// letting a reader assume.
    /// </para>
    /// </summary>
    public const int SignupEmployeeRoleId = 2;

    /// <summary>
    /// A role that is not <see cref="SignupEmployeeRoleId"/>. It used to be
    /// excluded by the procedure; since 2026-09-22 it is returned like any
    /// other, and the constant exists to prove exactly that.
    /// </summary>
    public const int SignupNonEmployeeRoleId = 1;

    public const int FullTimeUserId = 5001;
    public const int PartTimeUserId = 5002;
    public const int NoSetupUserId = 5003;
    public const int OtherOrganizationUserId = 5004;
    public const int NonEmployeeUserId = 5005;

    /// <summary>Somebody whose <c>dbo.Signup</c> row is soft-deleted.</summary>
    public const int DeletedUserId = 5006;

    // There is deliberately no WeekStart here. The procedure derives the week
    // from GETDATE() inside SQL Server, and the container's timezone need not
    // match this machine's, so the week has to be asked OF THE DATABASE:
    // ETimeSheetApiFactory.CurrentWeekStartAsync(). A host-side DateTime.Today
    // calculation would disagree by a day near midnight - and by a whole week
    // when that midnight is Sunday's.

    /// <summary>
    /// One <c>dbo.Signup</c> row. The table has no entity, so tests describe it
    /// themselves.
    /// </summary>
    /// <param name="IsDelete">
    /// The soft-delete flag the procedure has filtered on since 2026-09-22.
    /// Seeded explicitly rather than left to the column default, so a row can
    /// never disappear from the endpoint's results without a test saying so.
    /// </param>
    public record SignupRow(
        int UserId,
        string Name,
        string Email,
        int RoleId,
        int OrganizationId,
        bool IsDelete = false);

    /// <summary>
    /// A person in <see cref="OrganizationId"/>, live rather than soft-deleted.
    /// <para>
    /// <b>Not necessarily an "employee" any more.</b> The procedure dropped its
    /// <c>RoleID = 2</c> filter on 2026-09-22, so <paramref name="roleId"/> no
    /// longer decides whether the row comes back - only
    /// <paramref name="organizationId"/> and <paramref name="isDelete"/> do.
    /// The default is kept at the old employee role so existing arrangements
    /// read the same.
    /// </para>
    /// </summary>
    public static SignupRow Employee(
        int userId,
        string? name = null,
        int roleId = SignupEmployeeRoleId,
        int organizationId = OrganizationId,
        bool isDelete = false) =>
        new(userId,
            name ?? $"Employee {userId}",
            $"employee{userId}@etimesheet.test",
            roleId,
            organizationId,
            isDelete);

    /// <summary>
    /// A timesheet setup that yields a whole number of contracted hours.
    /// <para>
    /// The procedure multiplies <c>(EndDay - StartDay + 1)</c> by the daily
    /// minutes, so a Monday-to-Friday week (1 to 5) at 8 hours a day is
    /// 5 x 480 = 2400 minutes - 40 hours exactly, with no remainder to reason
    /// about in an assertion.
    /// </para>
    /// </summary>
    public static TimesheetMasterSetup Setup(
        int userId,
        int contractType,
        int? startDay = Constants.DayMaster.DayId.Monday,
        int? endDay = Constants.DayMaster.DayId.Friday,
        TimeSpan? maxTimeInHrs = null,
        TimeSpan? maxTimInMins = null,
        int organizationId = OrganizationId) =>
        new()
        {
            UserId = userId,
            OrganizationId = organizationId,
            ContractType = contractType,
            StartDay = startDay,
            EndDay = endDay,
            MaxTimeInHrs = maxTimeInHrs ?? TimeSpan.FromHours(8),

            // Not null, and not a second quantity of hours: the procedure reads
            // only DATEPART(MINUTE, ...) of this column. It must be present
            // though - the expected-time calculation returns NULL if it is not.
            MaxTimInMins = maxTimInMins ?? TimeSpan.Zero,
            IsDelete = false
        };

    /// <summary>
    /// One row as the procedure would return it, for unit tests - which never
    /// touch SQL Server and so build the result set by hand.
    /// </summary>
    public static EmployeeListDetail Row(
        int userId,
        int? setupId = 1,
        int? contractTypeId = ETimeSheet.Shared.Utilities.Constants.TimesheetMasterSetup.ContractType.FullTime,
        string? contractType = "Full Time") =>
        new()
        {
            UserId = userId,
            SetupId = setupId,
            Name = $"Employee {userId}",
            Email = $"employee{userId}@etimesheet.test",
            ExpectedHoursPerWeek = setupId is null ? null : 40,
            ExpectedMinsPerWeek = setupId is null ? null : 0,
            ExpectedHoursPerWeekText = setupId is null ? null : "40h/week",
            TotalLoggedHoursCurrentWeek = 0,
            TotalLoggedMinsCurrentWeek = 0,
            TotalLoggedHoursCurrentWeekText = "0h",
            ProgressOnThisWeek = 0m,
            ContractTypeId = contractTypeId,
            ContractType = contractType
        };
}
