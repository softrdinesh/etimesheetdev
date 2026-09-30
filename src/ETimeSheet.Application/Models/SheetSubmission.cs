/*
    THE SHEETSUBMISSION MODULE'S MODEL - one file, every shape the module exchanges.

    SheetSubmissionController and SheetSubmissionService have exactly one model
    file between them, and this is it: the request payloads, the response
    payloads, and the rows the module's stored procedures return. Adding a shape
    to this module means adding a class HERE, not adding a file next to it.

    Not here, deliberately:
      - The EF entity for dbo.TimesheetSubmission lives in
        Models/Entities/TimesheetSubmission.cs - entities map to a TABLE, not to
        a controller.
      - Validators, mappings and services keep their own files; this is the
        model, not the module.

    ORDER MATTERS in the response types. System.Text.Json writes properties in
    declaration order, so moving a property up or down here changes the JSON a
    client receives. Reordering is a contract change, not a tidy-up.
*/

namespace ETimeSheet.Application.Models;

/// <summary>
/// Body of the submit-timesheet request - hands one user's sheet in for
/// approval.
/// <para>
/// The totals are <b>not</b> in the payload. The service adds them up from the
/// user's saved <c>dbo.TimeLog</c> entries on the sheet, so a client can never
/// submit a figure the entries do not support.
/// </para>
/// </summary>
public class SheetSubmissionRequest
{
    /// <summary>
    /// The sheet being submitted - the <c>SheetCode</c> the user's time log
    /// entries carry, e.g. <c>"TS-00121"</c>. Required, at most 15 characters.
    /// </summary>
    public string? SheetCode { get; set; }

    /// <summary>
    /// The user submitting the sheet, and whose entries are totalled. Written to
    /// the <c>SubmittedBy</c> column.
    /// <para>
    /// <b>Temporary</b>, like <c>AdminSaveRequest.CreatedBy</c>: it comes from
    /// the token once authentication is on.
    /// </para>
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// The user's organisation. <b>Accepted but not used yet</b> - neither
    /// <c>dbo.TimeLog</c> nor <c>dbo.TimesheetSubmission</c> has an organisation
    /// column, so nothing on this path reads it.
    /// </summary>
    public int OrgId { get; set; }
}

/// <summary>
/// Body of the review-timesheet request - an admin approving or rejecting a
/// submitted sheet.
/// </summary>
public class SheetSubmissionReviewRequest
{
    /// <summary>
    /// The admin reviewing the sheet. Written to <c>ApprovedBy</c> or
    /// <c>RejectedBy</c>, and to the audit log's <c>CreatedBy</c>.
    /// <para>
    /// <b>Temporary</b>, like <see cref="SheetSubmissionRequest.UserId"/>: it
    /// comes from the token once authentication is on, and is not checked
    /// against any role until then.
    /// </para>
    /// </summary>
    public int AdminId { get; set; }

    /// <summary>
    /// The submitted sheet to review - its <c>Timesheetcode</c>. Required, at
    /// most 15 characters.
    /// </summary>
    public string? SheetCode { get; set; }

    /// <summary>
    /// The outcome: <c>2</c> = Approved, <c>3</c> = Rejected. No other value is
    /// accepted.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// The admin's organisation. <b>Accepted but not used yet</b>, as on
    /// <see cref="SheetSubmissionRequest.OrgId"/>.
    /// </summary>
    public int OrgId { get; set; }
}

/// <summary>
/// Caller-facing view of one <c>dbo.TimesheetSubmission</c> row.
/// </summary>
public class SheetSubmissionResponse
{
    /// <summary>The submitted sheet - the <c>Timesheetcode</c> column.</summary>
    public string SheetCode { get; set; } = string.Empty;

    /// <summary>
    /// Total time on the sheet in hours, rounded to two decimal places -
    /// <c>37.5</c> for 37h 30m. The same figure as <see cref="TotalMins"/>,
    /// expressed in hours, not the hours part of it.
    /// </summary>
    public decimal TotalHours { get; set; }

