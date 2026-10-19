using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Shared arrangements for the TimesheetProject tests: payload builders, row
/// builders, and seed/read helpers over <see cref="ETimeSheetApiFactory.WithDbContextAsync(Func{ETimeSheet.Infrastructure.Data.Context, Task})"/>.
/// <para>
/// Every row is seeded with an <b>explicit</b> id under <c>IDENTITY_INSERT</c>.
/// The reset reseeds identity to 0, and the first row a never-used table
/// receives after that is given id <b>0</b> - which the add-or-edit endpoints
/// read as "add". An id picked by the test is also the only one it can join on.
/// </para>
/// <para>
/// Rows only, never schema (CLAUDE.md §8). Everything goes through the
/// factory's guarded context, so nothing here can reach a real database.
/// </para>
/// </summary>
public static class TimesheetProjectTestData
{
    /// <summary>The organisation the tests work in.</summary>
    public const int OrganizationId = 40;

    /// <summary>Another organisation, whose rows must never leak into <see cref="OrganizationId"/>'s.</summary>
    public const int OtherOrganizationId = 41;

    /// <summary>The user recorded as having made a change - CreatedBy, UpdatedBy, DeletedBy.</summary>
    public const int ActingUserId = 900;

    /// <summary>A live client of <see cref="OrganizationId"/>, with the Indian Rupee as its currency.</summary>
    public const int ClientId = 1;

    /// <summary>A second live client, with the United States Dollar as its currency.</summary>
    public const int SecondClientId = 3;

    /// <summary>A removed client.</summary>
    public const int RemovedClientId = 2;

    public static readonly DateTime SeedDate = new(2026, 1, 15, 9, 0, 0);

    // ============================================================ payloads

    /// <summary>An add-or-edit project payload that passes validation.</summary>
    public static TimesheetProjectSaveRequest ProjectRequest(
        int? projectId = null,
        string? projectName = "Apollo",
        int clientId = ClientId,
        int organizationId = OrganizationId) => new()
    {
        ProjectId = projectId,
        OrganizationId = organizationId,
        ProjectName = projectName,
        TotalBudget = 1500.50m,
        TotalHours = 120.25m,
        StartDate = new DateTime(2026, 4, 1),
        EndDate = new DateTime(2026, 9, 30),
        Status = null,
        ClientId = clientId,
        SubmissionFrequency = 1,
    };

    /// <summary>A budget payload that passes validation.</summary>
    public static TimesheetProjectBudgetSaveRequest BudgetRequest(
        int projectId,
        int? projectCategoryId = null,
        decimal? allocatedHours = 10m,
        decimal? hourlyRate = 50m) => new()
    {
        ProjectId = projectId,
        ProjectCategoryId = projectCategoryId,
        AllocatedHours = allocatedHours,
        HourlyRate = hourlyRate,
        PeriodStart = new DateTime(2026, 4, 1),
        PeriodEnd = new DateTime(2026, 6, 30),
    };

    /// <summary>A project status payload that passes validation.</summary>
    public static TimesheetProjectStatusSaveRequest StatusRequest(
        int? statusId = null,
        string? status = "Active",
        string? colorCode = "#00AA00",
        int organizationId = OrganizationId) => new()
    {
        StatusId = statusId,
        OrganizationId = organizationId,
        Status = status,
        ColorCode = colorCode,
        UserId = ActingUserId,
    };

    /// <summary>A create-timesheet-user payload that passes every shape rule.</summary>
    public static TimesheetUserCreateRequest CreateUserRequest(
        string email = "neha.kapoor@etimesheet.test",
        int projectId = 1,
        int? projectRoleId = 5,
        int? countryId = null,
        int organizationId = OrganizationId) => new()
    {
        FullName = "Neha Kapoor",
        Email = email,
        CountryId = countryId,
        Address = "4 Park Street, Kolkata",
        OrganizationId = organizationId,
        ProjectId = projectId,
        ProjectRoleId = projectRoleId,
        JobType = 1,
        CostRate = 40m,
        BillRate = 75m,
        WorkLocationRange = 100,
        ProjectLocationLatitude = "22.5726",
        MaxLogHoursPerDay = 8,
        CreatedBy = ActingUserId,
    };

