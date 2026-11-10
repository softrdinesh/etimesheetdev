using ETimeSheet.Shared.Utilities;

namespace ETimeSheet.Application.Common;

/// <summary>
/// Display names of <c>dbo.TimesheetProjectSubmission.StatusID</c>, shared by
/// the project sheet endpoints and the approver reminder email.
/// </summary>
internal static class ProjectSheetStatus
{
    internal static string NameOf(int statusId) => statusId switch
    {
        Constants.TimesheetProjectSubmission.Status.Pending => "Pending",
        Constants.TimesheetProjectSubmission.Status.Submitted => "Submitted",
        Constants.TimesheetProjectSubmission.Status.Approved => "Approved",
        Constants.TimesheetProjectSubmission.Status.Rejected => "Rejected",
        _ => "Unknown",
    };
}
