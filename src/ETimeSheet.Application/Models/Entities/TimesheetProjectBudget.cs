namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A budget line of a project, mapped onto the existing
/// <c>dbo.TimesheetProjectBudget</c> table. This is a persistence/domain model
/// and is never returned from a controller.
/// <para>
/// Mapped for the remove only: the list and the add/edit go through
/// <c>spc_GetTimesheetProjectBudget</c> and
/// <c>spc_AddEditTimesheetProjectBudget</c>.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the table has no audit or
/// soft-delete columns, so a removed line is physically deleted.
/// </para>
/// </summary>
public class TimesheetProjectBudget
{
    /// <summary>The <c>BudgetID</c> column - <c>IDENTITY(1,1)</c>.</summary>
    public int BudgetId { get; set; }

    /// <summary>The <c>ProjectID</c> column - a <c>dbo.TimesheetProject.ProjectID</c>.</summary>
    public int? ProjectId { get; set; }

    /// <summary>
    /// The <c>ProjectCode</c> column. <c>nvarchar(50)</c> - a copy of the
    /// project's code, written by <c>spc_AddEditTimesheetProjectBudget</c>.
    /// </summary>
    public string? ProjectCode { get; set; }

    public int? ProjectCategoryId { get; set; }

    public decimal? AllocatedHours { get; set; }

    /// <summary>The <c>Hourlyrate</c> column.</summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>The <c>Allocated_amount</c> column.</summary>
    public decimal? AllocatedAmount { get; set; }

    /// <summary>The <c>PeriodStart</c> column. <c>date</c> - no time part.</summary>
    public DateTime? PeriodStart { get; set; }

    /// <summary>The <c>PeriodEnd</c> column. <c>date</c> - no time part.</summary>
    public DateTime? PeriodEnd { get; set; }
}