    /// <summary>An edit-timesheet-user payload that passes every shape rule.</summary>
    public static TimesheetUserUpdateRequest UpdateUserRequest(
        int timesheetUserProjectSetupId,
        string fullName,
        string email,
        int projectId = 1,
        int? projectRoleId = 5,
        int organizationId = OrganizationId) => new()
    {
        TimesheetUserProjectSetupId = timesheetUserProjectSetupId,
        OrganizationId = organizationId,
        FullName = fullName,
        Email = email,
        ProjectId = projectId,
        ProjectRoleId = projectRoleId,
        JobType = 2,
        CostRate = 45.50m,
        BillRate = 90.25m,
        WorkLocationRange = 250,
        ProjectLocationLatitude = "23.0225",
        ProjectLocationLongitude = "72.5714",
        MaxLogHoursPerDay = 9,
        UpdatedBy = ActingUserId,
    };

    // ================================================================ rows

    /// <summary>A client of <paramref name="organizationId"/>; currency 102 is the Indian Rupee.</summary>
    public static TimesheetClientSetup Client(
        int clientId,
        string clientName,
        int currencyId = MasterAdminTestData.IndiaCurrencyId,
        bool isDeleted = false,
        int organizationId = OrganizationId) => new()
    {
        ClientId = clientId,
        ClientCode = $"CL{clientId:D4}",
        ClientName = clientName,
        BillingEmail = $"client{clientId}@etimesheet.test",
        PhoneNumber = "0000000000",
        PaymentTermsDays = 30,
        CurrencyCode = currencyId,
        CountryId = MasterAdminTestData.IndiaCountryId,
        IsDeleted = isDeleted,
        DeleteDate = isDeleted ? SeedDate : null,
        OrganizationId = organizationId,
    };

    /// <summary>A project row. <c>ClientID</c> is NOT NULL but has no foreign key, so any id will do.</summary>
    public static TimesheetProject Project(
        int projectId,
        string code,
        string? projectName = "Seeded Project",
        int organizationId = OrganizationId,
        int clientId = ClientId,
        int? status = null,
        int? currencyCode = MasterAdminTestData.IndiaCurrencyId,
        int? submissionFrequency = null) => new()
    {
        ProjectId = projectId,
        Code = code,
        ProjectName = projectName,
        TotalBudget = 1000m,
        TotalHours = 100m,
        StartDate = new DateTime(2026, 1, 1),
        EndDate = new DateTime(2026, 12, 31),
        CurrencyCode = currencyCode,
        OrganizationId = organizationId,
        Status = status,
        ClientId = clientId,
        SubmissionFrequency = submissionFrequency,
    };

    /// <summary>A budget line of <paramref name="projectId"/>.</summary>
    public static TimesheetProjectBudget Budget(
        int budgetId,
        int projectId,
        DateTime? periodStart = null,
        int? projectCategoryId = null,
        string? projectCode = null) => new()
    {
        BudgetId = budgetId,
        ProjectId = projectId,
        ProjectCode = projectCode,
        ProjectCategoryId = projectCategoryId,
        AllocatedHours = 10m,
        HourlyRate = 20m,
        AllocatedAmount = 200m,
        PeriodStart = periodStart ?? new DateTime(2026, 1, 1),
        PeriodEnd = new DateTime(2026, 12, 31),
    };

    /// <summary>A project category of <paramref name="organizationId"/>.</summary>
    public static TimesheetProjectCategory Category(
        int categoryId,
        string categoryName,
        int organizationId = OrganizationId,
        bool isDeleted = false) => new()
    {
        CategoryId = categoryId,
        CategoryName = categoryName,
        OrganizationId = organizationId,
        IsDelete = isDeleted,
    };

    /// <summary>A project status of <paramref name="organizationId"/>, live unless <paramref name="isDeleted"/>.</summary>
    public static TimesheetProjectStatus Status(
        int statusId,
        string status,
        string? colorCode = "#123456",
        int organizationId = OrganizationId,
        bool isDeleted = false) => new()
    {
        StatusId = statusId,
        Status = status,
        ColorCode = colorCode,
        CreateDate = SeedDate,
        CreatedBy = 1,
        DeletedDate = isDeleted ? SeedDate : null,
        DeletedBy = isDeleted ? 1 : null,
        OrganizationId = organizationId,
    };

    /// <summary>A project role of <paramref name="organizationId"/>.</summary>
    public static TimesheetUserRole Role(
        int roleId,
        string roleName,
        int organizationId = OrganizationId,
        bool isDeleted = false) => new()
    {
        RoleId = roleId,
        RoleName = roleName,
        OrganizationId = organizationId,
        IsDelete = isDeleted,
    };

    /// <summary>A user in <c>dbo.Signup</c>, seeded through <see cref="ETimeSheetApiFactory.SeedEmployeesAsync"/>.</summary>
    public static EmployeeListTestData.SignupRow User(
        int userId,
        string name,
        string email,
        int organizationId = OrganizationId,
        bool isDeleted = false) =>
        new(userId, name, email, RoleId: 2, organizationId, isDeleted);

