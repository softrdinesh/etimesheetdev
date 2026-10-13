namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A project of an organisation, mapped onto the existing
/// <c>dbo.TimesheetProject</c> table. This is a persistence/domain model and is
/// never returned from a controller.
/// <para>
/// Mapped for the remove only: the list and the add/edit go through
/// <c>spc_GetTimesheetProject</c> and <c>spc_AddEditTimesheetProject</c>.
/// </para>
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the table has no audit or
/// soft-delete columns, so a removed project is physically deleted.
/// </para>
/// </summary>
public class TimesheetProject
{
    /// <summary>The <c>ProjectID</c> column - <c>IDENTITY(1,1)</c>.</summary>
    public int ProjectId { get; set; }

    /// <summary>The <c>Code</c> column. <c>nvarchar(20)</c>, NOT NULL.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The <c>Projectname</c> column. <c>nvarchar(100)</c>, nullable.</summary>
    public string? ProjectName { get; set; }

    /// <summary>The <c>TotalBudget</c> column. <c>decimal(18,2)</c>.</summary>
    public decimal? TotalBudget { get; set; }

    /// <summary>The <c>Totalhours</c> column. <c>decimal(18,2)</c> - a number of hours.</summary>
    public decimal? TotalHours { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int? CurrencyCode { get; set; }

    /// <summary>The <c>OrganizationID</c> column. Nullable on the table.</summary>
    public int? OrganizationId { get; set; }

    /// <summary>A <c>dbo.TimesheetProjectStatus.StatusID</c>; no foreign key.</summary>
    public int? Status { get; set; }
}
