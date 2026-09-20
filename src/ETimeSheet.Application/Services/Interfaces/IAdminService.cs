using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Services.Interfaces;

/// <summary>
/// Application contract for the Admin module - administrative CRUD over
/// <c>dbo.TimesheetMasterSetup</c>. Every public method of
/// <c>AdminService</c> is declared here; its private helpers are not.
/// <para>
/// <b>A user has exactly one setup.</b> That rule shapes this whole contract:
/// the read is by user id and returns a single setup, and the save takes no
/// setup id at all - there is only ever one row it could mean.
/// </para>
/// <para>
/// This is the only surface <c>AdminController</c> is allowed to touch.
/// </para>
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Saves a user's timesheet setup. <b>One method for insert and update</b>,
    /// and the caller does not choose between them: the user's live setup is
    /// updated if they have one, their deleted setup is overwritten and revived
    /// if they have one of those, and only a user with neither gets a new row.
    /// <para>
    /// It follows that this never fails for "already exists", and never fails
    /// to find the <b>setup</b> - every user is savable, and none of them can
    /// end up with two setups.
    /// </para>
    /// <para>
    /// <b>The time zone is resolved from the country</b> rather than taken from
    /// the payload. <c>CountryId</c> is required; the country's zone list is
    /// what decides. One zone and it is used outright, the payload's
    /// <c>TimeZone</c> being ignored; several and the payload must name one of
    /// them. A saved row can therefore never hold a zone its country does not
    /// have.
    /// </para>
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// No <c>CountryId</c>; or the country spans several time zones and
    /// <c>TimeZone</c> named none of them.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No country has that id. This is the one "not found" a save can produce,
    /// and it is about the country, never the setup.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.BusinessException">
    /// The country exists but has no time zone configured.
    /// </exception>
    Task<AdminResponse> SaveAsync(
        AdminSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the timesheet setup belonging to one user.</summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// The user has no setup - including the case where theirs was soft-deleted.
    /// </exception>
    Task<AdminResponse> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a timesheet setup: the row stays, marked deleted and stamped
    /// with who did it and when.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No live row has that id - deleting an already-deleted setup is a 404, not
    /// a silent success.
    /// </exception>
    Task DeleteAsync(
        AdminDeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every employee in one organisation, with the head-count totals
    /// for the same set of rows.
    /// <para>
    /// An organisation with no employees is an empty grid and a summary of
    /// zeroes, not a 404: "this organisation has nobody in it" is an answer, and
    /// an administrator looking at an empty list has learnt something. A 404
    /// would be reserved for an organisation that does not exist, and nothing
    /// here can tell the two apart - the procedure returns no rows either way.
    /// </para>
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// <paramref name="organizationId"/> is not a positive id.
    /// </exception>
    Task<EmployeeListResponse> GetEmployeeListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the time zones one country has - nothing else - unpacked from
    /// the comma-separated <c>dbo.Country.TimeZone</c> column.
    /// <para>
    /// Just the zones, because the caller already holds the country: it passed
    /// the id in to get here, and echoing the name and code back would be a
    /// second, unasked-for copy of a lookup row the client can read for itself.
    /// </para>
    /// <para>
    /// The setup screen's companion to <see cref="SaveAsync"/>: it answers
    /// "must I ask the user to choose a time zone, and from what?" before the
    /// save is attempted. One zone back means do not ask - the save will use it
    /// whatever the payload says. Several means the save requires
    /// <c>TimeZone</c> and requires it to be one of these. The two read the same
    /// column through the same splitter, so the picker cannot offer a value the
    /// save will refuse.
    /// </para>
    /// <para>
    /// A country whose <c>TimeZone</c> column is empty comes back with an empty
    /// list rather than an error. This is a read: reporting what is there is
    /// more useful than refusing to answer, and it is the save's job to stop an
    /// unusable country being stored against a setup.
    /// </para>
    /// </summary>
    /// <returns>
    /// The country's IANA zone ids, in the order the column lists them - so the
    /// first is its primary zone. Empty when the country has none recorded.
    /// </returns>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// <paramref name="countryId"/> is not a positive id.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// No country has that id.
    /// </exception>
    Task<IReadOnlyList<string>> GetCountryTimeZonesAsync(
        int countryId,
        CancellationToken cancellationToken = default);
}