    /// <summary>Total time on the sheet in minutes - <c>2250</c> for 37h 30m.</summary>
    public decimal TotalMins { get; set; }

    public int SubmittedBy { get; set; }

    /// <summary>When the sheet was submitted, in UTC.</summary>
    public DateTime SubmittedDate { get; set; }

    /// <summary>The submission's status - 1 = Submitted, 2 = Approved, 3 = Rejected.</summary>
    public int? StatusId { get; set; }

    public int? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public int? RejectedBy { get; set; }

    public DateTime? RejectedDate { get; set; }
}

/// <summary>
/// The user's current timesheet week: its dates, its sheet code, and the time
/// logged on it against the time their setup expects.
/// <para>
/// Every figure is in <b>hours</b>, rounded to two places - <c>37.5</c> for
/// 37h 30m - the same way <see cref="SheetSubmissionResponse.TotalHours"/> is.
/// </para>
/// </summary>
public class CurrentWeekSheetDetailResponse
{
    public int UserId { get; set; }

    /// <summary>
    /// The sheet code the user's entries this week carry, or <c>null</c> when
    /// they have logged nothing this week yet - the code is opened by the
    /// week's first entry.
    /// </summary>
    public string? SheetCode { get; set; }

    /// <summary>
    /// First day of the current week - the setup's <c>StartDay</c>, on or
    /// before today in the user's time zone. Monday when there is no usable
    /// setup.
    /// </summary>
    public DateTime WeekStartDate { get; set; }

    /// <summary>
    /// Last <b>working</b> day of the current week - the setup's <c>EndDay</c>
    /// after <see cref="WeekStartDate"/>, so Friday for a Monday-to-Friday
    /// setup. Six days after the start when there is no usable setup.
    /// </summary>
    public DateTime WeekEndDate { get; set; }

    /// <summary>
    /// The setup's <c>StartDay</c>, as stored - a <c>dbo.DayMaster.DayID</c>,
    /// 1 = Monday ... 7 = Sunday. <c>null</c> when the user has no setup or it
    /// has no start day.
    /// </summary>
    public int? StartDay { get; set; }

    /// <summary>
    /// <see cref="StartDay"/>'s name - <c>"Monday"</c>. <c>null</c> when
    /// <see cref="StartDay"/> is null or is not a valid day id.
    /// </summary>
    public string? StartDayName { get; set; }

    /// <summary>
    /// The setup's <c>EndDay</c>, as stored - 1 = Monday ... 7 = Sunday.
    /// <c>null</c> when the user has no setup or it has no end day.
    /// </summary>
    public int? EndDay { get; set; }

    /// <summary>
    /// <see cref="EndDay"/>'s name - <c>"Friday"</c>. <c>null</c> when
    /// <see cref="EndDay"/> is null or is not a valid day id.
    /// </summary>
    public string? EndDayName { get; set; }

    /// <summary>
    /// Hours logged on <see cref="SheetCode"/> - saved entries only, drafts
    /// and deleted entries excluded, exactly what submit-timesheet would
    /// record. <c>0</c> when there is no sheet code yet.
    /// </summary>
    public decimal TotalHoursWorked { get; set; }

    /// <summary>
    /// Hours the setup expects this week: working days (<c>StartDay</c> to
    /// <c>EndDay</c>) times the daily time (<c>MaxTimeinhrs</c> plus the
    /// minutes of <c>MaxTiminmins</c>). <c>null</c> when the user has no setup
    /// or it is missing any of those values.
    /// </summary>
    public decimal? TotalHoursExpected { get; set; }

    /// <summary>
    /// <see cref="TotalHoursWorked"/> minus <see cref="TotalHoursExpected"/>:
    /// positive when over, negative when short. <c>null</c> whenever the
    /// expected figure is.
    /// </summary>
    public decimal? TotalHoursDrift { get; set; }
}

/// <summary>
/// Body of the get-submitted-sheet-list request - the admin's view of the
/// sheets submitted in one organisation.
/// </summary>
public class SubmittedSheetListRequest
{
    /// <summary>
    /// The admin asking. <b>Not checked yet</b> - passed to
    /// <c>spc_GetSubmittedSheetList</c> as <c>@PAdminID</c>, where the admin
    /// check is still a placeholder.
    /// </summary>
    public int AdminId { get; set; }

