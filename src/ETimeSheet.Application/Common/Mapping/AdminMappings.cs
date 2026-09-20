using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Common.Mapping;

/// <summary>
/// Hand-written projections between <see cref="TimesheetMasterSetup"/> and the
/// Admin DTOs.
/// <para>
/// Explicit and compile-time checked, for the same reason as
/// <see cref="TimeLogMappings"/>: a convention-based mapper would silently drop
/// a column the day one of these names changes.
/// </para>
/// </summary>
internal static class AdminMappings
{
    internal static AdminResponse ToResponse(this TimesheetMasterSetup setup) => new()
    {
        SetupId = setup.SetupId,
        UserId = setup.UserId,
        MaxTimeInHrs = setup.MaxTimeInHrs,
        MaxTimInMins = setup.MaxTimInMins,
        OrganizationId = setup.OrganizationId,
        ContractType = setup.ContractType,

        // Day ids straight through - dbo.DayMaster.DayID, 1 = Monday ... 7 =
        // Sunday. Not translated to names here: the response carries the stored
        // value, and dbo.DayMaster is what turns it into a name.
        StartDay = setup.StartDay,
        EndDay = setup.EndDay,
        ExceptionDay = setup.ExceptionDay,

        CountryId = setup.CountryId,
        TimeZone = setup.TimeZone,
        TimeEntryLockAt = setup.TimeEntryLockAt,
        CreatedBy = setup.CreatedBy,
        CreateDate = setup.CreateDate,
        UpdatedBy = setup.UpdatedBy,
        UpdateDate = setup.UpdateDate
    };

    /// <summary>
    /// Copies the caller-settable columns of a save request onto an entity.
    /// <para>
    /// It deliberately touches <b>only</b> those columns. The key, the
    /// soft-delete flag and every audit column are left alone, so a payload can
    /// never rewrite who created a row or move a setup to a different id.
    /// Reviving a deleted row is a decision, not a side effect of copying
    /// fields, so <c>AdminService</c> makes it after calling this.
    /// </para>
    /// <para>
    /// Used for both the insert and the update path, which is what guarantees
    /// the two cannot drift apart and start accepting different fields.
    /// </para>
    /// </summary>
    /// <param name="times">
    /// The payload's three time fields, already read out of their
    /// <c>hh:mm:ss</c> strings. They are passed in rather than parsed here
    /// because parsing is validation - it can reject the payload - and a mapper
    /// is not where a request is accepted or refused.
    /// </param>
    /// <param name="timeZone">
    /// The setup's time zone, <b>already resolved against the country</b> - the
    /// country's own zone when it has one, or the one the payload chose from its
    /// list when it has several. Passed in for the same reason as
    /// <paramref name="times"/>: resolving it reads the database and can reject
    /// the payload, and neither belongs in a mapper.
    /// <para>
    /// This is why <c>request.TimeZone</c> is deliberately <b>not</b> read
    /// below. Copying it straight across would store whatever the caller typed,
    /// which is the exact thing the resolution exists to prevent.
    /// </para>
    /// </param>
    internal static void ApplyTo(
        this AdminSaveRequest request,
        TimesheetMasterSetup setup,
        TimesheetSetupTimes times,
        string timeZone)
    {
        setup.UserId = request.UserId;
        setup.MaxTimeInHrs = times.MaxTimeInHrs;
        setup.MaxTimInMins = times.MaxTimInMins;
        setup.OrganizationId = request.OrganizationId;
        setup.ContractType = request.ContractType;
        setup.StartDay = request.StartDay;
        setup.EndDay = request.EndDay;
        setup.ExceptionDay = request.ExceptionDay;
        setup.CountryId = request.CountryId;
        setup.TimeZone = timeZone;
        setup.TimeEntryLockAt = times.TimeEntryLockAt;
    }

    /// <summary>
    /// Projects one row of <c>dbo.spc_GetEmployeeListByPOrgID</c> to the grid.
    /// <para>
    /// Straight through, deliberately: every figure in the row was computed by
    /// the procedure, and recomputing or reformatting one here would let this
    /// endpoint and the procedure tell an administrator two different numbers.
    /// </para>
    /// </summary>
    internal static EmployeeResponse ToResponse(this EmployeeListDetail employee) => new()
    {
        UserId = employee.UserId,
        SetupId = employee.SetupId,
        Name = employee.Name,
        Email = employee.Email,
        ExpectedHoursPerWeek = employee.ExpectedHoursPerWeek,
        ExpectedMinsPerWeek = employee.ExpectedMinsPerWeek,
        ExpectedHoursPerWeekText = employee.ExpectedHoursPerWeekText,
        TotalLoggedHoursCurrentWeek = employee.TotalLoggedHoursCurrentWeek,
        TotalLoggedMinsCurrentWeek = employee.TotalLoggedMinsCurrentWeek,
        TotalLoggedHoursCurrentWeekText = employee.TotalLoggedHoursCurrentWeekText,
        ProgressOnThisWeek = employee.ProgressOnThisWeek,
        ContractTypeId = employee.ContractTypeId,
        ContractType = employee.ContractType
    };

    internal static IReadOnlyCollection<EmployeeResponse> ToResponses(
        this IEnumerable<EmployeeListDetail> employees) =>
        employees.Select(ToResponse).ToArray();
}
