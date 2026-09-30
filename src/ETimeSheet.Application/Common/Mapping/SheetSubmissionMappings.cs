using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;

namespace ETimeSheet.Application.Common.Mapping;

/// <summary>
/// Hand-written projections between <see cref="TimesheetSubmission"/> and the
/// SheetSubmission DTOs.
/// <para>
/// Explicit and compile-time checked, for the same reason as
/// <see cref="TimeLogMappings"/>: a convention-based mapper would silently drop
/// a column the day one of these names changes.
/// </para>
/// </summary>
internal static class SheetSubmissionMappings
{
    internal static SheetSubmissionResponse ToResponse(this TimesheetSubmission submission) => new()
    {
        SheetCode = submission.Timesheetcode,
        TotalHours = submission.TotalHours,
        TotalMins = submission.TotalMins,
        SubmittedBy = submission.SubmittedBy,
        SubmittedDate = submission.SubmittedDate,
        StatusId = submission.StatusId,
        ApprovedBy = submission.ApprovedBy,
        ApprovedDate = submission.ApprovedDate,
        RejectedBy = submission.RejectedBy,
        RejectedDate = submission.RejectedDate
    };

    internal static SubmittedSheetResponse ToResponse(this SubmittedSheetDetail detail) => new()
    {
        UserId = detail.UserId,
        Name = detail.Name,
        Email = detail.Email,
        SheetCode = detail.SheetCode,
        WeekStartDate = detail.WeekStartDate,
        WeekEndDate = detail.WeekEndDate,
        StartDay = detail.StartDay,
        StartDayName = detail.StartDayName,
        EndDay = detail.EndDay,
        EndDayName = detail.EndDayName,
        TotalHoursWorked = detail.TotalHoursWorked,
        TotalHoursExpected = detail.TotalHoursExpected,
        TotalHoursDrift = detail.TotalHoursDrift,
        Status = detail.Status,
        StatusText = detail.StatusText,
        SubmittedDate = detail.SubmittedDate
    };
}
