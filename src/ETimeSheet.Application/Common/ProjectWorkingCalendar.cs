using ETimeSheet.Application.Models.Entities;

namespace ETimeSheet.Application.Common;

/// <summary>
/// Which days a project is actually worked on: every day except its weekend
/// days (<c>dbo.TimeSheet_Weekend_setup</c>) and its holidays
/// (<c>dbo.TimesheetHolidaysetup</c>).
/// <para>
/// One definition, shared by everything that has to know whether a day counts -
/// the time log save, the submission period's two ends, and the approver
/// reminder - so no two of them can disagree about where a period stops.
/// </para>
/// <para>
/// <b>A holiday may be narrowed to a country, so a calendar is built for one
/// user at a time</b> on the paths that are about a user. The approver
/// reminder, which is about a project rather than a person, builds its calendar
/// with no country and so sees only the holidays that apply to everyone.
/// </para>
/// </summary>
internal sealed class ProjectWorkingCalendar
{
    private readonly IReadOnlySet<DayOfWeek> _weekendDays;

    /// <summary>
    /// Holiday dates to the description the setup gives them, so a refusal can
    /// name the holiday rather than just the date. The value is null for a
    /// holiday with no description.
    /// </summary>
    private readonly IReadOnlyDictionary<DateTime, string?> _holidays;

    private ProjectWorkingCalendar(
        IReadOnlySet<DayOfWeek> weekendDays,
        IReadOnlyDictionary<DateTime, string?> holidays)
    {
        _weekendDays = weekendDays;
        _holidays = holidays;
    }

    /// <summary>
    /// A calendar with no weekend and no holiday - every day is a working day.
    /// <para>
    /// This is what a project with no row in <c>dbo.TimeSheet_Weekend_setup</c>
    /// gets: no configuration means <b>no weekend</b>, deliberately, rather than
    /// an assumed Saturday and Sunday.
    /// </para>
    /// </summary>
    internal static ProjectWorkingCalendar EveryDay { get; } = new(
        new HashSet<DayOfWeek>(),
        new Dictionary<DateTime, string?>());

    /// <summary>True when the day is a weekend day of this project.</summary>
    internal bool IsWeekend(DateTime date) => _weekendDays.Contains(date.DayOfWeek);

    /// <summary>True when the day is a holiday covering this project.</summary>
    internal bool IsHoliday(DateTime date) => _holidays.ContainsKey(date.Date);

    /// <summary>True when the day is neither a weekend day nor a holiday.</summary>
    internal bool IsWorkingDay(DateTime date) => !IsWeekend(date) && !IsHoliday(date);

    /// <summary>
    /// How to refer to the holiday on this day in a message - "a holiday
    /// (Diwali)", or just "a holiday" when the setup gives no description.
    /// Returns null when the day is not a holiday at all.
    /// </summary>
    internal string? DescribeHoliday(DateTime date) =>
        _holidays.TryGetValue(date.Date, out var description)
            ? string.IsNullOrWhiteSpace(description) ? "a holiday" : $"a holiday ({description.Trim()})"
            : null;

    /// <summary>
    /// The calendar of one project, picked out of the organisation's weekend
    /// setups and holidays.
    /// <para>
    /// <b>Weekend precedence is most-specific-first:</b> a setup naming this
    /// project wins; failing that one naming its client; failing that the
    /// organisation's own. A project with none at all has no weekend.
    /// </para>
    /// <para>
    /// A holiday covers the day when it belongs to this organisation and each
    /// of its country, client and project is either empty - meaning "everyone" -
    /// or the one given. That is the same test the single-date lookup used
    /// before, kept identical so the two can never disagree.
    /// </para>
    /// </summary>
    /// <param name="countryId">
    /// The user's country, or null on a project-level calendar, which then sees
    /// only the holidays that name no country.
    /// </param>
    internal static ProjectWorkingCalendar For(
        int organizationId,
        int projectId,
        int clientId,
        int? countryId,
        IReadOnlyCollection<TimesheetWeekendSetup> weekendSetups,
        IReadOnlyCollection<TimesheetHolidaySetup> holidays)
    {
        var setup = MostSpecificWeekendSetup(organizationId, projectId, clientId, weekendSetups);

        var weekendDays = new HashSet<DayOfWeek>();

        if (setup is not null)
        {
            // A null flag is not a weekend day: the column is nullable, so a
            // day may be neither set nor cleared, and only an explicit 1 means
            // "this day is a weekend".
            AddWhenWeekend(weekendDays, DayOfWeek.Monday, setup.IsMondayWeekend);
            AddWhenWeekend(weekendDays, DayOfWeek.Tuesday, setup.IsTuesdayWeekend);
            AddWhenWeekend(weekendDays, DayOfWeek.Wednesday, setup.IsWednesdayWeekend);
            AddWhenWeekend(weekendDays, DayOfWeek.Thursday, setup.IsThursdayWeekend);
            AddWhenWeekend(weekendDays, DayOfWeek.Friday, setup.IsFridayWeekend);
            AddWhenWeekend(weekendDays, DayOfWeek.Saturday, setup.IsSaturdayWeekend);
            AddWhenWeekend(weekendDays, DayOfWeek.Sunday, setup.IsSundayWeekend);
        }

        var holidayDates = new Dictionary<DateTime, string?>();

        foreach (var holiday in holidays)
        {
            if (holiday.HolidayDate is not { } holidayDate
                || holiday.OrganizationId != organizationId
                || (holiday.ProjectId is not null && holiday.ProjectId != projectId)
                || (holiday.ClientId is not null && holiday.ClientId != clientId)
                || (holiday.CountryId is not null && holiday.CountryId != countryId))
            {
                continue;
            }

            // Two holidays on one day: the first wins, as the single-date
            // lookup's ORDER BY TimesheetHolidaySetupID did.
            if (!holidayDates.ContainsKey(holidayDate.Date))
            {
                holidayDates[holidayDate.Date] = holiday.Description;
            }
        }

        return weekendDays.Count == 0 && holidayDates.Count == 0
            ? EveryDay
            : new ProjectWorkingCalendar(weekendDays, holidayDates);
    }

    private static void AddWhenWeekend(ISet<DayOfWeek> weekendDays, DayOfWeek day, bool? isWeekend)
    {
        if (isWeekend == true)
        {
            weekendDays.Add(day);
        }
    }

    /// <summary>
    /// The weekend setup that applies: the project's, else its client's, else
    /// the organisation's. The lowest id wins within each step, should the
    /// table hold more than one - the API allows only one live row per project,
    /// but the table does not enforce it.
    /// </summary>
    private static TimesheetWeekendSetup? MostSpecificWeekendSetup(
        int organizationId,
        int projectId,
        int clientId,
        IReadOnlyCollection<TimesheetWeekendSetup> weekendSetups)
    {
        var ofOrganization = weekendSetups
            .Where(setup => setup.OrganizationId == organizationId)
            .OrderBy(setup => setup.TimeSheetWeekendSetupId)
            .ToList();

        return ofOrganization.FirstOrDefault(setup => setup.ProjectId == projectId)
            ?? ofOrganization.FirstOrDefault(setup => setup.ProjectId is null && setup.ClientId == clientId)
            ?? ofOrganization.FirstOrDefault(setup => setup.ProjectId is null && setup.ClientId is null);
    }
}
