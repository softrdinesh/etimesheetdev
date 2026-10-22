using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;

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
        CurrencySymbol = project.CurrencySymbol,
        Status = project.Status,
        StatusName = project.StatusName,
        ClientId = project.ClientId,
        ClientName = project.ClientName,
        SubmissionFrequency = project.SubmissionFrequency,
        SubmissionFrequencyName = project.SubmissionFrequencyName,
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
        CurrencySymbol = project.CurrencySymbol,
        Status = project.Status,
        StatusName = project.StatusName,
        ClientId = project.ClientId,
        ClientName = project.ClientName,
        SubmissionFrequency = project.SubmissionFrequency,
        SubmissionFrequencyName = project.SubmissionFrequencyName,
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

    /// <summary>Projects a budget row from <c>spc_GetTimesheetProjectBudgetByOrgID</c> to the budget-by-organisation response.</summary>
    internal static TimesheetProjectBudgetByOrgResponse ToResponse(this TimesheetProjectBudgetByOrgDetail budget) => new()
    {
        ProjectId = budget.ProjectId,
        ProjectCode = budget.ProjectCode,
        ProjectName = budget.ProjectName,
        BudgetId = budget.BudgetId,
        ProjectCategoryId = budget.ProjectCategoryId,
        CategoryName = budget.CategoryName,
        AllocatedHours = budget.AllocatedHours,
        HourlyRate = budget.HourlyRate,
        AllocatedAmount = budget.AllocatedAmount,
        PeriodStart = budget.PeriodStart,
        PeriodEnd = budget.PeriodEnd,
    };

    internal static TimesheetUserListResponse ToResponse(this TimesheetUserListDetail user) => new()
    {
        TimesheetUserProjectSetupId = user.TimesheetUserProjectSetupId,
        UserId = user.UserId,
        Name = user.Name,
        Email = user.Email,
        OrganizationId = user.OrganizationId,
        ProjectId = user.ProjectId,
        ProjectCode = user.ProjectCode,
        ProjectName = user.ProjectName,
        ProjectRoleId = user.ProjectRoleId,
        ProjectRoleName = user.ProjectRoleName,
        JobType = user.JobType,
        CostRate = user.CostRate,
        BillRate = user.BillRate,
        WorkLocationRange = user.WorkLocationRange,
        ProjectLocationLatitude = user.ProjectLocationLatitude,
        ProjectLocationLongitude = user.ProjectLocationLongitude,
        MaxLogHoursPerDay = user.MaxLogHoursPerDay,
    };

    /// <summary>Projects a holiday row from <c>spc_GetTimesheetHolidaySetup</c> to the holiday response.</summary>
    internal static TimesheetHolidaySetupResponse ToResponse(this TimesheetHolidaySetupDetail holiday) => new()
    {
        TimesheetHolidaySetupId = holiday.TimesheetHolidaySetupId,
        HolidayDate = holiday.HolidayDate,
        Description = holiday.Description,
        OrganizationId = holiday.OrganizationId,
        CountryId = holiday.CountryId,
        CountryName = holiday.CountryName,
        ClientId = holiday.ClientId,
        ClientName = holiday.ClientName,
        ProjectId = holiday.ProjectId,
        ProjectCode = holiday.ProjectCode,
        ProjectName = holiday.ProjectName,
        CreateDate = holiday.CreateDate,
        CreatedBy = holiday.CreatedBy,
    };

    /// <summary>Projects the row <c>spc_AddEditTimesheetHolidaySetup</c> returns to the holiday response.</summary>
    internal static TimesheetHolidaySetupResponse ToResponse(this TimesheetHolidaySetupSaveDetail holiday) => new()
    {
        TimesheetHolidaySetupId = holiday.TimesheetHolidaySetupId,
        HolidayDate = holiday.HolidayDate,
        Description = holiday.Description,
        OrganizationId = holiday.OrganizationId,
        CountryId = holiday.CountryId,
        CountryName = holiday.CountryName,
        ClientId = holiday.ClientId,
        ClientName = holiday.ClientName,
        ProjectId = holiday.ProjectId,
        ProjectCode = holiday.ProjectCode,
        ProjectName = holiday.ProjectName,
        CreateDate = holiday.CreateDate,
        CreatedBy = holiday.CreatedBy,
    };

    /// <summary>Projects a setup row from <c>spc_GetTimesheetWeekendSetup</c> to the weekend setup response.</summary>
    internal static TimesheetWeekendSetupResponse ToResponse(this TimesheetWeekendSetupDetail setup) => new()
    {
        TimeSheetWeekendSetupId = setup.TimeSheetWeekendSetupId,
        OrganizationId = setup.OrganizationId,
        ClientId = setup.ClientId,
        ClientName = setup.ClientName,
        ProjectId = setup.ProjectId,
        ProjectCode = setup.ProjectCode,
        ProjectName = setup.ProjectName,
        IsMondayWeekend = setup.IsMondayWeekend,
        IsTuesdayWeekend = setup.IsTuesdayWeekend,
        IsWednesdayWeekend = setup.IsWednesdayWeekend,
        IsThursdayWeekend = setup.IsThursdayWeekend,
        IsFridayWeekend = setup.IsFridayWeekend,
        IsSaturdayWeekend = setup.IsSaturdayWeekend,
        IsSundayWeekend = setup.IsSundayWeekend,
        CreateDate = setup.CreateDate,
        CreatedBy = setup.CreatedBy,
        UpdatedDate = setup.UpdatedDate,
        UpdatedBy = setup.UpdatedBy,
    };

    /// <summary>Projects the row <c>spc_AddEditTimesheetWeekendSetup</c> returns to the weekend setup response.</summary>
    internal static TimesheetWeekendSetupResponse ToResponse(this TimesheetWeekendSetupSaveDetail setup) => new()
    {
        TimeSheetWeekendSetupId = setup.TimeSheetWeekendSetupId,
        OrganizationId = setup.OrganizationId,
        ClientId = setup.ClientId,
        ClientName = setup.ClientName,
        ProjectId = setup.ProjectId,
        ProjectCode = setup.ProjectCode,
        ProjectName = setup.ProjectName,
        IsMondayWeekend = setup.IsMondayWeekend,
        IsTuesdayWeekend = setup.IsTuesdayWeekend,
        IsWednesdayWeekend = setup.IsWednesdayWeekend,
        IsThursdayWeekend = setup.IsThursdayWeekend,
        IsFridayWeekend = setup.IsFridayWeekend,
        IsSaturdayWeekend = setup.IsSaturdayWeekend,
        IsSundayWeekend = setup.IsSundayWeekend,
        CreateDate = setup.CreateDate,
        CreatedBy = setup.CreatedBy,
        UpdatedDate = setup.UpdatedDate,
        UpdatedBy = setup.UpdatedBy,
    };

    /// <summary>Projects an assignment row from <c>spc_GetUserAccessSetup</c> to the role assignment response.</summary>
    internal static TimesheetRoleAssignmentResponse ToResponse(this TimesheetRoleAssignmentDetail assignment) => new()
    {
        AssignmentId = assignment.AssignmentId,
        UserId = assignment.UserId,
        UserName = assignment.UserName,
        Email = assignment.Email,
        OrganizationId = assignment.OrganizationId,
        IsClientSetup = assignment.IsClientSetup,
        IsProject = assignment.IsProject,
        IsProjectBudget = assignment.IsProjectBudget,
        IsProjectCategory = assignment.IsProjectCategory,
        IsStatusSetup = assignment.IsStatusSetup,
        IsRoleSetup = assignment.IsRoleSetup,
        IsUserSetup = assignment.IsUserSetup,
        IsTimeLog = assignment.IsTimeLog,
        IsViewTimesheet = assignment.IsViewTimesheet,
        CreateDate = assignment.CreateDate,
        CreatedBy = assignment.CreatedBy,
        UpdatedDate = assignment.UpdatedDate,
        UpdatedBy = assignment.UpdatedBy,
    };

    /// <summary>Projects the row <c>spc_AddEditUserAccessSetup</c> returns to the role assignment response.</summary>
    internal static TimesheetRoleAssignmentResponse ToResponse(this TimesheetRoleAssignmentSaveDetail assignment) => new()
    {
        AssignmentId = assignment.AssignmentId,
        UserId = assignment.UserId,
        UserName = assignment.UserName,
        Email = assignment.Email,
        OrganizationId = assignment.OrganizationId,
        IsClientSetup = assignment.IsClientSetup,
        IsProject = assignment.IsProject,
        IsProjectBudget = assignment.IsProjectBudget,
        IsProjectCategory = assignment.IsProjectCategory,
        IsStatusSetup = assignment.IsStatusSetup,
        IsRoleSetup = assignment.IsRoleSetup,
        IsUserSetup = assignment.IsUserSetup,
        IsTimeLog = assignment.IsTimeLog,
        IsViewTimesheet = assignment.IsViewTimesheet,
        CreateDate = assignment.CreateDate,
        CreatedBy = assignment.CreatedBy,
        UpdatedDate = assignment.UpdatedDate,
        UpdatedBy = assignment.UpdatedBy,
    };

    /// <summary>
    /// Projects a user's day on a project, and the submission period it is in,
    /// to the time log response.
    /// </summary>
    internal static TimesheetProjectTimeLogResponse ToResponse(
        this TimesheetProjectTimeLog timeLog,
        DateTime? periodStartDate,
        DateTime? periodEndDate) => new()
    {
        TimesheetProjectTimeLogId = timeLog.TimesheetProjectTimeLogId,
        UserId = timeLog.UserId,
        ProjectId = timeLog.ProjectId,
        OrganizationId = timeLog.OrganizationId,
        SheetSubmissionCode = timeLog.SheetSubmissionCode,
        PeriodStartDate = periodStartDate,
        PeriodEndDate = periodEndDate,
        LogDate = timeLog.LogDate,
        HoursLogged = timeLog.HoursLogged,
        Description = timeLog.Description,
        CreateDate = timeLog.CreateDate,
        CreatedBy = timeLog.CreatedBy,
        UpdateDate = timeLog.UpdateDate,
        UpdatedBy = timeLog.UpdatedBy,
    };

    /// <summary>
    /// Projects a stored sheet to the sheet response. <paramref name="project"/>
    /// is null only for a sheet whose project no longer exists.
    /// </summary>
    internal static TimesheetProjectSheetResponse ToResponse(
        this TimesheetProjectSheetDetail sheet,
        TimesheetProject? project,
        string statusName) => new()
    {
        TimesheetProjectSubmissionId = sheet.TimesheetProjectSubmissionId,
        SheetSubmissionCode = sheet.SheetSubmissionCode,
        StatusId = sheet.StatusId,
        StatusName = statusName,
        UserId = sheet.UserId,
        ProjectId = sheet.ProjectId,
        ProjectCode = project?.Code,
        ProjectName = project?.ProjectName,
        PeriodStartDate = sheet.PeriodStartDate,
        PeriodEndDate = sheet.PeriodEndDate,
        TimeLogCount = sheet.TimeLogCount,
        TotalMinutesLogged = sheet.TotalMinutesLogged,
        TotalHoursLogged = ToHours(sheet.TotalMinutesLogged),
        SubmittedBy = sheet.SubmittedBy,
        SubmittedDate = sheet.SubmittedDate,
        ApprovedBy = sheet.ApprovedBy,
        ApprovedDate = sheet.ApprovedDate,
        RejectedBy = sheet.RejectedBy,
        RejectedDate = sheet.RejectedDate,
        RejectionReason = sheet.RejectionReason,
    };

    /// <summary>Minutes as hours, rounded to 2 places: 90 is 1.5.</summary>
    internal static decimal ToHours(int minutes) => decimal.Round(minutes / 60m, 2);

    /// <summary>Projects an approver setup row from <c>spc_GetTimesheetApproverSetup</c> to the approver setup response.</summary>
    internal static TimesheetApproverSetupResponse ToResponse(this TimesheetApproverSetupDetail setup) => new()
    {
        TimesheetApproverSetupId = setup.TimesheetApproverSetupId,
        OrganizationId = setup.OrganizationId,
        ProjectId = setup.ProjectId,
        ProjectCode = setup.ProjectCode,
        ProjectName = setup.ProjectName,
        ApproverId = setup.ApproverId,
        ApproverName = setup.ApproverName,
        ApproverEmail = setup.ApproverEmail,
        IsTimesheetApprover = setup.IsTimesheetApprover,
        CreateDate = setup.CreateDate,
        CreatedBy = setup.CreatedBy,
    };

    /// <summary>Projects the row <c>spc_AddEditTimesheetApproverSetup</c> returns to the approver setup response.</summary>
    internal static TimesheetApproverSetupResponse ToResponse(this TimesheetApproverSetupSaveDetail setup) => new()
    {
        TimesheetApproverSetupId = setup.TimesheetApproverSetupId,
        OrganizationId = setup.OrganizationId,
        ProjectId = setup.ProjectId,
        ProjectCode = setup.ProjectCode,
        ProjectName = setup.ProjectName,
        ApproverId = setup.ApproverId,
        ApproverName = setup.ApproverName,
        ApproverEmail = setup.ApproverEmail,
        IsTimesheetApprover = setup.IsTimesheetApprover,
        CreateDate = setup.CreateDate,
        CreatedBy = setup.CreatedBy,
    };
}
