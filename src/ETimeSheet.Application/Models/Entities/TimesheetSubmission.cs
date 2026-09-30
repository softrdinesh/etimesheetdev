namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A submitted timesheet, mapped onto the existing <c>dbo.TimesheetSubmission</c>
/// table. This is a persistence/domain model and is never returned from a
/// controller.
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the table has no soft-delete
/// or audit columns, only its own submit / approve / reject trail.
/// </para>
/// </summary>
public class TimesheetSubmission
{
    /// <summary>
    /// The timesheet's code - <c>varchar(15)</c>, and the table's primary key:
    /// a sheet is submitted once.
    /// </summary>
    public string Timesheetcode { get; set; } = string.Empty;

    /// <summary>Total hours submitted. <c>decimal(18,2)</c>, NOT NULL.</summary>
    public decimal TotalHours { get; set; }

    /// <summary>Total minutes submitted. <c>decimal(18,2)</c>, NOT NULL.</summary>
    public decimal TotalMins { get; set; }

    /// <summary>The user who submitted the timesheet. NOT NULL.</summary>
    public int SubmittedBy { get; set; }

    /// <summary>When the timesheet was submitted. <c>datetime</c>, NOT NULL.</summary>
    public DateTime SubmittedDate { get; set; }

    /// <summary>The user who approved it, if anyone has.</summary>
    public int? ApprovedBy { get; set; }

    /// <summary>When it was approved. <c>datetime</c>, nullable.</summary>
    public DateTime? ApprovedDate { get; set; }

    /// <summary>The user who rejected it, if anyone has.</summary>
    public int? RejectedBy { get; set; }

    /// <summary>When it was rejected. <c>datetime</c>, nullable.</summary>
    public DateTime? RejectedDate { get; set; }

    /// <summary>The submission's status. Nullable.</summary>
    public int? StatusId { get; set; }
}
