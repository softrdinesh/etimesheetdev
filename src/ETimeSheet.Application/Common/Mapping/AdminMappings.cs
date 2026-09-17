using ETimeSheet.Application.DTOs.Admins;
using ETimeSheet.Application.Models.Entities;

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
    internal static void ApplyTo(this AdminSaveRequest request, TimesheetMasterSetup setup)
    {
        setup.UserId = request.UserId;
        setup.MaxTimeInHrs = request.MaxTimeInHrs;
        setup.MaxTimInMins = request.MaxTimInMins;
        setup.OrganizationId = request.OrganizationId;
        setup.ContractType = request.ContractType;
        setup.StartDay = request.StartDay;
        setup.EndDay = request.EndDay;
        setup.ExceptionDay = request.ExceptionDay;
        setup.CountryId = request.CountryId;
        setup.TimeEntryLockAt = request.TimeEntryLockAt;
    }
}
