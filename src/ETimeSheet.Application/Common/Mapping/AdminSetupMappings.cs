using ETimeSheet.Application.DTOs.AdminSetups;
using ETimeSheet.Application.Models.Entities;

namespace ETimeSheet.Application.Common.Mapping;

/// <summary>
/// Hand-written projections between <see cref="TimesheetMasterSetup"/> and the
/// AdminSetup DTOs.
/// <para>
/// Explicit and compile-time checked, for the same reason as
/// <see cref="TimeLogMappings"/>: a convention-based mapper would silently drop
/// a column the day one of these names changes.
/// </para>
/// </summary>
internal static class AdminSetupMappings
{
    internal static AdminSetupResponse ToResponse(this TimesheetMasterSetup setup) => new()
    {
        SetupId = setup.SetupId,
        UserId = setup.UserId,
        MaxTimeInHrs = setup.MaxTimeInHrs,
        MaxTimInMins = setup.MaxTimInMins,
        OrganizationId = setup.OrganizationId,
        ContractType = setup.ContractType,

        // StartDay/EndDay/ExceptionDay are fixed-width char columns, so a value
        // shorter than the column comes back blank-padded. Trimming here means
        // a client can compare the value without having to know that.
        StartDay = setup.StartDay?.TrimEnd(),
        EndDay = setup.EndDay?.TrimEnd(),
        ExceptionDay = setup.ExceptionDay?.TrimEnd(),

        CountryId = setup.CountryId,
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
    /// never rewrite who created a row, resurrect a deleted one, or move a
    /// setup to a different id.
    /// </para>
    /// <para>
    /// Used for both the insert and the update path, which is what guarantees
    /// the two cannot drift apart and start accepting different fields.
    /// </para>
    /// </summary>
    internal static void ApplyTo(this AdminSetupSaveRequest request, TimesheetMasterSetup setup)
    {
        setup.UserId = request.UserId;
        setup.MaxTimeInHrs = request.MaxTimeInHrs;
        setup.MaxTimInMins = request.MaxTimInMins;
        setup.OrganizationId = request.OrganizationId;
        setup.ContractType = request.ContractType;
        setup.StartDay = NormaliseDayCode(request.StartDay);
        setup.EndDay = NormaliseDayCode(request.EndDay);
        setup.ExceptionDay = NormaliseDayCode(request.ExceptionDay);
        setup.CountryId = request.CountryId;
        setup.TimeEntryLockAt = request.TimeEntryLockAt;
    }

    /// <summary>
    /// Stores day codes in one canonical form - trimmed and upper-case - so that
    /// "mo", "MO " and "Mo" cannot end up as three different values in a
    /// case-insensitive column that will compare them as equal anyway.
    /// Whitespace-only becomes null rather than a row of blanks.
    /// </summary>
    private static string? NormaliseDayCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
