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
    /// <para>
    /// <b><c>CountryId</c> and <c>TimeZone</c> go straight through.</b> The
    /// payload is the only source for both: nothing is looked up, nothing is
    /// cross-checked against <c>dbo.Country</c>, and whatever the caller sent is
    /// what lands in the row. An earlier version derived <c>TimeZone</c> from
    /// the country and ignored the payload's value for a single-zone country -
    /// that is gone, and the two columns now behave like every other field here.
    /// </para>
    /// </summary>
    /// <param name="times">
    /// The payload's three time fields, already read out of their
    /// <c>hh:mm:ss</c> strings. They are passed in rather than parsed here
    /// because parsing is validation - it can reject the payload - and a mapper
    /// is not where a request is accepted or refused.
    /// </param>
    internal static void ApplyTo(
        this AdminSaveRequest request,
        TimesheetMasterSetup setup,
        TimesheetSetupTimes times)
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
        setup.TimeZone = request.TimeZone;
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
        ContractType = employee.ContractType,
        CountryId = employee.CountryId
    };

    internal static IReadOnlyCollection<EmployeeResponse> ToResponses(
        this IEnumerable<EmployeeListDetail> employees) =>
        employees.Select(ToResponse).ToArray();

    /// <summary>
    /// Flattens countries into one <see cref="CountryTimeZoneResponse"/> per
    /// time zone - the shape the setup screen's picker binds to.
    /// <para>
    /// This is where the list stops being nested: a country with three zones
    /// becomes three rows carrying the same <c>CountryId</c> and three different
    /// zones. <c>CountryTimeZones.Split</c> does the unpacking, the same
    /// splitter the save resolves a chosen zone with, so the picker can never
    /// offer a pairing the save then refuses.
    /// </para>
    /// <para>
    /// A country whose <c>TimeZone</c> column is empty contributes <b>no</b>
    /// rows. Listing it would offer a choice that the save answers 400 to, and a
    /// picker's job is to only contain valid answers.
    /// </para>
    /// <para>
    /// Order is preserved from both sides: the countries in the order the
    /// repository read them, and within each one the zones in the order the
    /// column lists them - so a country's primary zone is its first row.
    /// </para>
    /// </summary>
    internal static IReadOnlyList<CountryTimeZoneResponse> ToCountryTimeZoneResponses(
        this IEnumerable<Country> countries) =>
        countries
            .SelectMany(country => CountryTimeZones
                .Split(country.TimeZone)
                .Select(zone =>
                {
                    // Trimmed once and used for both the standalone field and
                    // the label, so the two can never disagree about what the
                    // country is called.
                    var name = country.Name?.Trim() ?? string.Empty;

                    return new CountryTimeZoneResponse
                    {
                        CountryId = country.Id,
                        CountryName = name,
                        TimeZone = zone,

                        // A nameless country yields the bare zone rather than a
                        // label with a leading hyphen: the row is broken
                        // reference data either way, and a dangling separator
                        // just looks like the API dropped something.
                        OptionValue = name.Length == 0 ? zone : $"{name}-{zone}"
                    };
                }))
            .ToArray();
}
