namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One project timesheet - a user's sheet for one project over one submission
/// period - mapped onto the <c>dbo.TimesheetProjectSubmission</c> table. This
/// is a persistence/domain model and is never returned from a controller.
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the columns differ
/// (<c>dateadded</c>, and an <c>int IsDelete</c>).
/// </para>
/// </summary>
public class TimesheetProjectSubmission
{
    /// <summary>The <c>TimesheetProjectSubmissionID</c> column - <c>IDENTITY(1,1)</c>, the primary key.</summary>
    public int TimesheetProjectSubmissionId { get; set; }

    /// <summary>The <c>SheetSubmissionCode</c> column - <c>PSHTnnnn</c>. <c>nvarchar(20)</c>, NOT NULL.</summary>
    public string SheetSubmissionCode { get; set; } = string.Empty;

    /// <summary>See <c>Constants.TimesheetProjectSubmission.Status</c>.</summary>
    public int StatusId { get; set; }

    public int UserId { get; set; }

    public int ProjectId { get; set; }

    public int OrganizationId { get; set; }

    /// <summary>The first day of the period, at midnight.</summary>
    public DateTime PeriodStartDate { get; set; }

    /// <summary>The last day of the period, at midnight - inclusive.</summary>
    public DateTime PeriodEndDate { get; set; }

    /// <summary>The sheet's live logged time in hours. <c>decimal(18,2)</c>, NOT NULL.</summary>
    public decimal TotalHoursLogged { get; set; }

    /// <summary>The sheet's live logged time in minutes. <c>decimal(18,2)</c>, NOT NULL.</summary>
    public decimal TotalMinutesLogged { get; set; }

    public int? SubmittedBy { get; set; }

    public DateTime? SubmittedDate { get; set; }

    public int? ApprovedBy { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public int? RejectedBy { get; set; }

    public DateTime? RejectedDate { get; set; }

    public string? RejectionReason { get; set; }

    /// <summary>Soft-delete flag. 1 = deleted, 0 = live. NOT NULL.</summary>
    public int IsDelete { get; set; }

    /// <summary>The <c>dateadded</c> column.</summary>
    public DateTime? DateAdded { get; set; }
}
