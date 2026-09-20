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
/// It owns the five rules that matter here: that a user holds exactly one setup
/// and a save therefore updates, revives or inserts rather than duplicating;
/// what a soft delete actually means; that a deleted setup comes back rather
/// than being replaced; who gets stamped into the audit columns; and that a
/// setup's time zone is resolved from its country rather than believed. It
/// reaches the database only through <see cref="IAdminRepository"/> and
/// <see cref="ICountryRepository"/>, and never sees <c>Context</c>.
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

        // Resolved before the save decides anything, for the same reason: the
        // country is what says which time zone this setup may hold, and a
        // payload that names one the country does not have should cost the
        // caller a 400 rather than a half-written row.
        var timeZone = await ResolveTimeZoneAsync(request, cancellationToken);

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
            ? await AddNewAsync(request, times, timeZone, cancellationToken)
            : await UpdateExistingAsync(existing, request, times, timeZone, cancellationToken);
    }

    /// <summary>
    /// Works out the one IANA time zone this setup should hold, from the country
    /// it names and - only when the country needs it - the payload's choice.
    /// <para>
    /// <b>The country decides, not the caller.</b> <c>dbo.Country.TimeZone</c>
    /// lists a country's zones, comma-separated, and the count of that list is
    /// the whole rule:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>One zone</b> - the United Kingdom, Germany, India. That zone is
    /// stored, and <c>request.TimeZone</c> is not consulted at all: there is
    /// exactly one answer the country can have, so asking the caller to repeat
    /// it only creates a way for them to get it wrong.
    /// </description></item>
    /// <item><description>
    /// <b>Several zones</b> - the United States, Australia, Canada, Brazil. The
    /// caller must choose, and must choose one of the country's own: a zone the
    /// country does not have is a 400, not a stored value. No default is picked
    /// for them, because "the first one listed" would silently put a New York
    /// employee's day on a Los Angeles clock.
    /// </description></item>
    /// </list>
    /// <para>
    /// Here and not in <c>AdminSaveRequestValidator</c> because the rule is a
    /// database question - which country, how many zones, which ones - and a
    /// validator may not read the database (CLAUDE.md §12).
    /// </para>
    /// </summary>
    /// <returns>
    /// The zone as <c>dbo.Country</c> spells it, never as the payload spelled
    /// it: matching is case-insensitive so a client is not punished for
    /// <c>"europe/london"</c>, but IANA ids are case-sensitive to every library
    /// that will later look one up, so what is stored is the canonical form.
    /// </returns>
    /// <exception cref="ValidationException">
    /// No country id; or the country has several zones and the payload named
    /// none of them.
    /// </exception>
    /// <exception cref="NotFoundException">No country has that id.</exception>
    /// <exception cref="BusinessException">
    /// The country exists but its <c>TimeZone</c> column is empty - a gap in the
    /// lookup rather than a fault in the payload, so it is not keyed on a field.
    /// </exception>
    private async Task<string> ResolveTimeZoneAsync(
        AdminSaveRequest request,
        CancellationToken cancellationToken)
    {
        // Checked here as well as in the validator: this method cannot be
        // correct without a country, and a service does not get to assume the
        // validator ran.
        if (request.CountryId is not { } countryId || countryId <= 0)
        {
            throw new ValidationException(
                nameof(AdminSaveRequest.CountryId),
                "CountryId is required: it is what determines the setup's time zone.");
        }

        var country = await _countryRepository.GetByIdAsync(countryId, cancellationToken)
            ?? throw NotFoundException.For("Country", countryId);

        var zones = CountryTimeZones.Split(country.TimeZone);

        if (zones.Count == 0)
        {
            // A BusinessException rather than a field-keyed ValidationException.
            // Both answer 400, but this failure is about no field the caller
            // sent: they named a real country, and nothing they can change in
            // their payload gets past it. The row in dbo.Country needs a time
            // zone. Keying it on "TimeZone" would send them looking for a
            // mistake in a field that is not theirs to fix.
            throw new BusinessException(
                $"Country '{countryId}' has no time zone configured.");
        }

        if (zones.Count == 1)
        {
            // Single-zone country: its zone wins outright, and anything the
            // payload sent is ignored rather than compared. See the summary.
            return zones[0];
        }

        var chosen = CountryTimeZones.Find(zones, request.TimeZone);

        if (chosen is not null)
        {
            return chosen;
        }

        // One message for both "you sent nothing" and "you sent something that
        // is not on the list", because the fix is identical and the list is what
        // the client actually needs to see. It is safe to spell out: these are
        // public IANA ids from a lookup table, not anything about this user.
        throw new ValidationException(
            nameof(AdminSaveRequest.TimeZone),
            $"Country '{countryId}' spans {zones.Count} time zones, so TimeZone is " +
            $"required and must be one of: {string.Join(", ", zones)}.");
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
    /// <exception cref="ValidationException">A time field was sent and is not <c>hh:mm:ss</c>.</exception>
    private static TimesheetSetupTimes ReadTimes(AdminSaveRequest request) => new(
        TimeOfDay.ParseOptional(request.MaxTimeInHrs, nameof(AdminSaveRequest.MaxTimeInHrs)),
        TimeOfDay.ParseOptional(request.MaxTimInMins, nameof(AdminSaveRequest.MaxTimInMins)),
        TimeOfDay.ParseOptional(request.TimeEntryLockAt, nameof(AdminSaveRequest.TimeEntryLockAt)));

    public async Task<AdminResponse> GetByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var setup = await _adminRepository.GetByUserIdAsync(userId, cancellationToken)
            ?? throw NotFoundException.For("Timesheet setup for user", userId);

        return setup.ToResponse();
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

    public async Task<IReadOnlyList<string>> GetCountryTimeZonesAsync(
        int countryId,
        CancellationToken cancellationToken = default)
    {
        // Checked here rather than by a route constraint, for the same reason as
        // the employee list: a caller who sends 0 is told what is wrong with it,
        // where a constraint would simply not match the route.
        if (countryId <= 0)
        {
            throw new ValidationException(
                "countryID",
                "countryID is required and must be greater than 0.");
        }

        // The row is fetched rather than just its TimeZone column, so that a
        // country that does not exist can be told apart from one that exists
        // with no zones recorded. The first is a 404; the second is an empty
        // list, which is a real answer.
        var country = await _countryRepository.GetByIdAsync(countryId, cancellationToken)
            ?? throw NotFoundException.For("Country", countryId);

        // The same splitter the save uses, deliberately: if the picker and the
        // save ever read that column differently, a client could be offered a
        // zone the save then rejects.
        return CountryTimeZones.Split(country.TimeZone);
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
        string timeZone,
        CancellationToken cancellationToken)
    {
        var setup = new TimesheetMasterSetup();
        request.ApplyTo(setup, times, timeZone);

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

        return saved.ToResponse();
    }

    /// <summary>
    /// Overwrites the user's existing setup with the payload, reviving it first
    /// if it had been deleted.
    /// </summary>
    private async Task<AdminResponse> UpdateExistingAsync(
        TimesheetMasterSetup setup,
        AdminSaveRequest request,
        TimesheetSetupTimes times,
        string timeZone,
        CancellationToken cancellationToken)
    {
        var wasDeleted = setup.IsDelete == true;

        request.ApplyTo(setup, times, timeZone);

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

        return setup.ToResponse();
    }
}
