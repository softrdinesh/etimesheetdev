using ETimeSheet.Shared.Utilities;

namespace ETimeSheet.Application.Common;

/// <summary>
/// The submission periods of a project: <b>calendar</b> weeks and months,
/// clipped to the project's own life and trimmed to the days it is worked on.
/// <para>
/// One definition, shared by everything that needs a period - the time log save
/// that stamps a <c>PSHT</c> code, the sheet submit, and the approver reminder
/// that fires once a period has ended - so no two of them can ever disagree
/// about where a period starts or ends.
/// </para>
/// <para>
/// <b>Periods follow the calendar, not the project's start date.</b> A weekly
/// project runs Monday to Sunday whatever day it started on; a monthly one runs
/// the 1st to the last of each month. The project's start and end dates only
/// clip the first and last period: a project starting on Wednesday 11 November
/// has a first week of 11-15 November, and the second runs 16-22 as usual.
/// </para>
/// <para>
/// <b>Both ends are then trimmed to working days</b> - see
/// <see cref="ProjectWorkingCalendar"/>. With Saturday and Sunday configured as
/// weekend, that same first week is 11-13 November and is submitted from the
/// 13th; the next is 16-20. A period whose every day is a weekend day or a
/// holiday has no working day at all, and is not a period.
/// </para>
/// </summary>
internal static class ProjectSubmissionPeriod
{
    /// <summary>
    /// The values of <c>dbo.TimesheetProject.SubmissionFrequency</c> this
    /// application supports.
    /// <para>
    /// <b>Every 15 days (2) is deliberately absent.</b> The column's CHECK
    /// constraint still allows it, because the database is not ours to change,
    /// but there is no calendar block that "15 days" names - it is the one
    /// frequency the calendar cannot express - so a project set to it is
    /// refused at the point where a period would be needed, with a message
    /// saying so.
    /// </para>
    /// </summary>
    internal static readonly IReadOnlySet<int> Frequencies = new HashSet<int>
    {
        Constants.TimesheetProject.SubmissionFrequency.Weekly,
        Constants.TimesheetProject.SubmissionFrequency.Monthly,
    };

    /// <summary>
    /// The calendar block containing <paramref name="date"/>: its Monday-to-Sunday
    /// week, or its first-to-last month. Neither clipped to the project nor
    /// trimmed to working days - that is <see cref="Of"/>'s job.
    /// </summary>
    internal static (DateTime Start, DateTime End) BlockOf(DateTime date, int submissionFrequency)
    {
        if (submissionFrequency == Constants.TimesheetProject.SubmissionFrequency.Monthly)
        {
            var firstOfMonth = new DateTime(date.Year, date.Month, 1);

            return (firstOfMonth, firstOfMonth.AddMonths(1).AddDays(-1));
        }

        // DayOfWeek numbers Sunday 0 to Saturday 6, so + 6 % 7 turns it into
        // "days since Monday" - 0 for Monday, 6 for Sunday.
        var monday = date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

        return (monday, monday.AddDays(6));
    }

    /// <summary>
    /// The calendar block containing <paramref name="date"/>, clipped to the
    /// project's life but <b>not</b> trimmed to working days - every day the
    /// block covers that the project was running on.
    /// <para>
    /// This is the span a sheet's <i>contents</i> are gathered over: its total,
    /// and the code its entries share. The trimmed period
    /// (<see cref="Of"/>) says which days may be logged and when the sheet
    /// becomes submittable, but an entry written before a day became a weekend
    /// day or a holiday still belongs to that week's sheet, and summing only
    /// the trimmed days would quietly leave it off every total.
    /// </para>
    /// </summary>
    internal static (DateTime Start, DateTime End) ClippedBlockOf(
        DateTime date,
        DateTime projectStartDate,
        DateTime? projectEndDate,
        int submissionFrequency)
    {
        var (blockStart, blockEnd) = BlockOf(date, submissionFrequency);

        return (
            blockStart < projectStartDate.Date ? projectStartDate.Date : blockStart,
            projectEndDate is { } projectEnd && blockEnd > projectEnd.Date ? projectEnd.Date : blockEnd);
    }

    /// <summary>The start of the calendar block after the one starting on <paramref name="blockStart"/>.</summary>
    internal static DateTime NextBlockStart(DateTime blockStart, int submissionFrequency) =>
        submissionFrequency == Constants.TimesheetProject.SubmissionFrequency.Monthly
            ? blockStart.AddMonths(1)
            : blockStart.AddDays(7);

    /// <summary>
    /// The submission period containing <paramref name="date"/>: its calendar
    /// block, clipped to the project's start and end dates, then trimmed inwards
    /// until both ends are working days. Both dates are inclusive.
    /// <para>
    /// <see langword="null"/> when nothing is left - the block lies outside the
    /// project's life, or every day of it is a weekend day or a holiday. There
    /// is then no sheet for that block, and no time can be logged in it either.
    /// </para>
    /// </summary>
    internal static (DateTime Start, DateTime End)? Of(
        DateTime date,
        DateTime projectStartDate,
        DateTime? projectEndDate,
        int submissionFrequency,
        ProjectWorkingCalendar calendar)
    {
        var (blockStart, blockEnd) = BlockOf(date, submissionFrequency);

        // The project's own dates clip the first and last period, so a project
        // starting mid-week has a short first week and one ending mid-month a
        // short last month.
        var start = blockStart < projectStartDate.Date ? projectStartDate.Date : blockStart;
        var end = projectEndDate is { } projectEnd && blockEnd > projectEnd.Date ? projectEnd.Date : blockEnd;

        // Trimmed inwards from both ends, never outwards: a period never covers
        // a day the project is not worked on, so its last day is always the day
        // the sheet becomes submittable.
        while (start <= end && !calendar.IsWorkingDay(start))
        {
            start = start.AddDays(1);
        }

        while (end >= start && !calendar.IsWorkingDay(end))
        {
            end = end.AddDays(-1);
        }

        return start > end ? null : (start, end);
    }
}
