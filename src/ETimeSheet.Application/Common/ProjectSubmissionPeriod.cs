using ETimeSheet.Shared.Utilities;

namespace ETimeSheet.Application.Common;

/// <summary>
/// The submission periods of a project, from its <c>StartDate</c> and
/// <c>SubmissionFrequency</c> (<c>dbo.TimesheetProject</c>).
/// <para>
/// One definition, shared by everything that needs a period - the time log
/// save that stamps a <c>PSHT</c> code, the sheet submit, and the approver
/// reminder that fires once a period has ended - so no two of them can ever
/// disagree about where a period starts or ends.
/// </para>
/// </summary>
internal static class ProjectSubmissionPeriod
{
    /// <summary>The values <c>dbo.TimesheetProject.SubmissionFrequency</c> accepts.</summary>
    internal static readonly IReadOnlySet<int> Frequencies = new HashSet<int>
    {
        Constants.TimesheetProject.SubmissionFrequency.Weekly,
        Constants.TimesheetProject.SubmissionFrequency.FifteenDays,
        Constants.TimesheetProject.SubmissionFrequency.Monthly,
    };

    /// <summary>
    /// The submission period containing <paramref name="date"/>, counted
    /// from the project's start date: blocks of 7 days for weekly, 15 days for
    /// every 15 days, and calendar months from the start day for monthly - a
    /// project starting on the 10th has periods from the 10th to the 9th.
    /// Both dates are inclusive. <paramref name="date"/> is never before
    /// <paramref name="projectStartDate"/>.
    /// </summary>
    internal static (DateTime Start, DateTime End) Of(
        DateTime date,
        DateTime projectStartDate,
        int submissionFrequency)
    {
        if (submissionFrequency == Constants.TimesheetProject.SubmissionFrequency.Monthly)
        {
            // Every period is counted from the start date itself, never from
            // the previous period, so a start on the 31st does not drift to
            // the 28th after February.
            var months = (date.Year - projectStartDate.Year) * 12 + date.Month - projectStartDate.Month;

            if (projectStartDate.AddMonths(months) > date)
            {
                months--;
            }

            return (projectStartDate.AddMonths(months), projectStartDate.AddMonths(months + 1).AddDays(-1));
        }

        var days = submissionFrequency == Constants.TimesheetProject.SubmissionFrequency.Weekly ? 7 : 15;
        var periodStart = projectStartDate.AddDays((date - projectStartDate).Days / days * days);

        return (periodStart, periodStart.AddDays(days - 1));
    }
}
