using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Common.Mapping;

/// <summary>
/// Hand-written projections from the TimesheetProject procedure rows to the
/// TimesheetProject response, explicit for the same reason as
/// <see cref="AdminMappings"/>.
/// </summary>
internal static class TimesheetProjectMappings
{
    /// <summary>Projects a project row from <c>spc_GetTimesheetProject</c> to the project response.</summary>
    internal static TimesheetProjectResponse ToResponse(this TimesheetProjectDetail project) => new()
    {
        ProjectId = project.ProjectId,
        Code = project.Code,
        ProjectName = project.ProjectName,
        TotalBudget = project.TotalBudget,
        TotalHours = project.TotalHours,
        StartDate = project.StartDate,
        EndDate = project.EndDate,
        CurrencyCode = project.CurrencyCode,
        Status = project.Status,
        StatusName = project.StatusName,
        ClientId = project.ClientId,
        ClientName = project.ClientName,
    };

    /// <summary>Projects the project <c>spc_AddEditTimesheetProject</c> saved to the project response.</summary>
    internal static TimesheetProjectResponse ToResponse(this TimesheetProjectSaveDetail project) => new()
    {
        ProjectId = project.ProjectId,
        Code = project.Code,
        ProjectName = project.ProjectName,
        TotalBudget = project.TotalBudget,
        TotalHours = project.TotalHours,
        StartDate = project.StartDate,
        EndDate = project.EndDate,
        CurrencyCode = project.CurrencyCode,
        Status = project.Status,
        StatusName = project.StatusName,
        ClientId = project.ClientId,
        ClientName = project.ClientName,
    };

    /// <summary>Projects a status row from <c>spc_GetTimesheetProjectStatus</c> to the status response.</summary>
    internal static TimesheetProjectStatusResponse ToResponse(this TimesheetProjectStatusDetail status) => new()
    {
        StatusId = status.StatusId,
        Status = status.Status,
        ColorCode = status.ColorCode,
    };

    /// <summary>Projects the row <c>spc_AddEditTimesheetProjectStatus</c> returns to the status response.</summary>
    internal static TimesheetProjectStatusResponse ToResponse(this TimesheetProjectStatusSaveDetail status) => new()
    {
        StatusId = status.StatusId,
        Status = status.Status,
        ColorCode = status.ColorCode,
    };

    /// <summary>Projects a budget row from <c>spc_GetTimesheetProjectBudget</c> to the budget response.</summary>
    internal static TimesheetProjectBudgetResponse ToResponse(this TimesheetProjectBudgetDetail budget) => new()
    {
        BudgetId = budget.BudgetId,
        ProjectCode = budget.ProjectCode,
        ProjectCategoryId = budget.ProjectCategoryId,
        CategoryName = budget.CategoryName,
        AllocatedHours = budget.AllocatedHours,
        HourlyRate = budget.HourlyRate,
        AllocatedAmount = budget.AllocatedAmount,
        PeriodStart = budget.PeriodStart,
        PeriodEnd = budget.PeriodEnd,
    };

    /// <summary>Projects the budget line <c>spc_AddEditTimesheetProjectBudget</c> saved to the budget response.</summary>
    internal static TimesheetProjectBudgetResponse ToResponse(this TimesheetProjectBudgetSaveDetail budget) => new()
    {
        BudgetId = budget.BudgetId,
        ProjectCode = budget.ProjectCode,
        ProjectCategoryId = budget.ProjectCategoryId,
        CategoryName = budget.CategoryName,
        AllocatedHours = budget.AllocatedHours,
        HourlyRate = budget.HourlyRate,
        AllocatedAmount = budget.AllocatedAmount,
        PeriodStart = budget.PeriodStart,
        PeriodEnd = budget.PeriodEnd,
    };
}
