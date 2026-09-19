/*
    THE ADMIN MODULE'S MODEL - one file, every shape the module exchanges.

    AdminController and AdminService have exactly one model file between
    them, and this is it: the request payloads, the response payloads, and the
    rows the module's stored procedures return. Adding a shape to this module
    means adding a class HERE, not adding a file next to it.

    A module may READ another module's model - TimeLogService reads the Admin
    entity, for instance - but it never gains a second model file of its own.

    Not here, deliberately:
      - EF entities (Models/Entities/) map one-to-one to a TABLE, not to a
        controller. dbo.DayMaster belongs to no controller at all, and
        dbo.TimesheetMasterSetup is read by two of them.
      - Validators, mappings and services keep their own files; this is the
        model, not the module.

    ORDER MATTERS in the response types. System.Text.Json writes properties in
    declaration order, so moving a property up or down here changes the JSON a
    client receives. Reordering is a contract change, not a tidy-up.
*/

namespace ETimeSheet.Application.Models;

/// <summary>
/// Body of the timesheet setup save request - <b>one payload for both insert and
/// update</b>.
/// <para>
/// There is deliberately no setup id here. A user holds exactly one setup, so
/// <see cref="UserId"/> already identifies the row: the service looks the user
/// up and updates their setup if they have one, revives and overwrites it if
/// theirs was deleted, and inserts only when they have neither. The client never
/// has to know which of the three happened, and cannot create a second row for a
/// user by sending the wrong id.
/// </para>
/// <para>
/// Every field except <see cref="UserId"/> and <see cref="CreatedBy"/> is
/// optional, mirroring the table: every column on
/// <c>dbo.TimesheetMasterSetup</c> other than the key is nullable.
/// </para>
/// </summary>
public class AdminSaveRequest
{
    /// <summary>The user these settings belong to. Required - it is what identifies the row to save.</summary>
    public int UserId { get; set; }

    /// <summary>
    /// Maximum time loggable, as a string in <c>hh:mm:ss</c> rather than a
    /// number: 8 hours is <c>"08:00:00"</c>. The column is <c>time(7)</c>, so
    /// the value must be inside a single day.
    /// <para>
    /// A string rather than a <c>TimeSpan</c>, as on every time input in this
    /// API: the contract is one exact format, and <c>AdminService</c> is what
    /// reads it. See <see cref="ETimeSheet.Shared.Utilities.TimeOfDay"/>.
    /// </para>
    /// </summary>
    public string? MaxTimeInHrs { get; set; }

    /// <summary>Companion to <see cref="MaxTimeInHrs"/>, also a <c>time(7)</c> and also <c>hh:mm:ss</c>.</summary>
    public string? MaxTimInMins { get; set; }

    public int? OrganizationId { get; set; }

    public int? ContractType { get; set; }

    /// <summary>
    /// First day of the timesheet week - a <c>dbo.DayMaster.DayID</c>: 1 =
    /// Monday, 2 = Tuesday ... 7 = Sunday. Send 1, not <c>"MO"</c>; the column
    /// stopped being a two-letter code on 2026-09-17.
    /// </summary>
    public int? StartDay { get; set; }

    /// <summary>Last day of the timesheet week. A day id, as <see cref="StartDay"/>. Send it with <see cref="StartDay"/> or not at all.</summary>
    public int? EndDay { get; set; }

    /// <summary>
    /// A day worked in addition to the normal week - also a
    /// <c>dbo.DayMaster.DayID</c>. Send 7 for Sunday, not <c>"SUN"</c>.
    /// </summary>
    public int? ExceptionDay { get; set; }

    public int? CountryId { get; set; }

    /// <summary>Time of day after which entry is locked, as <c>hh:mm:ss</c> - for example <c>"18:00:00"</c>.</summary>
    public string? TimeEntryLockAt { get; set; }

    // CanUserLoggedPreDayTime is deliberately absent: it is not a column on
    // dbo.TimesheetMasterSetup, it is derived by
    // spc_GetTimesheetMasterSetupByUserID. There is nothing here to store.