    /// <summary>One assignment of a user to a project, live unless <paramref name="isDeleted"/>.</summary>
    public static TimesheetUserProjectSetup Assignment(
        int setupId,
        int userId,
        int projectId,
        int? projectRoleId = 5,
        bool isDeleted = false) => new()
    {
        TimesheetUserProjectSetupId = setupId,
        UserId = userId,
        ProjectId = projectId,
        ProjectRoleId = projectRoleId,
        CostRate = 30m,
        BillRate = 60m,
        CreateDate = SeedDate,
        CreatedBy = 1,
        IsDelete = isDeleted ? 1 : 0,
        DeletedDate = isDeleted ? SeedDate : null,
        DeletedBy = isDeleted ? 1 : null,
        JobType = 1,
        WorkLocationRange = 50,
        ProjectLocationLatitude = "19.0760",
        ProjectLocationLongitude = "72.8777",
        MaxLogHoursPerDay = 8,
    };

    // ================================================================ seed

    /// <summary>
    /// Inserts <c>dbo.Organization</c> rows. The table is built from the EF
    /// mapping in the container, so only the mapped columns exist.
    /// </summary>
    public static Task SeedOrganizationsAsync(this ETimeSheetApiFactory factory, params int[] organizationIds) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var organizationId in organizationIds)
            {
                var name = $"Organization {organizationId}";

                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.Organization ON;
                    INSERT INTO dbo.Organization (OrganizationID, OrganizationName)
                    VALUES ({organizationId}, {name});
                    SET IDENTITY_INSERT dbo.Organization OFF;");
            }
        });

    /// <summary>
    /// Inserts <c>dbo.TimesheetClientSetup</c> rows, <c>OrganizationID</c>
    /// included - the column is NOT NULL with no default.
    /// </summary>
    public static Task SeedProjectClientsAsync(this ETimeSheetApiFactory factory, params TimesheetClientSetup[] clients) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var client in clients)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetClientSetup ON;
                    INSERT INTO dbo.TimesheetClientSetup
                        (ClientID, ClientCode, Clientname, BillingEmail, PhoneNumber,
                         payment_terms_days, Currencycode, CountryID, IsDeleted, DeleteDate, OrganizationID)
                    VALUES
                        ({client.ClientId}, {client.ClientCode}, {client.ClientName}, {client.BillingEmail},
                         {client.PhoneNumber}, {client.PaymentTermsDays}, {client.CurrencyCode},
                         {client.CountryId}, {client.IsDeleted}, {client.DeleteDate}, {client.OrganizationId});
                    SET IDENTITY_INSERT dbo.TimesheetClientSetup OFF;");
            }
        });

    public static Task SeedProjectsAsync(this ETimeSheetApiFactory factory, params TimesheetProject[] projects) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var project in projects)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetProject ON;
                    INSERT INTO dbo.TimesheetProject
                        (ProjectID, Code, Projectname, TotalBudget, Totalhours, StartDate, EndDate,
                         CurrencyCode, OrganizationID, Status, ClientID, SubmissionFrequency)
                    VALUES
                        ({project.ProjectId}, {project.Code}, {project.ProjectName}, {project.TotalBudget},
                         {project.TotalHours}, {project.StartDate}, {project.EndDate}, {project.CurrencyCode},
                         {project.OrganizationId}, {project.Status}, {project.ClientId}, {project.SubmissionFrequency});
                    SET IDENTITY_INSERT dbo.TimesheetProject OFF;");
            }
        });

    public static Task SeedBudgetsAsync(this ETimeSheetApiFactory factory, params TimesheetProjectBudget[] budgets) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var budget in budgets)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetProjectBudget ON;
                    INSERT INTO dbo.TimesheetProjectBudget
                        (BudgetID, ProjectID, ProjectCode, ProjectCategoryID, AllocatedHours, Hourlyrate,
                         Allocated_amount, PeriodStart, PeriodEnd)
                    VALUES
                        ({budget.BudgetId}, {budget.ProjectId}, {budget.ProjectCode}, {budget.ProjectCategoryId},
                         {budget.AllocatedHours}, {budget.HourlyRate}, {budget.AllocatedAmount},
                         {budget.PeriodStart}, {budget.PeriodEnd});
                    SET IDENTITY_INSERT dbo.TimesheetProjectBudget OFF;");
            }
        });

    public static Task SeedCategoriesAsync(this ETimeSheetApiFactory factory, params TimesheetProjectCategory[] categories) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var category in categories)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetProjectCategory ON;
                    INSERT INTO dbo.TimesheetProjectCategory (ID, Categoryname, OrganizationID, IsDelete)
                    VALUES ({category.CategoryId}, {category.CategoryName}, {category.OrganizationId}, {category.IsDelete});
                    SET IDENTITY_INSERT dbo.TimesheetProjectCategory OFF;");
            }
        });

    public static Task SeedStatusesAsync(this ETimeSheetApiFactory factory, params TimesheetProjectStatus[] statuses) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var status in statuses)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetProjectStatus ON;
                    INSERT INTO dbo.TimesheetProjectStatus
                        (StatusID, Status, Colorcode, CreateDate, CreatedBy, DeletedDate, Deletedby, OrgID)
                    VALUES
                        ({status.StatusId}, {status.Status}, {status.ColorCode}, {status.CreateDate},
                         {status.CreatedBy}, {status.DeletedDate}, {status.DeletedBy}, {status.OrganizationId});
                    SET IDENTITY_INSERT dbo.TimesheetProjectStatus OFF;");
            }
        });

    public static Task SeedUserRolesAsync(this ETimeSheetApiFactory factory, params TimesheetUserRole[] roles) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var role in roles)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetUserRole ON;
                    INSERT INTO dbo.TimesheetUserRole (RoleID, Rolename, OrganizationID, IsDelete)
                    VALUES ({role.RoleId}, {role.RoleName}, {role.OrganizationId}, {role.IsDelete});
                    SET IDENTITY_INSERT dbo.TimesheetUserRole OFF;");
            }
        });

    public static Task SeedAssignmentsAsync(this ETimeSheetApiFactory factory, params TimesheetUserProjectSetup[] setups) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var setup in setups)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.Timesheet_Users_Project_setup ON;
                    INSERT INTO dbo.Timesheet_Users_Project_setup
                        (TimesheetUserProjectSetupID, UserID, ProjectRoleID, Cost_rate_per_hour,
                         Billable_rate_per_hour, CreateDate, Createdby, IsDelete, DeletedDate, DeletedBy,
                         Jobtype, WorklocationRange, ProjectID, ProjectlocLatitude, ProjectlocLongtitude,
                         Max_log_hour_day)
                    VALUES
                        ({setup.TimesheetUserProjectSetupId}, {setup.UserId}, {setup.ProjectRoleId}, {setup.CostRate},
                         {setup.BillRate}, {setup.CreateDate}, {setup.CreatedBy}, {setup.IsDelete},
                         {setup.DeletedDate}, {setup.DeletedBy}, {setup.JobType}, {setup.WorkLocationRange},
                         {setup.ProjectId}, {setup.ProjectLocationLatitude}, {setup.ProjectLocationLongitude},
                         {setup.MaxLogHoursPerDay});
                    SET IDENTITY_INSERT dbo.Timesheet_Users_Project_setup OFF;");
            }
        });

    // ================================================================ read

    /// <summary>Every project row, in id order - what an endpoint actually wrote.</summary>
    public static Task<List<TimesheetProject>> ProjectRowsAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.TimesheetProject
            .AsNoTracking()
            .OrderBy(project => project.ProjectId)
            .ToListAsync());

    /// <summary>Every budget row, in id order.</summary>
    public static Task<List<TimesheetProjectBudget>> BudgetRowsAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.TimesheetProjectBudget
            .AsNoTracking()
            .OrderBy(budget => budget.BudgetId)
            .ToListAsync());

    /// <summary>Every status row, deleted ones included, in id order.</summary>
    public static Task<List<TimesheetProjectStatus>> StatusRowsAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.TimesheetProjectStatus
            .AsNoTracking()
            .IgnoreQueryFilters()
            .OrderBy(status => status.StatusId)
            .ToListAsync());

    /// <summary>Every assignment row, deleted ones included, in id order.</summary>
    public static Task<List<TimesheetUserProjectSetup>> AssignmentRowsAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.Timesheet_Users_Project_setup
            .AsNoTracking()
            .IgnoreQueryFilters()
            .OrderBy(setup => setup.TimesheetUserProjectSetupId)
            .ToListAsync());

    /// <summary>One user's <c>dbo.Signup</c> row.</summary>
    public static Task<Signup?> SignupRowAsync(this ETimeSheetApiFactory factory, int userId) =>
        factory.WithDbContextAsync(db => db.Signup
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserId == userId));

    /// <summary>How many <c>dbo.Signup</c> rows there are.</summary>
    public static Task<int> SignupCountAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.Signup.CountAsync());
}
