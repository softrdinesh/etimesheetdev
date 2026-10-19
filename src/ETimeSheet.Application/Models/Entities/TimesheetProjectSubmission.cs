namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One submitted project sheet, mapped onto
/// <c>dbo.TimesheetProjectSubmission</c>. This is a persistence/domain model
/// and is never returned from a controller.
/// <para>
/// Mapped ahead of the submission endpoints because the project time log
/// needs it: an entry may not be edited or removed once its sheet has been
/// submitted, and this is what that is read from.
/// </para>
/// </summary>
public class TimesheetProjectSubmission
{
    /// <summary>The <c>TimesheetProjectSubmissionID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetProjectSubmissionId { get; set; }

    /// <summary>
    /// The sheet. Its entries are the <c>dbo.TimesheetProjectTimeLog</c> rows
    /// carrying the same code - neither table holds the other's key.
    /// <para>
    /// <b>Not unique</b>: no index enforces it, so two rows can carry one code.
    /// </para>
    /// </summary>
    public string SheetSubmissionCode { get; set; } = string.Empty;

    public decimal TotalHours { get; set; }

    public decimal TotalMinutes { get; set; }

    /// <summary>
    /// <c>1</c> = Submitted, <c>2</c> = Approved, <c>3</c> = Rejected, as
    /// <c>Constants.TimesheetSubmission.Status</c>.
    /// </summary>
    public int StatusId { get; set; }

    public int SubmittedBy { get; set; }

    public DateTime SubmittedDate { get; set; }

    public int? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public int? RejectedBy { get; set; }

    public DateTime? RejectedDate { get; set; }

    /// <summary>Why the sheet was rejected. Cleared if it is later approved.</summary>
    public string? RejectionReason { get; set; }

    /// <summary><c>1</c> is deleted, <c>0</c> is live. <c>NOT NULL</c> with no default.</summary>
    public int IsDelete { get; set; }
}