    /// <summary>
    /// The user performing the save.
    /// <para>
    /// Written to the row's <c>CreatedBy</c> when the save inserts, and to its
    /// <c>UpdatedBy</c> when the save updates or revives - the payload carries
    /// one "who is doing this", and which audit column it lands in follows from
    /// what the save turned out to be.
    /// </para>
    /// <para>
    /// <b>Temporary.</b> This belongs in the token, not in the payload - a
    /// caller can currently claim to be anyone. It moves to the authenticated
    /// principal the moment JWT is switched back on, and this property is then
    /// deleted from the contract.
    /// </para>
    /// </summary>
    public int CreatedBy { get; set; }
}

/// <summary>
/// Body of the timesheet setup delete request.
/// <para>
/// The delete is a <b>soft</b> delete: the row is never removed. It is marked
/// <c>IsDelete = 1</c> and stamped with who deleted it and when, so the history
/// survives and a global query filter simply stops returning it.
/// </para>
/// </summary>
public class AdminDeleteRequest
{
    /// <summary>The row to delete. Must identify a live, non-deleted row.</summary>
    public int SetupId { get; set; }

    /// <summary>
    /// The user performing the delete, written to the <c>Deletedby</c> column.
    /// <para>
    /// <b>Temporary</b>, exactly as <c>AdminSaveRequest.CreatedBy</c> is:
    /// it comes from the token once authentication is on.
    /// </para>
    /// </summary>
    public int DeletedBy { get; set; }
}

/// <summary>
/// Caller-facing view of one <c>dbo.TimesheetMasterSetup</c> row.
/// <para>
/// This is the administrative view and carries the whole row, audit columns
/// included, because an admin screen has to show who last changed a setup.
/// It is not the same shape as
/// <c>ETimeSheet.Application.DTOs.TimeLogs.TimesheetMasterSetupResponse</c>,
/// which is the seven-column projection that
/// <c>spc_GetTimesheetMasterSetupByUserID</c> returns to the timesheet screen.
/// Two audiences, two contracts - deliberately not shared.
/// </para>
/// </summary>
public class AdminResponse
{
    public int SetupId { get; init; }

    public int? UserId { get; init; }

    /// <summary>The <c>MaxTimeinhrs</c> column. Serialises as <c>"08:00:00"</c>, not as a number.</summary>
    public TimeSpan? MaxTimeInHrs { get; init; }

    /// <summary>The <c>MaxTiminmins</c> column.</summary>
    public TimeSpan? MaxTimInMins { get; init; }

    public int? OrganizationId { get; init; }

    public int? ContractType { get; init; }

    /// <summary>
    /// First day of the timesheet week - a <c>dbo.DayMaster.DayID</c>: 1 =
    /// Monday through 7 = Sunday. Look the name up in <c>dbo.DayMaster</c>, or
    /// use <c>Constants.DayMaster</c>.
    /// </summary>
    public int? StartDay { get; init; }

    /// <summary>Last day of the timesheet week. A day id, as <see cref="StartDay"/>.</summary>
    public int? EndDay { get; init; }

    /// <summary>A day worked in addition to the normal week. A day id, as <see cref="StartDay"/>.</summary>
    public int? ExceptionDay { get; init; }

    public int? CountryId { get; init; }

    public TimeSpan? TimeEntryLockAt { get; init; }

    // ---- audit ----

    public int? CreatedBy { get; init; }

    public DateTime? CreateDate { get; init; }

    public int? UpdatedBy { get; init; }

    public DateTime? UpdateDate { get; init; }
}

/// <summary>
/// One employee row of the organisation's employee list - the grid an
/// administrator sees.
/// <para>
/// A projection of <c>dbo.spc_GetEmployeeListByPOrgID</c>: every figure below is
/// computed by the procedure, not by the API, so a report running the procedure
/// directly and this endpoint can never disagree about an employee's hours.
/// </para>
/// </summary>
public class EmployeeResponse
{
    public int UserId { get; init; }

    /// <summary>
    /// The employee's timesheet setup id, or <see langword="null"/> when they
    /// have none. <b>This is the "is this employee set up?" flag</b> - and it is
    /// what the summary's with/without counts are built from. The expected-time
    /// fields below cannot serve that purpose: they are also null for a setup
    /// that exists but has no working week or no daily maximum on it.
    /// </summary>
    public int? SetupId { get; init; }

    public string? Name { get; init; }

    public string? Email { get; init; }

    /// <summary>Whole hours of contracted time per week. Null when there is no setup, or it is incomplete.</summary>
    public int? ExpectedHoursPerWeek { get; init; }

