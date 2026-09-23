using ETimeSheet.Application.Common;
using ETimeSheet.Application.Common.Mapping;
using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Exceptions;
using ETimeSheet.Shared.Utilities;
using Microsoft.Extensions.Logging;

namespace ETimeSheet.Application.Services.Implementations;

/// <summary>
/// Business logic for the Admin module.
/// <para>
/// It owns the four rules that matter here: that a user holds exactly one setup
/// and a save therefore updates, revives or inserts rather than duplicating;
/// what a soft delete actually means; that a deleted setup comes back rather
/// than being replaced; and who gets stamped into the audit columns. It reaches
/// the database only through <see cref="IAdminRepository"/> and
/// <see cref="ICountryRepository"/>, and never sees <c>Context</c>.
/// </para>
/// <para>
/// <b>It does not resolve the time zone.</b> <c>CountryId</c> and
/// <c>TimeZone</c> are stored exactly as the payload sends them - no lookup, no
/// cross-check against <c>dbo.Country</c>. Deciding whether the pair makes
/// sense is the caller's job; <c>get-country-list-with-timezones</c> is there to
/// build the choice from.
/// </para>
/// <para>
/// It <i>does</i> read <c>dbo.Country</c> on the way <b>out</b>, to name the
/// country a setup holds and build its <c>countryWithTimeZone</c> label. That is
/// presentation, not resolution: it runs after the write and cannot change a
/// stored value.
/// </para>
/// <para>
/// <b>No authorisation check.</b> Administrative CRUD is exactly the surface
/// that should be behind a permission, and it is not, because authentication is
/// switched off for this project for now. The acting user therefore comes from
/// the payload. Both of those change together when JWT is turned back on.
/// </para>
/// </summary>
public class AdminService : IAdminService
{
    private readonly IAdminRepository _adminRepository;
    private readonly ICountryRepository _countryRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IAdminRepository adminRepository,
        ICountryRepository countryRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<AdminService> logger)
    {
        _adminRepository = adminRepository;
        _countryRepository = countryRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<AdminResponse> SaveAsync(
        AdminSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // Read before anything is looked up or written: all three time fields
        // arrive as hh:mm:ss strings, and a malformed one should cost the caller
        // a 400 rather than a round trip to the database first. Parsed once here
        // and carried down, so the insert, the update and the revive branch all
        // write the same values.
        var times = ReadTimes(request);

        // The whole point of the shared endpoint: the client sends the same
        // payload every time and never has to know which operation it is
        // performing. The user - not a setup id - decides, because a user holds
        // exactly one setup. Three cases, in this order:
        //
        //   live row     -> update it
        //   deleted row  -> overwrite it and bring it back
        //   nothing      -> insert
        //
        // The middle case is why the lookup ignores query filters. If it did
        // not, a user whose setup was deleted would look like a new user and get
        // a second row, and the table would end up with two setups for them -
        // the exact thing this endpoint exists to prevent.
        var existing = await _adminRepository.FindForSaveByUserIdAsync(
            request.UserId,
            cancellationToken);

        return existing is null
            ? await AddNewAsync(request, times, cancellationToken)
            : await UpdateExistingAsync(existing, request, times, cancellationToken);
    }

    /// <summary>
    /// Reads the payload's three <c>hh:mm:ss</c> strings into the
    /// <see cref="TimeSpan"/> values the <c>time(7)</c> columns hold.
    /// <para>
    /// Here rather than in <c>AdminSaveRequestValidator</c> because
    /// <see cref="TimeOfDay"/> is the one place in the application that decides
    /// what a time of day is; a second opinion in a validator can drift from it.
    /// All three are optional, so an absent field stays null and only a field
    /// that was actually sent can fail.
    /// </para>
    /// </summary>
    /// <exception cref="ValidationException">
    /// A required time field is missing; a time field is not <c>hh:mm:ss</c>; or
    /// one of the two bounded fields is outside its range.
    /// </exception>
    private static TimesheetSetupTimes ReadTimes(AdminSaveRequest request)
    {
        // Parse, not ParseOptional: both became mandatory on 2026-09-22, and
        // Parse is what says so - it fails a missing value with a message keyed
        // on the field. That is the whole reason the validator says nothing
        // about these (CLAUDE.md §12).
        var maxTimeInHrs = TimeOfDay.Parse(
            request.MaxTimeInHrs,
            nameof(AdminSaveRequest.MaxTimeInHrs));

        var maxTimInMins = TimeOfDay.Parse(
            request.MaxTimInMins,
            nameof(AdminSaveRequest.MaxTimInMins));

        // The ranges are narrower than "a time of day", and they are checked
        // here because they cannot be stated without the parsed TimeSpan.
        RequireInRange(
            maxTimeInHrs,
            MaxTimeInHrsUpperBound,
            nameof(AdminSaveRequest.MaxTimeInHrs));

        // 00:59:00, not 00:59:59: this column carries the MINUTES half of a
        // daily maximum - the remainder that goes with MaxTimeInHrs - so an
        // hour component would be double-counting and a seconds component is
        // finer than anything that reads it. spc_GetEmployeeListByPOrgID takes
        // DATEPART(MINUTE, ...) of it and nothing else.
        RequireInRange(
            maxTimInMins,
            MaxTimInMinsUpperBound,
            nameof(AdminSaveRequest.MaxTimInMins));

        // Still optional, and still ParseOptional: absent is null and fine,
        // present-but-malformed is a 400.
        var timeEntryLockAt = TimeOfDay.ParseOptional(
            request.TimeEntryLockAt,
            nameof(AdminSaveRequest.TimeEntryLockAt));

        return new TimesheetSetupTimes(maxTimeInHrs, maxTimInMins, timeEntryLockAt);
    }

    /// <summary>
    /// Inclusive upper bound for <c>MaxTimeInHrs</c> - the daily maximum's hours
    /// half. 23:00:00, not 23:59:59: a whole number of hours is what this column
    /// means, and the minutes travel separately in <c>MaxTimInMins</c>.
    /// </summary>
    private static readonly TimeSpan MaxTimeInHrsUpperBound = new(23, 0, 0);

    /// <summary>
    /// Inclusive upper bound for <c>MaxTimInMins</c> - the minutes that go with
    /// <see cref="MaxTimeInHrsUpperBound"/>. 00:59:00, so it can never carry an
    /// hour of its own.
    /// </summary>
    private static readonly TimeSpan MaxTimInMinsUpperBound = new(0, 59, 0);

    /// <summary>
    /// Fails a parsed time that is outside <c>00:00:00</c>..<paramref name="upperBound"/>
    /// inclusive.
    /// <para>
    /// The lower bound is not a parameter because it is always midnight, and
    /// <see cref="TimeOfDay"/> has already refused anything negative - so this
    /// only has the top end left to check. The message quotes both ends in the
    /// same <c>hh:mm:ss</c> the caller sent, rather than describing them, so
    /// there is nothing to translate before fixing the payload.
    /// </para>
    /// </summary>
    private static void RequireInRange(TimeSpan value, TimeSpan upperBound, string field)
    {
        if (value > upperBound)
        {
            throw new ValidationException(
                field,
                $"{field} must be between \"{TimeSpan.Zero:hh\\:mm\\:ss}\" and " +
                $"\"{upperBound:hh\\:mm\\:ss}\".");
        }
    }

    public async Task<AdminResponse?> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var setup = await _adminRepository.GetByUserIdAsync(userId, cancellationToken);

        // Null rather than a 404: the controller answers 200 with
        // success: true and data: null. A read that found nothing has answered
        // the question, not failed to - and on an admin screen "this user has
        // not been set up yet" is the whole reason someone opened it. Telling
        // them success: false sends them looking for a mistake they did not
        // make.
        return setup is null
            ? null
            : await ToResponseAsync(setup, cancellationToken);
    }

    /// <summary>
    /// Projects a saved setup to its response, naming the country it holds.
    /// <para>
    /// The one extra read this module does, and it is <b>presentation only</b>:
    /// it supplies <c>CountryName</c> and the <c>CountryWithTimeZone</c> label so a
    /// screen can show the country and preselect its picker. It cannot affect
    /// what is stored - the save writes <c>CountryID</c> and <c>TimeZone</c>
    /// exactly as the payload sent them, and this runs afterwards, on the way
    /// out.
    /// </para>
    /// <para>
    /// Every path that returns an <c>AdminResponse</c> goes through here, so the
    /// two fields are populated the same way on the read and on the save. A
    /// field that carried a value from one endpoint and null from another would
    /// be a trap (CLAUDE.md §5).
    /// </para>
    /// <para>
    /// A country id the lookup has no row for leaves <c>CountryName</c> null
    /// rather than failing. That is now reachable - the save stores the id
    /// without checking it - and a read is not the place to start rejecting rows
    /// that are already stored.
    /// </para>
    /// </summary>
    private async Task<AdminResponse> ToResponseAsync(
        TimesheetMasterSetup setup,
        CancellationToken cancellationToken)
    {
        if (setup.CountryId is not { } countryId || countryId <= 0)
        {
            // No country on the row, so nothing to look up and nothing to name.
            return setup.ToResponse();
        }

        var country = await _countryRepository.GetByIdAsync(countryId, cancellationToken);

        return setup.ToResponse(country?.Name);
    }

    public async Task DeleteAsync(
        AdminDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var setup = await _adminRepository.GetForUpdateAsync(request.SetupId, cancellationToken)
            ?? throw NotFoundException.For("Timesheet setup", request.SetupId);

        // A soft delete, never a DELETE statement: the row stays and is hidden
        // by the entity's global query filter. The two stamps are the point of
        // doing it this way - without them the row is indistinguishable from one
        // that was deleted by accident years ago.
        setup.IsDelete = true;
        setup.DeleteDate = _dateTimeProvider.UtcNow;
        setup.DeletedBy = request.DeletedBy;

        await _adminRepository.UpdateAsync(setup, cancellationToken);

        _logger.LogInformation(
            "Timesheet setup {SetupId} soft-deleted by user {DeletedBy}.",
            setup.SetupId,
            request.DeletedBy);
    }

    public async Task<EmployeeListResponse> GetEmployeeListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        // Checked here rather than by a route constraint, so a caller who sends
        // 0 is told what is wrong with it. A constraint would simply not match
        // the route, and the deny-by-default fallback policy would answer 401 -
        // which says nothing true about the request.
        if (organizationId <= 0)
        {
            throw new ValidationException(
                "orgID",
                "orgID is required and must be greater than 0.");
        }

        var employees = await _adminRepository.GetEmployeeListByOrganizationIdAsync(
            organizationId,
            cancellationToken);

        // No authorisation check: authentication is switched off for this
        // project for now. This is a whole organisation's staff list, so it is
        // the first endpoint that should gain a permission when JWT is turned
        // back on - an employee has no business reading it.
        return new EmployeeListResponse
        {
            Summary = Summarise(employees),
            Employees = employees.ToResponses()
        };
    }

    public async Task<IReadOnlyList<CountryTimeZoneResponse>> GetCountryListWithTimeZonesAsync(
        CancellationToken cancellationToken = default)
    {
        // No argument, so nothing to validate and nothing that can be "not
        // found": the whole lookup is the answer. The previous shape of this
        // read took a country id and could therefore 400 and 404; asking for
        // every pairing at once removed both failures along with the round trip
        // per country.
        var countries = await _countryRepository.GetAllAsync(cancellationToken);

        // The flattening - one entry per zone, CountryId repeated across a
        // multi-zone country's entries - is in AdminMappings, and goes through
        // the same splitter the save resolves a chosen zone with. That is what
        // guarantees the picker cannot offer a pairing SaveAsync then refuses.
        //
        // Countries with no zone recorded fall out here rather than arriving as
        // empty labels: offering one would be offering a choice that the save
        // answers 400 to.
        return countries.ToCountryTimeZoneResponses();
    }

    /// <summary>
    /// Counts the head-count totals from the rows that are about to be
    /// returned - never with a second query.
    /// <para>
    /// That is what keeps the summary and the grid consistent: they are two
    /// views of one list, counted in one pass, so the totals cannot describe a
    /// different moment than the rows beneath them.
    /// </para>
    /// <para>
    /// <c>SetupID</c> is what decides "has a setup", rather than any of the
    /// expected-time columns: those are also null when a setup exists but has no
    /// working week or no daily maximum on it, and an employee who has been set
    /// up badly is a different problem from one who has not been set up at all.
    /// </para>
    /// </summary>
    private static EmployeeListSummaryResponse Summarise(
        IReadOnlyList<EmployeeListDetail> employees)
    {
        var withSetup = employees.Count(employee => employee.SetupId.HasValue);

        return new EmployeeListSummaryResponse
        {
            TotalEmployees = employees.Count,
            TotalEmployeesWithSetup = withSetup,

            // Subtracted rather than counted again, so the two can never fail to
            // add up to the head count however the rows are shaped.
            TotalEmployeesWithoutSetup = employees.Count - withSetup,

            // Counted on the id, not on the text beside it: the text is there to
            // be displayed and could be reworded in the procedure tomorrow,
            // while the id is persisted and cannot move.
            TotalFullTime = employees.Count(employee =>
                employee.ContractTypeId == Constants.TimesheetMasterSetup.ContractType.FullTime),

            TotalPartTime = employees.Count(employee =>
                employee.ContractTypeId == Constants.TimesheetMasterSetup.ContractType.PartTime)
        };
    }

    private async Task<AdminResponse> AddNewAsync(
        AdminSaveRequest request,
        TimesheetSetupTimes times,
        CancellationToken cancellationToken)
    {
        var setup = new TimesheetMasterSetup();
        request.ApplyTo(setup, times);

        // Written explicitly rather than by AuditableEntityInterceptor:
        // TimesheetMasterSetup does not derive from AuditableEntity, because
        // this table spells its soft-delete column IsDelete (a nullable bit)
        // where dbo.TimeLog spells it IsDeleted (a non-null int). The
        // interceptor therefore never sees this entity.
        setup.IsDelete = false;
        setup.CreateDate = _dateTimeProvider.UtcNow;
        setup.CreatedBy = request.CreatedBy;

        var saved = await _adminRepository.AddAsync(setup, cancellationToken);

        _logger.LogInformation(
            "Timesheet setup {SetupId} created for user {UserId} by user {CreatedBy}.",
            saved.SetupId,
            request.UserId,
            request.CreatedBy);

        return await ToResponseAsync(saved, cancellationToken);
    }

    /// <summary>
    /// Overwrites the user's existing setup with the payload, reviving it first
    /// if it had been deleted.
    /// </summary>
    private async Task<AdminResponse> UpdateExistingAsync(
        TimesheetMasterSetup setup,
        AdminSaveRequest request,
        TimesheetSetupTimes times,
        CancellationToken cancellationToken)
    {
        var wasDeleted = setup.IsDelete == true;

        request.ApplyTo(setup, times);

        if (wasDeleted)
        {
            // Brought back rather than left deleted-but-updated: the caller
            // asked for this user to have this setup, and a row that is still
            // flagged deleted would be invisible to every read in the API.
            //
            // The delete stamps are cleared with it. They record a delete that
            // has been undone, and leaving them on a live row would have the
            // next reader believe the setup is gone. CreatedBy and CreateDate
            // are untouched - this is still the row that was originally created,
            // and who created it has not changed.
            setup.IsDelete = false;
            setup.DeleteDate = null;
            setup.DeletedBy = null;
        }

        setup.UpdateDate = _dateTimeProvider.UtcNow;
        // request.CreatedBy, into UpdatedBy: the payload names whoever is saving,
        // and on this path that is the person changing the row, not the one who
        // first created it. The row's own CreatedBy is left as it was.
        setup.UpdatedBy = request.CreatedBy;

        await _adminRepository.UpdateAsync(setup, cancellationToken);

        // One constant template with the outcome as a value, rather than two
        // templates chosen at runtime: structured logging groups by template, and
        // a template that changes shape splits one event into two.
        _logger.LogInformation(
            "Timesheet setup {SetupId} for user {UserId} {SaveOutcome} by user {CreatedBy}.",
            setup.SetupId,
            request.UserId,
            wasDeleted ? "restored and updated" : "updated",
            request.CreatedBy);

        return await ToResponseAsync(setup, cancellationToken);
    }
}