    /// <summary>
    /// <c>0</c> for every user's submissions in the organisation; any other
    /// value for that user's only.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>The organisation whose submissions to list.</summary>
    public int OrgId { get; set; }
}

/// <summary>
/// One row of <c>dbo.spc_GetSubmittedSheetList</c> - a keyless result type,
/// shaped exactly like the procedure's SELECT list, never tracked and never
/// written.
/// </summary>
public class SubmittedSheetDetail
{
    public int UserId { get; set; }

    /// <summary>The person's name - <c>dbo.Signup.name</c>.</summary>
    public string? Name { get; set; }

    public string? Email { get; set; }

    public string SheetCode { get; set; } = string.Empty;

    public DateTime? WeekStartDate { get; set; }

    public DateTime? WeekEndDate { get; set; }

    public int? StartDay { get; set; }

    public string? StartDayName { get; set; }

    public int? EndDay { get; set; }

    public string? EndDayName { get; set; }

    public decimal TotalHoursWorked { get; set; }

    public decimal? TotalHoursExpected { get; set; }

    public decimal? TotalHoursDrift { get; set; }

    public int? Status { get; set; }

    public string? StatusText { get; set; }

    public DateTime SubmittedDate { get; set; }
}

/// <summary>
/// One submitted sheet in the admin's list: who submitted it, the same week and
/// hours details <see cref="CurrentWeekSheetDetailResponse"/> carries, and its
/// status.
/// <para>
/// Differences from the current-week view, all deliberate:
/// <see cref="TotalHoursWorked"/> is the total <b>submitted</b>, not a recount;
/// the week is dated from the sheet's own entries, so both week dates are
/// null when none is left; and expected and drift use the user's current
/// setup.
/// </para>
/// </summary>
public class SubmittedSheetResponse
{
    public int UserId { get; set; }

    /// <summary>The person's name, from <c>dbo.Signup</c>.</summary>
    public string? Name { get; set; }

    public string? Email { get; set; }

    public string SheetCode { get; set; } = string.Empty;

    /// <summary>
    /// The setup's <c>StartDay</c> on or before the sheet's earliest entry.
    /// Null when the sheet has no live entry left to date it by.
    /// </summary>
    public DateTime? WeekStartDate { get; set; }

    /// <summary>The setup's <c>EndDay</c> after <see cref="WeekStartDate"/>; null whenever it is.</summary>
    public DateTime? WeekEndDate { get; set; }

    /// <summary>The setup's <c>StartDay</c> - 1 = Monday ... 7 = Sunday.</summary>
    public int? StartDay { get; set; }

    /// <summary><see cref="StartDay"/>'s name, from <c>dbo.DayMaster</c>.</summary>
    public string? StartDayName { get; set; }

    /// <summary>The setup's <c>EndDay</c> - 1 = Monday ... 7 = Sunday.</summary>
    public int? EndDay { get; set; }

    /// <summary><see cref="EndDay"/>'s name, from <c>dbo.DayMaster</c>.</summary>
    public string? EndDayName { get; set; }

    /// <summary>The hours submitted - <c>dbo.TimesheetSubmission.TotalHours</c>.</summary>
    public decimal TotalHoursWorked { get; set; }

    /// <summary>Hours the user's current setup expects in a week; null without a complete setup.</summary>
    public decimal? TotalHoursExpected { get; set; }

    /// <summary>Worked minus expected; null whenever expected is.</summary>
    public decimal? TotalHoursDrift { get; set; }

    /// <summary>The submission's <c>StatusID</c> - 1 = Submitted, 2 = Approved, 3 = Rejected.</summary>
    public int? Status { get; set; }

    /// <summary><see cref="Status"/> as text; null for a status the procedure does not name yet.</summary>
    public string? StatusText { get; set; }

    /// <summary>When the sheet was submitted, in UTC.</summary>
    public DateTime SubmittedDate { get; set; }
}