    /// <summary>
    /// The minutes that go with <see cref="ExpectedHoursPerWeek"/> - the
    /// remainder, not a separate quantity. 37.5 hours a week is 37 and 30.
    /// </summary>
    public int? ExpectedMinsPerWeek { get; init; }

    /// <summary>The same figure ready to display - <c>"37h 30m/week"</c>.</summary>
    public string? ExpectedHoursPerWeekText { get; init; }

    /// <summary>
    /// Whole hours logged in the current Monday-Sunday week. Zero rather than
    /// null when nothing has been logged - the answer is known, and it is none.
    /// </summary>
    public int TotalLoggedHoursCurrentWeek { get; init; }

    /// <summary>The minutes that go with <see cref="TotalLoggedHoursCurrentWeek"/>.</summary>
    public int TotalLoggedMinsCurrentWeek { get; init; }

    /// <summary>The same figure ready to display - <c>"12h 45m"</c>.</summary>
    public string? TotalLoggedHoursCurrentWeekText { get; init; }

    /// <summary>
    /// Percent of the contracted week logged so far, already capped at 100 by
    /// the procedure, so it can drive a progress bar without the client
    /// clamping it. Zero when there is no expected time to measure against.
    /// </summary>
    public decimal ProgressOnThisWeek { get; init; }

    /// <summary>1 = full time, 2 = part time. Null when there is no setup, or it names no contract.</summary>
    public int? ContractTypeId { get; init; }

    /// <summary>The contract spelled out - <c>"Full Time"</c> or <c>"Part Time"</c>.</summary>
    public string? ContractType { get; init; }
}

/// <summary>
/// Head-count totals for the employee list.
/// <para>
/// <b>Every number here is counted from the rows in the same response</b>, not
/// queried separately. That is deliberate: a second query could be answered from
/// a slightly different moment, and a summary that disagrees with the grid under
/// it is worse than no summary at all. Filter the grid and the totals follow.
/// </para>
/// </summary>
public class EmployeeListSummaryResponse
{
    /// <summary>Employees returned - the row count of the grid.</summary>
    public int TotalEmployees { get; init; }

    /// <summary>How many of them have a timesheet setup.</summary>
    public int TotalEmployeesWithSetup { get; init; }

    /// <summary>
    /// How many have none. <see cref="TotalEmployeesWithSetup"/> and this always
    /// add up to <see cref="TotalEmployees"/>: an employee either has a setup or
    /// does not.
    /// </summary>
    public int TotalEmployeesWithoutSetup { get; init; }

    /// <summary>How many are on a full-time contract.</summary>
    public int TotalFullTime { get; init; }

    /// <summary>
    /// How many are on a part-time contract.
    /// <para>
    /// <b>Full time and part time need not add up to the head count.</b> An
    /// employee with no setup has no contract, and a setup can carry a contract
    /// id that is neither 1 nor 2. Those are counted in neither, which is the
    /// honest answer - inventing a default would report a contract nobody chose.
    /// </para>
    /// </summary>
    public int TotalPartTime { get; init; }
}

/// <summary>
/// What the employee list endpoint returns: the head-count totals, then the
/// employees they were counted from.
/// <para>
/// <b>Property order matters here.</b> System.Text.Json writes properties in
/// declaration order, so <see cref="Summary"/> is declared first to put the
/// totals at the top of the payload - where they can be read without scrolling
/// past the whole grid. The same shape as
/// <c>TimeLoggedDetailsForTaskResponse</c>, for the same reason.
/// </para>
/// </summary>
public class EmployeeListResponse
{
    public EmployeeListSummaryResponse Summary { get; init; } = new();

    public IReadOnlyCollection<EmployeeResponse> Employees { get; init; } =
        Array.Empty<EmployeeResponse>();
}

