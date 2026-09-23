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
    /// <b><c>CountryId</c> and <c>TimeZone</c> are stored exactly as sent.</b>
    /// Neither is looked up, derived or cross-checked: no read of
    /// <c>dbo.Country</c> happens on this path at all, and the pair the caller
    /// sends is the pair the row ends up with.
    /// </para>
    /// <para>
    /// An earlier version resolved <c>TimeZone</c> from the country - ignoring
    /// the payload's value for a single-zone country and rejecting one that was
    /// not on a multi-zone country's list. That is gone. <b>Nothing now stops a
    /// setup holding a zone its country does not have</b>, so the caller owns
    /// that consistency; <see cref="GetCountryListWithTimeZonesAsync"/> exists
    /// to build the choice from.
    /// </para>
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ValidationException">
    /// The payload is malformed - a missing <c>CountryId</c>, or a time field
    /// that is not <c>hh:mm:ss</c>. Shape only; no rule here reads the database.
    /// </exception>
    Task<AdminResponse> SaveAsync(
        AdminSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the timesheet setup belonging to one user, or
    /// <see langword="null"/> when they have none - including the case where
    /// theirs was soft-deleted.
    /// <para>
    /// <b>Null is an answer, not a failure.</b> The question "what is this
    /// user's setup?" has been answered truthfully: there isn't one. It reaches
    /// the client as a 200 with <c>success: true</c> and <c>data: null</c>,
    /// because nothing went wrong - the request was well formed, it ran, and
    /// the database has no row. Reserve <c>success: false</c> for a caller who
    /// has something to fix.
    /// </para>
    /// <para>
    /// Not the null-as-failure CLAUDE.md §6 forbids: that rule is about
    /// signalling an <b>error</b> by returning null rather than throwing, and
    /// every error this method can hit still throws. Here null carries exactly
    /// one meaning, and it is a fact about the data.
    /// </para>
    /// <para>
    /// It pairs with <see cref="SaveAsync"/>, which needs no distinction
    /// between insert and update: a client can read this, get null, and send
    /// the same save payload it would have sent anyway.
    /// </para>
    /// </summary>
    Task<AdminResponse?> GetByUserIdAsync(
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
    /// Returns every country/time-zone pairing there is - <b>one entry per time
    /// zone</b>, not one per country - for the setup screen's country picker.
    /// <para>
    /// <c>dbo.Country.TimeZone</c> packs a country's IANA zones into one
    /// comma-separated column, and this unpacks the whole table: the United
    /// Kingdom contributes one entry, the United States twenty-nine, every one
    /// of them carrying the same <c>CountryId</c>. So <c>CountryId</c> repeats,
    /// deliberately, and the caller binds a flat list to a dropdown without
    /// having to expand a nested shape or ask a second question.
    /// </para>
    /// <para>
    /// Each entry carries the country id, the country name and the zone
    /// separately, plus the two joined with a hyphen as <c>OptionValue</c> for
    /// display. Both, deliberately: the label is what a dropdown shows and keys
    /// a selection on, while the separate fields are what
    /// <see cref="SaveAsync"/> wants back - so a client never has to split a
    /// label to build its next request.
    /// </para>
    /// <para>
    /// It replaced a per-country read that took an id and answered with bare
    /// zone strings. That read could only be made <i>after</i> a country had
    /// been chosen, which is backwards: the client needs the list to build the
    /// choice with. One call now, at screen load, instead of one per country
    /// the user clicks.
    /// </para>
    /// <para>
    /// <see cref="SaveAsync"/>'s companion, and the two read the same column
    /// through the same splitter - so the picker cannot offer a pairing the
    /// save will refuse. <b>A country with no zone recorded contributes
    /// nothing</b>, because the save answers 400 for one and a picker should
    /// hold only answers that work.
    /// </para>
    /// <para>
    /// No paging and no filter: <c>dbo.Country</c> is a bounded reference list,
    /// and a picker wants all of it at once.
    /// </para>
    /// </summary>
    /// <returns>
    /// The pairings, countries ordered by name and each country's zones in the
    /// order its column lists them - so a country's primary zone comes first.
    /// Empty only if the lookup itself holds no zones at all.
    /// </returns>
    Task<IReadOnlyList<CountryTimeZoneResponse>> GetCountryListWithTimeZonesAsync(
        CancellationToken cancellationToken = default);
}