/// <summary>
/// One row of the result set returned by
/// <c>dbo.spc_GetEmployeeListByPOrgID</c> - one employee in an organisation,
/// with their contracted weekly time, what they have logged so far this week,
/// and how far through the week that puts them.
/// <para>
/// This is a keyless type: it is not a table, it has no identity and it is never
/// tracked or written. It exists solely to give the procedure's SELECT list a
/// shape EF Core can materialise, which is why it carries exactly the thirteen
/// columns the procedure returns - no more.
/// </para>
/// <para>
/// <b>The procedure joins <c>dbo.Signup</c> to <c>dbo.TimesheetMasterSetup</c>
/// with a LEFT JOIN</b>, so an employee who has never been given a setup still
/// appears - with <see cref="SetupId"/> null and every expected-time column
/// null. That is the whole point of the row being here: an employee with no
/// setup is exactly what an administrator is looking for.
/// </para>
/// </summary>
public class EmployeeListDetail
{
    /// <summary>The employee's <c>dbo.Signup.UserID</c>.</summary>
    public int UserId { get; set; }

    /// <summary>
    /// Their <c>dbo.TimesheetMasterSetup.SetupID</c>, or <see langword="null"/>
    /// when they have no setup at all. This is the only reliable "have they been
    /// set up?" signal in the row - the expected-time columns are also null when
    /// a setup exists but is half-filled.
    /// </summary>
    public int? SetupId { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    /// <summary>
    /// Whole hours of contracted time per week, computed by the procedure from
    /// the setup's working week and daily maximum. Null when the setup is
    /// missing or incomplete.
    /// </summary>
    public int? ExpectedHoursPerWeek { get; set; }

    /// <summary>
    /// The leftover minutes that go with <see cref="ExpectedHoursPerWeek"/> -
    /// the remainder of the same division, not a separate quantity. 37.5 hours a
    /// week is 37 and 30, never 37.5.
    /// </summary>
    public int? ExpectedMinsPerWeek { get; set; }

    /// <summary>
    /// The same figure already formatted by the procedure - <c>"37h 30m/week"</c>.
    /// Passed through rather than rebuilt, so the API and any report running the
    /// procedure directly cannot phrase it differently.
    /// </summary>
    public string? ExpectedHoursPerWeekText { get; set; }

    /// <summary>
    /// Whole hours logged in the current Monday-Sunday week. <b>Not nullable</b>:
    /// the procedure coalesces a user with no entries to zero, which is a
    /// genuine "nothing logged" rather than "unknown".
    /// </summary>
    public int TotalLoggedHoursCurrentWeek { get; set; }

    /// <summary>The leftover minutes that go with <see cref="TotalLoggedHoursCurrentWeek"/>.</summary>
    public int TotalLoggedMinsCurrentWeek { get; set; }

    /// <summary>The same figure formatted by the procedure - <c>"12h 45m"</c>.</summary>
    public string? TotalLoggedHoursCurrentWeekText { get; set; }

    /// <summary>
    /// Percent of the contracted week logged so far, <b>clamped to 100 by the
    /// procedure</b> so a bar can be drawn straight from it. Zero when there is
    /// no expected time to measure against.
    /// <para>
    /// A <c>decimal</c> because the procedure casts to <c>DECIMAL(10, 0)</c>; the
    /// scale of zero means the values that come back are whole numbers.
    /// </para>
    /// </summary>
    public decimal ProgressOnThisWeek { get; set; }

    /// <summary>
    /// The raw <c>ContractType</c> column: 1 = full time, 2 = part time. Null
    /// when the employee has no setup, or has one that does not name a contract.
    /// </summary>
    public int? ContractTypeId { get; set; }

    /// <summary>
    /// The contract spelled out by the procedure - <c>"Full Time"</c> or
    /// <c>"Part Time"</c>. Null for any other id, including when there is no
    /// setup.
    /// </summary>
    public string? ContractType { get; set; }
}

/// <summary>
/// The three <c>time(7)</c> columns of <c>dbo.TimesheetMasterSetup</c>, parsed
/// out of the <c>hh:mm:ss</c> strings the save payload carries.
/// <para>
/// One value carried between <c>AdminService</c> and <c>AdminMappings</c>
/// instead of three loose <c>TimeSpan?</c> parameters, which at a call site are
/// three arguments of the same type in a row and can be transposed without the
/// compiler noticing. It exists so the payload is read exactly once per save:
/// the three fields are parsed before the service decides whether it is
/// inserting, updating or reviving, and the same parsed value is what every
/// branch writes.
/// </para>
/// </summary>
internal readonly record struct TimesheetSetupTimes(
    TimeSpan? MaxTimeInHrs,
    TimeSpan? MaxTimInMins,
    TimeSpan? TimeEntryLockAt);
