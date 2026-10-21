using System.Globalization;
using System.Net;
using System.Net.Mail;
using ETimeSheet.Application.Common.Mapping;
using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Application.Services.Interfaces;
using ETimeSheet.Shared.Configuration;
using ETimeSheet.Shared.Exceptions;
using ETimeSheet.Shared.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETimeSheet.Application.Services.Implementations;

/// <summary>
/// Business logic for the TimesheetProject module.
/// <para>
/// It owns the validation of every payload, the add-or-update decision, and
/// what a remove means. It reaches the database only through
/// <see cref="ITimesheetProjectRepository"/>, and never sees <c>Context</c>.
/// </para>
/// <para>
/// <b>No authorisation check</b>, as in <c>AdminService</c>: authentication
/// is switched off for this project for now.
/// </para>
/// </summary>
public class TimesheetProjectService : ITimesheetProjectService
{
    /// <summary><c>dbo.TimesheetProject.Projectname</c> is <c>nvarchar(100)</c>.</summary>
    private const int ProjectNameMaximumLength = 100;

    /// <summary><c>dbo.TimesheetProjectStatus.Status</c> is <c>nvarchar(20)</c>.</summary>
    private const int StatusNameMaximumLength = 20;

    /// <summary><c>dbo.TimesheetProjectStatus.Colorcode</c> is <c>nvarchar(25)</c>.</summary>
    private const int ColorCodeMaximumLength = 25;

    /// <summary><c>dbo.TimesheetHolidaysetup.Description</c> is <c>nvarchar(100)</c>.</summary>
    private const int HolidayDescriptionMaximumLength = 100;

    /// <summary><c>spc_Signup_timesheet_user</c>'s <c>@Fullname</c> is <c>nvarchar(50)</c>; a longer name would be cut short silently.</summary>
    private const int FullNameMaximumLength = 50;

    /// <summary><c>dbo.Signup.Email</c> is <c>nvarchar(50)</c>.</summary>
    private const int EmailMaximumLength = 50;

    /// <summary>
    /// <c>dbo.Timesheet_Users_Project_setup.ProjectlocLatitude</c> and
    /// <c>ProjectlocLongtitude</c> - and the procedure's <c>@Projectloclatitude</c> -
    /// are <c>nvarchar(25)</c>.
    /// </summary>
    private const int CoordinateMaximumLength = 25;

    /// <summary>The largest value a <c>decimal(18,2)</c> column holds.</summary>
    private const decimal RateMaximum = 9999999999999999.99m;

    /// <summary>
    /// The most <c>Max_log_hour_day</c> may be: a day holds no more hours than
    /// this, so a larger cap could never bind.
    /// </summary>
    private const int MaxLogHoursPerDayMaximum = 24;

    /// <summary>The values <c>dbo.TimesheetProject.SubmissionFrequency</c> accepts.</summary>
    private static readonly IReadOnlySet<int> SubmissionFrequencies = new HashSet<int>
    {
        Constants.TimesheetProject.SubmissionFrequency.Weekly,
        Constants.TimesheetProject.SubmissionFrequency.FifteenDays,
        Constants.TimesheetProject.SubmissionFrequency.Monthly,
    };

    /// <summary>The welcome email for a new account, in <c>Infrastructure/Assets/EmailTemplates/</c>.</summary>
    private const string WelcomeEmailTemplateName = "WelcomeUserTemplate.html";

    private readonly ITimesheetProjectRepository _timesheetProjectRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateStore _emailTemplateStore;
    private readonly WelcomeEmailSettings _welcomeEmailSettings;
    private readonly ILogger<TimesheetProjectService> _logger;

    public TimesheetProjectService(
        ITimesheetProjectRepository timesheetProjectRepository,
        IDateTimeProvider dateTimeProvider,
        IEmailSender emailSender,
        IEmailTemplateStore emailTemplateStore,
        IOptions<WelcomeEmailSettings> welcomeEmailSettings,
        ILogger<TimesheetProjectService> logger)
    {
        _timesheetProjectRepository = timesheetProjectRepository;
        _dateTimeProvider = dateTimeProvider;
        _emailSender = emailSender;
        _emailTemplateStore = emailTemplateStore;
        _welcomeEmailSettings = welcomeEmailSettings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TimesheetProjectResponse>> GetListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        // Checked here rather than by a route constraint: a 0 is told what is
        // wrong with it instead of missing the route.
        if (organizationId <= 0)
        {
            throw new ValidationException(
                "orgID",
                "orgID is required and must be greater than 0.");
        }

        var projects = await _timesheetProjectRepository.GetListByOrganizationIdAsync(
            organizationId,
            cancellationToken);

        return projects.Select(project => project.ToResponse()).ToList();
    }

    public async Task<TimesheetProjectResponse> SaveAsync(
        TimesheetProjectSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every rule is checked here, before anything is written, and every
        // failure is collected - the caller is told about all of them in
        // one 400, keyed on the field names it sent.
        var projectName = string.IsNullOrWhiteSpace(request.ProjectName) ? null : request.ProjectName.Trim();
        var failures = new Dictionary<string, string[]>();

        if (request.ProjectId < 0)
        {
            failures[nameof(TimesheetProjectSaveRequest.ProjectId)] = new[]
            {
                "ProjectId must be 0 (or left out) to add a project, or the id of the project to update."
            };
        }

        if (request.OrganizationId <= 0)
        {
            failures[nameof(TimesheetProjectSaveRequest.OrganizationId)] = new[]
            {
                "OrganizationId is required and must be greater than 0."
            };
        }

        if (projectName?.Length > ProjectNameMaximumLength)
        {
            failures[nameof(TimesheetProjectSaveRequest.ProjectName)] = new[]
            {
                $"ProjectName must be {ProjectNameMaximumLength} characters or fewer."
            };
        }

        if (request.TotalBudget < 0)
        {
            failures[nameof(TimesheetProjectSaveRequest.TotalBudget)] = new[]
            {
                "TotalBudget must not be negative."
            };
        }

        if (request.TotalHours < 0)
        {
            failures[nameof(TimesheetProjectSaveRequest.TotalHours)] = new[]
            {
                "TotalHours must not be negative."
            };
        }

        if (request.StartDate is not null && request.EndDate is not null && request.EndDate < request.StartDate)
        {
            failures[nameof(TimesheetProjectSaveRequest.EndDate)] = new[]
            {
                "EndDate must not be before StartDate."
            };
        }

        if (request.Status <= 0)
        {
            failures[nameof(TimesheetProjectSaveRequest.Status)] = new[]
            {
                "Status must be greater than 0 when sent."
            };
        }

        // Checked here rather than left to the column's CHECK constraint, which
        // would surface as a 500 instead of a 400 naming the field.
        if (request.SubmissionFrequency is { } submissionFrequency
            && !SubmissionFrequencies.Contains(submissionFrequency))
        {
            failures[nameof(TimesheetProjectSaveRequest.SubmissionFrequency)] = new[]
            {
                "SubmissionFrequency must be 1 (weekly), 2 (every 15 days) or 3 (monthly) when sent."
            };
        }

        if (request.ClientId <= 0)
        {
            failures[nameof(TimesheetProjectSaveRequest.ClientId)] = new[]
            {
                "ClientId is required and must be greater than 0."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Then the database: the column is NOT NULL with no foreign key, so
        // nothing else stops a project naming a client that is not there. The
        // project's currency is the client's, so the one lookup does both.
        var currencyCode = await _timesheetProjectRepository.GetClientCurrencyCodeAsync(request.ClientId, cancellationToken)
            ?? throw new ValidationException(
                nameof(TimesheetProjectSaveRequest.ClientId),
                $"Client '{request.ClientId}' does not exist or has been removed.");

        // Null and 0 both mean "add" - a negative id was refused above.
        var projectId = request.ProjectId is > 0 ? request.ProjectId : null;

        // Only an update can come back empty: the project it names does not
        // exist or belongs to another organisation.
        var saved = await _timesheetProjectRepository.SaveAsync(
                projectId,
                request.OrganizationId,
                projectName,
                request.TotalBudget,
                request.TotalHours,
                request.StartDate,
                request.EndDate,
                currencyCode,
                request.Status,
                request.ClientId,
                request.SubmissionFrequency,
                cancellationToken)
            ?? throw NotFoundException.For("Project", projectId ?? 0);

        _logger.LogInformation(
            "Project {ProjectId} {Action} for organization {OrganizationId}.",
            saved.ProjectId,
            projectId is null ? "added" : "updated",
            request.OrganizationId);

        return saved.ToResponse();
    }

    public async Task DeleteAsync(
        TimesheetProjectDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every rule is checked before anything is read, and every failure is
        // reported together.
        var failures = new Dictionary<string, string[]>();

        if (request.ProjectId <= 0)
        {
            failures[nameof(TimesheetProjectDeleteRequest.ProjectId)] = new[]
            {
                "ProjectId is required and must be greater than 0."
            };
        }

        if (request.OrganizationId <= 0)
        {
            failures[nameof(TimesheetProjectDeleteRequest.OrganizationId)] = new[]
            {
                "OrganizationId is required and must be greater than 0."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // The repository matches on the organisation too, so a project of
        // another organisation is a 404 - never removed.
        var project = await _timesheetProjectRepository.GetForUpdateAsync(
                request.ProjectId,
                request.OrganizationId,
                cancellationToken)
            ?? throw new NotFoundException(
                $"Project '{request.ProjectId}' was not found in organization '{request.OrganizationId}'.");

        // The project's budget goes with it - every row, duplicates included -
        // so no budget is left pointing at a project that no longer exists.
        // No foreign key does this for us.
        var budgets = await _timesheetProjectRepository.GetBudgetListForUpdateByProjectIdAsync(
            project.ProjectId,
            cancellationToken);

        // A hard delete: dbo.TimesheetProject has no soft-delete column, so
        // there is nothing to mark - the row goes, with its budget, together.
        await _timesheetProjectRepository.DeleteAsync(project, budgets, cancellationToken);

        _logger.LogInformation(
            "Project {ProjectId} of organization {OrganizationId} deleted, with {BudgetCount} budget row(s).",
            project.ProjectId,
            request.OrganizationId,
            budgets.Count);
    }

    public async Task<IReadOnlyList<TimesheetProjectBudgetResponse>> GetBudgetListByProjectIdAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        // Checked here rather than by a route constraint: a 0 is told what is
        // wrong with it instead of missing the route.
        if (projectId <= 0)
        {
            throw new ValidationException(
                "projectID",
                "projectID is required and must be greater than 0.");
        }

        var budgets = await _timesheetProjectRepository.GetBudgetListByProjectIdAsync(
            projectId,
            cancellationToken);

        return budgets.Select(budget => budget.ToResponse()).ToList();
    }

    public async Task<TimesheetProjectBudgetResponse> SaveBudgetAsync(
        TimesheetProjectBudgetSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // The shape of the payload first, every failure collected into one 400.
        var failures = new Dictionary<string, string[]>();

        if (request.ProjectId <= 0)
        {
            failures[nameof(TimesheetProjectBudgetSaveRequest.ProjectId)] = new[]
            {
                "ProjectId is required and must be greater than 0."
            };
        }

        if (request.ProjectCategoryId <= 0)
        {
            failures[nameof(TimesheetProjectBudgetSaveRequest.ProjectCategoryId)] = new[]
            {
                "ProjectCategoryId must be greater than 0 when sent."
            };
        }

        if (request.AllocatedHours < 0)
        {
            failures[nameof(TimesheetProjectBudgetSaveRequest.AllocatedHours)] = new[]
            {
                "AllocatedHours must not be negative."
            };
        }

        if (request.HourlyRate < 0)
        {
            failures[nameof(TimesheetProjectBudgetSaveRequest.HourlyRate)] = new[]
            {
                "HourlyRate must not be negative."
            };
        }

        // The columns are date, so only the date part is compared and stored.
        var periodStart = request.PeriodStart?.Date;
        var periodEnd = request.PeriodEnd?.Date;

        if (periodStart is not null && periodEnd is not null && periodEnd < periodStart)
        {
            failures[nameof(TimesheetProjectBudgetSaveRequest.PeriodEnd)] = new[]
            {
                "PeriodEnd must not be before PeriodStart."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Then the database: the project must exist, and a category must be a
        // live one of the project's own organisation.
        var project = await _timesheetProjectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw NotFoundException.For("Project", request.ProjectId);

        if (request.ProjectCategoryId is int projectCategoryId
            && (project.OrganizationId is not int organizationId
                || !await _timesheetProjectRepository.ProjectCategoryExistsAsync(
                    projectCategoryId,
                    organizationId,
                    cancellationToken)))
        {
            throw new ValidationException(
                nameof(TimesheetProjectBudgetSaveRequest.ProjectCategoryId),
                $"Project category '{projectCategoryId}' is not a category of this project's organization.");
        }

        // Worked out, never sent: the amount is the hours at the rate, rounded
        // to the column's two decimal places. Either one missing means no amount.
        decimal? allocatedAmount = request.AllocatedHours is decimal hours && request.HourlyRate is decimal rate
            ? Math.Round(hours * rate, 2, MidpointRounding.AwayFromZero)
            : null;

        // A project has one budget, so there is no budget id to decide with:
        // the procedure updates the project's budget if it has one and adds it
        // if not, under a lock so two saves cannot both add. It always returns
        // the saved row; empty would mean the project vanished since the check
        // above.
        var saved = await _timesheetProjectRepository.SaveBudgetAsync(
                request.ProjectId,
                request.ProjectCategoryId,
                request.AllocatedHours,
                request.HourlyRate,
                allocatedAmount,
                periodStart,
                periodEnd,
                cancellationToken)
            ?? throw NotFoundException.For("Project", request.ProjectId);

        _logger.LogInformation(
            "Budget {BudgetId} saved for project {ProjectId}.",
            saved.BudgetId,
            request.ProjectId);

        return saved.ToResponse();
    }

    public async Task DeleteBudgetAsync(
        TimesheetProjectBudgetDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BudgetId <= 0)
        {
            throw new ValidationException(
                nameof(TimesheetProjectBudgetDeleteRequest.BudgetId),
                "BudgetId is required and must be greater than 0.");
        }

        var budget = await _timesheetProjectRepository.GetBudgetForUpdateAsync(
                request.BudgetId,
                cancellationToken)
            ?? throw NotFoundException.For("Budget", request.BudgetId);

        // A hard delete: the table has no soft-delete column.
        await _timesheetProjectRepository.DeleteBudgetAsync(budget, cancellationToken);

        _logger.LogInformation(
            "Budget {BudgetId} of project {ProjectId} deleted.",
            budget.BudgetId,
            budget.ProjectId);
    }

    public async Task<IReadOnlyList<TimesheetProjectStatusResponse>> GetStatusListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        // Checked here rather than by a route constraint: a 0 is told what is
        // wrong with it instead of missing the route.
        if (organizationId <= 0)
        {
            throw new ValidationException(
                "orgID",
                "orgID is required and must be greater than 0.");
        }

        var statuses = await _timesheetProjectRepository.GetStatusListByOrganizationIdAsync(
            organizationId,
            cancellationToken);

        return statuses.Select(status => status.ToResponse()).ToList();
    }

    public async Task<TimesheetProjectStatusResponse> SaveStatusAsync(
        TimesheetProjectStatusSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every rule is checked here, before anything is written, and every
        // failure is collected - the caller is told about all of them in one
        // 400, keyed on the field names it sent.
        var statusName = request.Status?.Trim();
        var colorCode = string.IsNullOrWhiteSpace(request.ColorCode) ? null : request.ColorCode.Trim();
        var failures = new Dictionary<string, string[]>();

        if (request.StatusId < 0)
        {
            failures[nameof(TimesheetProjectStatusSaveRequest.StatusId)] = new[]
            {
                "StatusId must be 0 (or left out) to add a status, or the id of the status to edit."
            };
        }

        if (request.OrganizationId <= 0)
        {
            failures[nameof(TimesheetProjectStatusSaveRequest.OrganizationId)] = new[]
            {
                "OrganizationId is required and must be greater than 0."
            };
        }

        if (string.IsNullOrEmpty(statusName))
        {
            failures[nameof(TimesheetProjectStatusSaveRequest.Status)] = new[] { "Status is required." };
        }
        else if (statusName.Length > StatusNameMaximumLength)
        {
            failures[nameof(TimesheetProjectStatusSaveRequest.Status)] = new[]
            {
                $"Status must be {StatusNameMaximumLength} characters or fewer."
            };
        }

        if (colorCode?.Length > ColorCodeMaximumLength)
        {
            failures[nameof(TimesheetProjectStatusSaveRequest.ColorCode)] = new[]
            {
                $"ColorCode must be {ColorCodeMaximumLength} characters or fewer."
            };
        }

        if (request.UserId <= 0)
        {
            failures[nameof(TimesheetProjectStatusSaveRequest.UserId)] = new[]
            {
                "UserId is required: the row records who created or last updated it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Null and 0 both mean "add" - a negative id was refused above.
        var statusId = request.StatusId is > 0 ? request.StatusId : null;

        // Only an edit can come back empty: the status it names does not
        // exist, is deleted, or belongs to another organisation.
        var saved = await _timesheetProjectRepository.SaveStatusAsync(
                statusId,
                request.OrganizationId,
                statusName!,
                colorCode,
                request.UserId,
                _dateTimeProvider.UtcNow,
                cancellationToken)
            ?? throw NotFoundException.For("Project status", statusId ?? 0);

        _logger.LogInformation(
            "Project status {StatusId} {Action} for organization {OrganizationId} by user {UserId}.",
            saved.StatusId,
            statusId is null ? "added" : "updated",
            request.OrganizationId,
            request.UserId);

        return saved.ToResponse();
    }

    public async Task DeleteStatusAsync(
        TimesheetProjectStatusDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every rule is checked before anything is read, and every failure is
        // reported together.
        var failures = new Dictionary<string, string[]>();

        if (request.StatusId <= 0)
        {
            failures[nameof(TimesheetProjectStatusDeleteRequest.StatusId)] = new[]
            {
                "StatusId is required and must be greater than 0."
            };
        }

        if (request.DeletedBy <= 0)
        {
            failures[nameof(TimesheetProjectStatusDeleteRequest.DeletedBy)] = new[]
            {
                "DeletedBy is required: a soft delete records who performed it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // The repository hides deleted statuses, so one deleted already is a
        // 404 - never re-stamped over the first DeletedDate.
        var status = await _timesheetProjectRepository.GetStatusForUpdateAsync(request.StatusId, cancellationToken)
            ?? throw NotFoundException.For("Project status", request.StatusId);

        // A soft delete, never a DELETE statement: the row stays, and the
        // entity's query filter and both status procedures stop returning it.
        status.DeletedDate = _dateTimeProvider.UtcNow;
        status.DeletedBy = request.DeletedBy;

        await _timesheetProjectRepository.UpdateStatusAsync(status, cancellationToken);

        _logger.LogInformation(
            "Project status {StatusId} of organization {OrganizationId} soft-deleted by user {DeletedBy}.",
            status.StatusId,
            status.OrganizationId,
            request.DeletedBy);
    }

    public async Task<IReadOnlyList<TimesheetProjectBudgetByOrgResponse>> GetBudgetListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        // Checked here rather than by a route constraint: a 0 is told what is
        // wrong with it instead of missing the route.
        if (organizationId <= 0)
        {
            throw new ValidationException(
                "orgID",
                "orgID is required and must be greater than 0.");
        }

        var budgets = await _timesheetProjectRepository.GetBudgetListByOrganizationIdAsync(
            organizationId,
            cancellationToken);

        return budgets.Select(budget => budget.ToResponse()).ToList();
    }

    public async Task<TimesheetUserCreateResponse> CreateTimesheetUserAsync(
        TimesheetUserCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every shape rule first, every failure collected - the caller is told
        // about all of them in one 400, keyed on the field names it sent.
        var fullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        var latitude = string.IsNullOrWhiteSpace(request.ProjectLocationLatitude) ? null : request.ProjectLocationLatitude.Trim();
        var failures = new Dictionary<string, string[]>();

        AddFullNameFailure(failures, nameof(TimesheetUserCreateRequest.FullName), fullName);
        AddEmailFailure(failures, nameof(TimesheetUserCreateRequest.Email), email);

        if (request.CountryId <= 0)
        {
            failures[nameof(TimesheetUserCreateRequest.CountryId)] = new[]
            {
                "CountryId must be greater than 0 when sent."
            };
        }

        if (request.OrganizationId <= 0)
        {
            failures[nameof(TimesheetUserCreateRequest.OrganizationId)] = new[]
            {
                "OrganizationId is required and must be greater than 0."
            };
        }

        if (request.ProjectId <= 0)
        {
            failures[nameof(TimesheetUserCreateRequest.ProjectId)] = new[]
            {
                "ProjectId is required and must be greater than 0."
            };
        }

        if (request.ProjectRoleId <= 0)
        {
            failures[nameof(TimesheetUserCreateRequest.ProjectRoleId)] = new[]
            {
                "ProjectRoleId must be greater than 0 when sent."
            };
        }

        if (request.JobType <= 0)
        {
            failures[nameof(TimesheetUserCreateRequest.JobType)] = new[]
            {
                "JobType must be greater than 0 when sent."
            };
        }

        AddRateFailure(failures, nameof(TimesheetUserCreateRequest.CostRate), request.CostRate);
        AddRateFailure(failures, nameof(TimesheetUserCreateRequest.BillRate), request.BillRate);

        if (request.WorkLocationRange < 0)
        {
            failures[nameof(TimesheetUserCreateRequest.WorkLocationRange)] = new[]
            {
                "WorkLocationRange must not be negative."
            };
        }

        AddMaxLogHoursPerDayFailure(
            failures,
            nameof(TimesheetUserCreateRequest.MaxLogHoursPerDay),
            request.MaxLogHoursPerDay);

        AddCoordinateFailure(failures, nameof(TimesheetUserCreateRequest.ProjectLocationLatitude), latitude, 90m);

        if (request.CreatedBy <= 0)
        {
            failures[nameof(TimesheetUserCreateRequest.CreatedBy)] = new[]
            {
                "CreatedBy is required: the assignment records who created it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Then the database. No foreign key protects any of these columns, and
        // the procedure checks none of them, so a wrong id would be saved as is.
        if (!await _timesheetProjectRepository.OrganizationExistsAsync(request.OrganizationId, cancellationToken))
        {
            failures[nameof(TimesheetUserCreateRequest.OrganizationId)] = new[]
            {
                $"Organization '{request.OrganizationId}' does not exist."
            };
        }

        if (request.CountryId is { } countryId
            && !await _timesheetProjectRepository.CountryExistsAsync(countryId, cancellationToken))
        {
            failures[nameof(TimesheetUserCreateRequest.CountryId)] = new[]
            {
                $"Country '{countryId}' does not exist."
            };
        }

        var project = await _timesheetProjectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project is null || project.OrganizationId != request.OrganizationId)
        {
            failures[nameof(TimesheetUserCreateRequest.ProjectId)] = new[]
            {
                $"Project '{request.ProjectId}' does not exist in organization '{request.OrganizationId}'."
            };
        }

        if (request.ProjectRoleId is { } projectRoleId
            && !await _timesheetProjectRepository.UserRoleExistsAsync(projectRoleId, request.OrganizationId, cancellationToken))
        {
            failures[nameof(TimesheetUserCreateRequest.ProjectRoleId)] = new[]
            {
                $"Role '{projectRoleId}' does not exist in organization '{request.OrganizationId}' or has been removed."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // The procedure matches an existing user by email whatever their state
        // or organisation, and would put them on this project regardless. Only
        // a live user of this organisation may be assigned that way.
        var existingUser = await _timesheetProjectRepository.GetUserByEmailAsync(email!, cancellationToken);

        if (existingUser is not null)
        {
            if (existingUser.IsDelete != 0)
            {
                throw new ConflictException($"The user with email '{email}' has been removed.");
            }

            if (existingUser.OrganizationId != request.OrganizationId)
            {
                throw new ConflictException($"The user with email '{email}' belongs to another organization.");
            }
        }

        await _timesheetProjectRepository.CreateTimesheetUserAsync(
            fullName!,
            email!,
            request.CountryId,
            address,
            request.OrganizationId,
            request.ProjectRoleId,
            request.JobType,
            request.CostRate!.Value,
            request.BillRate!.Value,
            request.CreatedBy,
            request.WorkLocationRange,
            request.ProjectId,
            latitude,
            request.MaxLogHoursPerDay,
            cancellationToken);

        // The procedure returns nothing, so the user is read back.
        var user = await _timesheetProjectRepository.GetUserByEmailAsync(email!, cancellationToken)
            ?? throw new InvalidOperationException(
                "spc_Signup_timesheet_user completed without error but the user cannot be found.");

        _logger.LogInformation(
            "User {UserId} put on project {ProjectId} of organization {OrganizationId} by user {CreatedBy} ({Outcome}).",
            user.UserId,
            request.ProjectId,
            request.OrganizationId,
            request.CreatedBy,
            existingUser is null ? "new user" : "existing user");

        // Only a new account is welcomed; an existing user was only put on
        // another project. Sent to the address as the caller typed it - the
        // stored one is upper case.
        var welcomeEmailSent = existingUser is null
            && await SendWelcomeEmailAsync(user.UserId, user.Name, email!);

        return new TimesheetUserCreateResponse
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            OrganizationId = user.OrganizationId,
            ProjectId = request.ProjectId,
            IsNewUser = existingUser is null,
            WelcomeEmailSent = welcomeEmailSent,
        };
    }

    /// <summary>
    /// Sends the welcome email to a new account, directly through the email
    /// service - nothing is queued or stored. Never throws: the account exists
    /// by now, and failing the request would invite a retry that, finding the
    /// email taken, only adds a second project assignment. A failure is logged
    /// and reported as <see langword="false"/>.
    /// </summary>
    private async Task<bool> SendWelcomeEmailAsync(int userId, string userName, string emailAddress)
    {
        try
        {
            var body = await _emailTemplateStore.GetAsync(WelcomeEmailTemplateName, CancellationToken.None);

            // Every value is HTML-encoded: a name with a '<' in it is text, not markup.
            var values = new Dictionary<string, string>
            {
                ["UserName"] = userName,
                ["NexoraLoginURL"] = _welcomeEmailSettings.LoginUrl,
                ["SupportEmail"] = _welcomeEmailSettings.SupportEmail,
            };

            foreach (var (placeholder, value) in values)
            {
                body = body.Replace($"{{{{{placeholder}}}}}", WebUtility.HtmlEncode(value), StringComparison.Ordinal);
            }

            // CancellationToken.None: the account is already created, so a
            // caller that disconnects now must not cost the user their email.
            // The email service's own timeout still bounds the wait.
            var result = await _emailSender.SendAsync(
                new EmailMessage(emailAddress, _welcomeEmailSettings.Subject, body),
                CancellationToken.None);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Welcome email to new user {UserId} was not sent: {Error}",
                    userId,
                    result.Error);
            }

            return result.Succeeded;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Welcome email to new user {UserId} could not be built or sent.", userId);
            return false;
        }
    }

    public async Task<TimesheetUserResponse> UpdateTimesheetUserAsync(
        TimesheetUserUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every shape rule first, every failure collected - the caller is told
        // about all of them in one 400, keyed on the field names it sent.
        var fullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var latitude = string.IsNullOrWhiteSpace(request.ProjectLocationLatitude) ? null : request.ProjectLocationLatitude.Trim();
        var longitude = string.IsNullOrWhiteSpace(request.ProjectLocationLongitude) ? null : request.ProjectLocationLongitude.Trim();
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetUserProjectSetupId <= 0)
        {
            failures[nameof(TimesheetUserUpdateRequest.TimesheetUserProjectSetupId)] = new[]
            {
                "TimesheetUserProjectSetupId is required and must be greater than 0."
            };
        }

        if (request.OrganizationId <= 0)
        {
            failures[nameof(TimesheetUserUpdateRequest.OrganizationId)] = new[]
            {
                "OrganizationId is required and must be greater than 0."
            };
        }

        AddFullNameFailure(failures, nameof(TimesheetUserUpdateRequest.FullName), fullName);
        AddEmailFailure(failures, nameof(TimesheetUserUpdateRequest.Email), email);

        if (request.ProjectId <= 0)
        {
            failures[nameof(TimesheetUserUpdateRequest.ProjectId)] = new[]
            {
                "ProjectId is required and must be greater than 0."
            };
        }

        if (request.ProjectRoleId <= 0)
        {
            failures[nameof(TimesheetUserUpdateRequest.ProjectRoleId)] = new[]
            {
                "ProjectRoleId must be greater than 0 when sent."
            };
        }

        if (request.JobType <= 0)
        {
            failures[nameof(TimesheetUserUpdateRequest.JobType)] = new[]
            {
                "JobType must be greater than 0 when sent."
            };
        }

        AddRateFailure(failures, nameof(TimesheetUserUpdateRequest.CostRate), request.CostRate);
        AddRateFailure(failures, nameof(TimesheetUserUpdateRequest.BillRate), request.BillRate);

        if (request.WorkLocationRange < 0)
        {
            failures[nameof(TimesheetUserUpdateRequest.WorkLocationRange)] = new[]
            {
                "WorkLocationRange must not be negative."
            };
        }

        AddMaxLogHoursPerDayFailure(
            failures,
            nameof(TimesheetUserUpdateRequest.MaxLogHoursPerDay),
            request.MaxLogHoursPerDay);

        AddCoordinateFailure(failures, nameof(TimesheetUserUpdateRequest.ProjectLocationLatitude), latitude, 90m);
        AddCoordinateFailure(failures, nameof(TimesheetUserUpdateRequest.ProjectLocationLongitude), longitude, 180m);

        if (request.UpdatedBy <= 0)
        {
            failures[nameof(TimesheetUserUpdateRequest.UpdatedBy)] = new[]
            {
                "UpdatedBy is required: an edit records who made it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // The assignment, and its user, must exist, be live, and belong to the
        // organisation - otherwise it is a 404, as for another organisation's
        // project. The query filter hides a deleted assignment.
        var setup = await _timesheetProjectRepository.GetUserProjectSetupForUpdateAsync(
                request.TimesheetUserProjectSetupId,
                cancellationToken)
            ?? throw NotFoundException.For("Timesheet user", request.TimesheetUserProjectSetupId);

        var user = await _timesheetProjectRepository.GetUserForUpdateAsync(setup.UserId, cancellationToken);

        if (user is null || user.IsDelete != 0 || user.OrganizationId != request.OrganizationId)
        {
            throw NotFoundException.For("Timesheet user", request.TimesheetUserProjectSetupId);
        }

        // Then what the payload names. No foreign key protects these columns.
        var project = await _timesheetProjectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project is null || project.OrganizationId != request.OrganizationId)
        {
            failures[nameof(TimesheetUserUpdateRequest.ProjectId)] = new[]
            {
                $"Project '{request.ProjectId}' does not exist in organization '{request.OrganizationId}'."
            };
        }

        if (request.ProjectRoleId is { } projectRoleId
            && !await _timesheetProjectRepository.UserRoleExistsAsync(projectRoleId, request.OrganizationId, cancellationToken))
        {
            failures[nameof(TimesheetUserUpdateRequest.ProjectRoleId)] = new[]
            {
                $"Role '{projectRoleId}' does not exist in organization '{request.OrganizationId}' or has been removed."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        if (await _timesheetProjectRepository.UserProjectSetupExistsAsync(
                setup.UserId,
                request.ProjectId,
                setup.TimesheetUserProjectSetupId,
                cancellationToken))
        {
            throw new ConflictException(
                $"User '{setup.UserId}' is already on project '{request.ProjectId}' through another assignment.");
        }

        // dbo.Signup is touched only when the name or email actually changes.
        // The email is compared - and stored - as spc_Signup_timesheet_user
        // does: upper case, without surrounding spaces.
        var normalizedEmail = email!.ToUpperInvariant();
        var emailChanged = !string.Equals(user.Email.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase);
        var nameChanged = !string.Equals(user.Name, fullName, StringComparison.Ordinal);

        if (emailChanged)
        {
            // Any other user, removed or not: the procedure matches an email
            // whatever the user's state, so two rows must never share one.
            var emailOwner = await _timesheetProjectRepository.GetUserByEmailAsync(normalizedEmail, cancellationToken);

            if (emailOwner is not null && emailOwner.UserId != user.UserId)
            {
                throw new ConflictException($"The email '{email}' is already used by another user.");
            }

            user.Email = normalizedEmail;
        }

        if (nameChanged)
        {
            user.Name = fullName!;
        }

        if (emailChanged || nameChanged)
        {
            user.UpdatedDate = _dateTimeProvider.UtcNow;
            user.UpdateBy = request.UpdatedBy;
        }

        // Everything else is the assignment's, and is overwritten as sent.
        setup.ProjectId = request.ProjectId;
        setup.ProjectRoleId = request.ProjectRoleId;
        setup.JobType = request.JobType;
        setup.CostRate = request.CostRate;
        setup.BillRate = request.BillRate;
        setup.WorkLocationRange = request.WorkLocationRange;
        setup.ProjectLocationLatitude = latitude;
        setup.ProjectLocationLongitude = longitude;
        setup.MaxLogHoursPerDay = request.MaxLogHoursPerDay;

        await _timesheetProjectRepository.UpdateTimesheetUserAsync(setup, cancellationToken);

        _logger.LogInformation(
            "Timesheet user assignment {TimesheetUserProjectSetupId} of user {UserId} edited by user {UpdatedBy}; dbo.Signup {SignupOutcome}.",
            setup.TimesheetUserProjectSetupId,
            user.UserId,
            request.UpdatedBy,
            emailChanged || nameChanged ? "updated" : "unchanged");

        return new TimesheetUserResponse
        {
            TimesheetUserProjectSetupId = setup.TimesheetUserProjectSetupId,
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            OrganizationId = user.OrganizationId,
            ProjectId = setup.ProjectId,
            ProjectRoleId = setup.ProjectRoleId,
            JobType = setup.JobType,
            CostRate = setup.CostRate,
            BillRate = setup.BillRate,
            WorkLocationRange = setup.WorkLocationRange,
            ProjectLocationLatitude = setup.ProjectLocationLatitude,
            ProjectLocationLongitude = setup.ProjectLocationLongitude,
            MaxLogHoursPerDay = setup.MaxLogHoursPerDay,
        };
    }

    public async Task DeleteTimesheetUserAsync(
        TimesheetUserDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (request.UserId <= 0)
        {
            failures[nameof(TimesheetUserDeleteRequest.UserId)] = new[]
            {
                "UserId is required and must be greater than 0."
            };
        }

        if (request.ProjectId <= 0)
        {
            failures[nameof(TimesheetUserDeleteRequest.ProjectId)] = new[]
            {
                "ProjectId is required and must be greater than 0."
            };
        }

        if (request.DeletedBy <= 0)
        {
            failures[nameof(TimesheetUserDeleteRequest.DeletedBy)] = new[]
            {
                "DeletedBy is required: a soft delete records who performed it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Every live assignment of the user to the project - normally one;
        // create-timesheet-user does not stop duplicates, and leaving one
        // behind would leave the user on the project.
        var setups = await _timesheetProjectRepository.GetUserProjectSetupListForUpdateAsync(
            request.UserId,
            request.ProjectId,
            cancellationToken);

        // The repository hides deleted assignments, so a user removed already
        // is a 404 - never re-stamped over the first DeletedDate.
        if (setups.Count == 0)
        {
            throw new NotFoundException(
                $"User '{request.UserId}' is not on project '{request.ProjectId}'.");
        }

        // A soft delete, never a DELETE statement: the rows stay, and the
        // entity's query filter stops returning them.
        var now = _dateTimeProvider.UtcNow;

        foreach (var setup in setups)
        {
            setup.IsDelete = 1;
            setup.DeletedDate = now;
            setup.DeletedBy = request.DeletedBy;
        }

        await _timesheetProjectRepository.UpdateTimesheetUsersAsync(setups, cancellationToken);

        _logger.LogInformation(
            "User {UserId} taken off project {ProjectId} by user {DeletedBy} ({Count} assignment row(s) soft-deleted).",
            request.UserId,
            request.ProjectId,
            request.DeletedBy,
            setups.Count);
    }

    private static void AddFullNameFailure(Dictionary<string, string[]> failures, string field, string? fullName)
    {
        if (fullName is null)
        {
            failures[field] = new[] { $"{field} is required." };
        }
        else if (fullName.Length > FullNameMaximumLength)
        {
            failures[field] = new[] { $"{field} must be {FullNameMaximumLength} characters or fewer." };
        }
    }

    private static void AddEmailFailure(Dictionary<string, string[]> failures, string field, string? email)
    {
        if (email is null)
        {
            failures[field] = new[] { $"{field} is required." };
        }
        else if (email.Length > EmailMaximumLength)
        {
            failures[field] = new[] { $"{field} must be {EmailMaximumLength} characters or fewer." };
        }
        else if (!IsEmailAddress(email))
        {
            failures[field] = new[] { $"{field} must be a valid email address." };
        }
    }

    /// <summary>A latitude (<paramref name="limit"/> 90) or longitude (180), sent as text and stored as text.</summary>
    private static void AddCoordinateFailure(Dictionary<string, string[]> failures, string field, string? coordinate, decimal limit)
    {
        if (coordinate is null)
        {
            return;
        }

        if (coordinate.Length > CoordinateMaximumLength)
        {
            failures[field] = new[] { $"{field} must be {CoordinateMaximumLength} characters or fewer." };
        }
        else if (!decimal.TryParse(coordinate, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                 || parsed < -limit
                 || parsed > limit)
        {
            failures[field] = new[] { $"{field} must be a number from -{limit} to {limit}." };
        }
    }

    private static void AddRateFailure(Dictionary<string, string[]> failures, string field, decimal? rate)
    {
        if (rate is null)
        {
            failures[field] = new[] { $"{field} is required." };
        }
        else if (rate < 0)
        {
            failures[field] = new[] { $"{field} must not be negative." };
        }
        else if (rate > RateMaximum)
        {
            failures[field] = new[] { $"{field} must not be greater than {RateMaximum}." };
        }
    }

    /// <summary>
    /// <c>Max_log_hour_day</c> is a whole number of hours and optional: left
    /// out, the assignment sets no cap. Negative is refused, and so is a value
    /// no day could reach.
    /// </summary>
    private static void AddMaxLogHoursPerDayFailure(
        Dictionary<string, string[]> failures,
        string field,
        int? maxLogHoursPerDay)
    {
        if (maxLogHoursPerDay is not { } hours)
        {
            return;
        }

        if (hours < 0)
        {
            failures[field] = new[] { $"{field} must not be negative." };
        }
        else if (hours > MaxLogHoursPerDayMaximum)
        {
            failures[field] = new[]
            {
                $"{field} must not be greater than {MaxLogHoursPerDayMaximum}: a day holds no more hours."
            };
        }
    }

    private static bool IsEmailAddress(string email) =>
        // Address == email rejects what MailAddress quietly accepts, such as a
        // display name ("Priya <priya@example.com>").
        MailAddress.TryCreate(email, out var address) && address.Address == email;

    public async Task<IReadOnlyList<TimesheetUserListResponse>> GetTimesheetUserListByOrganizationIdAsync(
        int organizationId,
        CancellationToken cancellationToken = default)
    {
        // Checked here rather than by a route constraint: a 0 is told what is
        // wrong with it instead of missing the route.
        if (organizationId <= 0)
        {
            throw new ValidationException(
                "orgID",
                "orgID is required and must be greater than 0.");
        }

        var users = await _timesheetProjectRepository.GetTimesheetUserListByOrganizationIdAsync(
            organizationId,
            cancellationToken);

        return users.Select(user => user.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<TimesheetHolidaySetupResponse>> GetHolidaySetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (organizationId < 0)
        {
            failures["orgID"] = new[] { "orgID must be greater than 0, or left out." };
        }

        if (projectId < 0)
        {
            failures["projectID"] = new[] { "projectID must be greater than 0, or left out." };
        }

        // Null and 0 both mean "not filtered". With neither filter the whole
        // table would come back, across every organisation - refused.
        var organizationFilter = organizationId is > 0 ? organizationId : null;
        var projectFilter = projectId is > 0 ? projectId : null;

        if (failures.Count == 0 && organizationFilter is null && projectFilter is null)
        {
            failures["orgID"] = new[] { "orgID or projectID is required." };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var holidays = await _timesheetProjectRepository.GetHolidaySetupListAsync(
            organizationFilter,
            projectFilter,
            cancellationToken);

        return holidays.Select(holiday => holiday.ToResponse()).ToList();
    }

    public async Task<TimesheetHolidaySetupResponse> SaveHolidaySetupAsync(
        TimesheetHolidaySetupSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every rule is checked here, before anything is written, and every
        // failure is collected - the caller is told about all of them in one
        // 400, keyed on the field names it sent.
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetHolidaySetupId < 0)
        {
            failures[nameof(TimesheetHolidaySetupSaveRequest.TimesheetHolidaySetupId)] = new[]
            {
                "TimesheetHolidaySetupId must be 0 (or left out) to add a holiday, or the id of the holiday to edit."
            };
        }

        if (request.HolidayDate is null)
        {
            failures[nameof(TimesheetHolidaySetupSaveRequest.HolidayDate)] = new[] { "HolidayDate is required." };
        }

        if (description?.Length > HolidayDescriptionMaximumLength)
        {
            failures[nameof(TimesheetHolidaySetupSaveRequest.Description)] = new[]
            {
                $"Description must be {HolidayDescriptionMaximumLength} characters or fewer."
            };
        }

        if (request.OrganizationId <= 0)
        {
            failures[nameof(TimesheetHolidaySetupSaveRequest.OrganizationId)] = new[]
            {
                "OrganizationId is required and must be greater than 0."
            };
        }

        if (request.CountryId <= 0)
        {
            failures[nameof(TimesheetHolidaySetupSaveRequest.CountryId)] = new[]
            {
                "CountryId must be greater than 0, or left out."
            };
        }

        if (request.ClientId <= 0)
        {
            failures[nameof(TimesheetHolidaySetupSaveRequest.ClientId)] = new[]
            {
                "ClientId must be greater than 0, or left out."
            };
        }

        if (request.ProjectId <= 0)
        {
            failures[nameof(TimesheetHolidaySetupSaveRequest.ProjectId)] = new[]
            {
                "ProjectId must be greater than 0, or left out."
            };
        }

        if (request.CreatedBy <= 0)
        {
            failures[nameof(TimesheetHolidaySetupSaveRequest.CreatedBy)] = new[]
            {
                "CreatedBy is required: the row records who created it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Then the database. No foreign key protects any of these columns, and
        // the procedure checks none of them, so a wrong id would be saved as is.
        if (request.CountryId is { } countryId
            && !await _timesheetProjectRepository.CountryExistsAsync(countryId, cancellationToken))
        {
            failures[nameof(TimesheetHolidaySetupSaveRequest.CountryId)] = new[]
            {
                $"Country '{countryId}' does not exist."
            };
        }

        if (request.ClientId is { } clientId
            && !await _timesheetProjectRepository.ClientExistsAsync(clientId, request.OrganizationId, cancellationToken))
        {
            failures[nameof(TimesheetHolidaySetupSaveRequest.ClientId)] = new[]
            {
                $"Client '{clientId}' does not exist in organization '{request.OrganizationId}' or has been removed."
            };
        }

        if (request.ProjectId is { } projectId)
        {
            var project = await _timesheetProjectRepository.GetByIdAsync(projectId, cancellationToken);

            if (project is null || project.OrganizationId != request.OrganizationId)
            {
                failures[nameof(TimesheetHolidaySetupSaveRequest.ProjectId)] = new[]
                {
                    $"Project '{projectId}' does not exist in organization '{request.OrganizationId}'."
                };
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Null and 0 both mean "add" - a negative id was refused above.
        var timesheetHolidaySetupId = request.TimesheetHolidaySetupId is > 0 ? request.TimesheetHolidaySetupId : null;

        // Only an edit can come back empty: the holiday it names does not
        // exist, is deleted, or belongs to another organisation.
        var saved = await _timesheetProjectRepository.SaveHolidaySetupAsync(
                timesheetHolidaySetupId,
                request.HolidayDate!.Value.Date,
                description,
                request.OrganizationId,
                request.CountryId,
                request.ClientId,
                request.ProjectId,
                request.CreatedBy,
                _dateTimeProvider.UtcNow,
                cancellationToken)
            ?? throw NotFoundException.For("Holiday", timesheetHolidaySetupId ?? 0);

        _logger.LogInformation(
            "Holiday {TimesheetHolidaySetupId} {Action} for organization {OrganizationId} by user {UserId}.",
            saved.TimesheetHolidaySetupId,
            timesheetHolidaySetupId is null ? "added" : "updated",
            request.OrganizationId,
            request.CreatedBy);

        return saved.ToResponse();
    }

    public async Task DeleteHolidaySetupAsync(
        TimesheetHolidaySetupDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetHolidaySetupId <= 0)
        {
            failures[nameof(TimesheetHolidaySetupDeleteRequest.TimesheetHolidaySetupId)] = new[]
            {
                "TimesheetHolidaySetupId is required and must be greater than 0."
            };
        }

        if (request.OrganizationId <= 0)
        {
            failures[nameof(TimesheetHolidaySetupDeleteRequest.OrganizationId)] = new[]
            {
                "OrganizationId is required and must be greater than 0."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // The repository hides deleted holidays, so one deleted already is a
        // 404 rather than a silent success.
        var holiday = await _timesheetProjectRepository.GetHolidaySetupForUpdateAsync(
                request.TimesheetHolidaySetupId,
                request.OrganizationId,
                cancellationToken)
            ?? throw NotFoundException.For("Holiday", request.TimesheetHolidaySetupId);

        // A soft delete, never a DELETE statement: the row stays, and the
        // entity's query filter and both holiday procedures stop returning it.
        // The table has no delete date or "deleted by" column to stamp.
        holiday.IsDelete = true;

        await _timesheetProjectRepository.UpdateHolidaySetupAsync(holiday, cancellationToken);

        _logger.LogInformation(
            "Holiday {TimesheetHolidaySetupId} of organization {OrganizationId} soft-deleted.",
            holiday.TimesheetHolidaySetupId,
            holiday.OrganizationId);
    }

    public async Task<IReadOnlyList<TimesheetWeekendSetupResponse>> GetWeekendSetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (organizationId < 0)
        {
            failures["orgID"] = new[] { "orgID must be greater than 0, or left out." };
        }

        if (projectId < 0)
        {
            failures["projectID"] = new[] { "projectID must be greater than 0, or left out." };
        }

        // Null and 0 both mean "not filtered". With neither filter the whole
        // table would come back, across every organisation - refused.
        var organizationFilter = organizationId is > 0 ? organizationId : null;
        var projectFilter = projectId is > 0 ? projectId : null;

        if (failures.Count == 0 && organizationFilter is null && projectFilter is null)
        {
            failures["orgID"] = new[] { "orgID or projectID is required." };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var setups = await _timesheetProjectRepository.GetWeekendSetupListAsync(
            organizationFilter,
            projectFilter,
            cancellationToken);

        return setups.Select(setup => setup.ToResponse()).ToList();
    }

    public async Task<TimesheetWeekendSetupResponse> SaveWeekendSetupAsync(
        TimesheetWeekendSetupSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every rule is checked here, before anything is written, and every
        // failure is collected - the caller is told about all of them in one
        // 400, keyed on the field names it sent.
        var failures = new Dictionary<string, string[]>();

        if (request.TimeSheetWeekendSetupId < 0)
        {
            failures[nameof(TimesheetWeekendSetupSaveRequest.TimeSheetWeekendSetupId)] = new[]
            {
                "TimeSheetWeekendSetupId must be 0 (or left out) to add a setup, or the id of the setup to edit."
            };
        }

        if (request.OrganizationId <= 0)
        {
            failures[nameof(TimesheetWeekendSetupSaveRequest.OrganizationId)] = new[]
            {
                "OrganizationId is required and must be greater than 0."
            };
        }

        if (request.ClientId <= 0)
        {
            failures[nameof(TimesheetWeekendSetupSaveRequest.ClientId)] = new[]
            {
                "ClientId must be greater than 0, or left out."
            };
        }

        if (request.ProjectId <= 0)
        {
            failures[nameof(TimesheetWeekendSetupSaveRequest.ProjectId)] = new[]
            {
                "ProjectId is required and must be greater than 0."
            };
        }

        if (request.UserId <= 0)
        {
            failures[nameof(TimesheetWeekendSetupSaveRequest.UserId)] = new[]
            {
                "UserId is required: the row records who created or last updated it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Then the database. No foreign key protects any of these columns, and
        // the procedure checks none of them, so a wrong id would be saved as is.
        if (request.ClientId is { } clientId
            && !await _timesheetProjectRepository.ClientExistsAsync(clientId, request.OrganizationId, cancellationToken))
        {
            failures[nameof(TimesheetWeekendSetupSaveRequest.ClientId)] = new[]
            {
                $"Client '{clientId}' does not exist in organization '{request.OrganizationId}' or has been removed."
            };
        }

        var project = await _timesheetProjectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project is null || project.OrganizationId != request.OrganizationId)
        {
            failures[nameof(TimesheetWeekendSetupSaveRequest.ProjectId)] = new[]
            {
                $"Project '{request.ProjectId}' does not exist in organization '{request.OrganizationId}'."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Null and 0 both mean "add" - a negative id was refused above.
        var timeSheetWeekendSetupId = request.TimeSheetWeekendSetupId is > 0 ? request.TimeSheetWeekendSetupId : null;

        // One setup per project. The setup being edited is excluded, so an
        // edit that keeps its project is not a conflict with itself. The
        // procedure checks again under a lock, for two saves racing.
        if (await _timesheetProjectRepository.WeekendSetupExistsForProjectAsync(
                request.ProjectId,
                timeSheetWeekendSetupId ?? 0,
                cancellationToken))
        {
            throw new ConflictException(
                $"Project '{request.ProjectId}' already has a weekend setup. Edit that one instead.");
        }

        // Only an edit can come back empty: the setup it names does not
        // exist, is deleted, or belongs to another organisation.
        var saved = await _timesheetProjectRepository.SaveWeekendSetupAsync(
                timeSheetWeekendSetupId,
                request.OrganizationId,
                request.ClientId,
                request.ProjectId,
                request.IsMondayWeekend,
                request.IsTuesdayWeekend,
                request.IsWednesdayWeekend,
                request.IsThursdayWeekend,
                request.IsFridayWeekend,
                request.IsSaturdayWeekend,
                request.IsSundayWeekend,
                request.UserId,
                _dateTimeProvider.UtcNow,
                cancellationToken)
            ?? throw NotFoundException.For("Weekend setup", timeSheetWeekendSetupId ?? 0);

        _logger.LogInformation(
            "Weekend setup {TimeSheetWeekendSetupId} {Action} for project {ProjectId} of organization {OrganizationId} by user {UserId}.",
            saved.TimeSheetWeekendSetupId,
            timeSheetWeekendSetupId is null ? "added" : "updated",
            request.ProjectId,
            request.OrganizationId,
            request.UserId);

        return saved.ToResponse();
    }

    public async Task DeleteWeekendSetupAsync(
        TimesheetWeekendSetupDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (request.TimeSheetWeekendSetupId <= 0)
        {
            failures[nameof(TimesheetWeekendSetupDeleteRequest.TimeSheetWeekendSetupId)] = new[]
            {
                "TimeSheetWeekendSetupId is required and must be greater than 0."
            };
        }

        if (request.DeletedBy <= 0)
        {
            failures[nameof(TimesheetWeekendSetupDeleteRequest.DeletedBy)] = new[]
            {
                "DeletedBy is required: a soft delete records who performed it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // The repository hides deleted setups, so one deleted already is a
        // 404 - never re-stamped over the first DeletedDate.
        var setup = await _timesheetProjectRepository.GetWeekendSetupForUpdateAsync(
                request.TimeSheetWeekendSetupId,
                cancellationToken)
            ?? throw NotFoundException.For("Weekend setup", request.TimeSheetWeekendSetupId);

        // A soft delete, never a DELETE statement: the row stays, and the
        // entity's query filter and both weekend procedures stop returning it.
        setup.IsDelete = true;
        setup.DeletedDate = _dateTimeProvider.UtcNow;
        setup.DeletedBy = request.DeletedBy;

        await _timesheetProjectRepository.UpdateWeekendSetupAsync(setup, cancellationToken);

        _logger.LogInformation(
            "Weekend setup {TimeSheetWeekendSetupId} of project {ProjectId} soft-deleted by user {DeletedBy}.",
            setup.TimeSheetWeekendSetupId,
            setup.ProjectId,
            request.DeletedBy);
    }

    public async Task<IReadOnlyList<TimesheetRoleAssignmentResponse>> GetRoleAssignmentListAsync(
        int? organizationId,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (organizationId < 0)
        {
            failures["orgID"] = new[] { "orgID must be greater than 0, or left out." };
        }

        if (userId < 0)
        {
            failures["userID"] = new[] { "userID must be greater than 0, or left out." };
        }

        // Null and 0 both mean "not filtered". With neither filter the whole
        // table would come back, across every organisation - refused.
        var organizationFilter = organizationId is > 0 ? organizationId : null;
        var userFilter = userId is > 0 ? userId : null;

        if (failures.Count == 0 && organizationFilter is null && userFilter is null)
        {
            failures["orgID"] = new[] { "orgID or userID is required." };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var assignments = await _timesheetProjectRepository.GetRoleAssignmentListAsync(
            organizationFilter,
            userFilter,
            cancellationToken);

        return assignments.Select(assignment => assignment.ToResponse()).ToList();
    }

    public async Task<TimesheetRoleAssignmentResponse> SaveRoleAssignmentAsync(
        TimesheetRoleAssignmentSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every rule is checked here, before anything is written, and every
        // failure is collected - the caller is told about all of them in one
        // 400, keyed on the field names it sent.
        var failures = new Dictionary<string, string[]>();

        if (request.AssignmentId < 0)
        {
            failures[nameof(TimesheetRoleAssignmentSaveRequest.AssignmentId)] = new[]
            {
                "AssignmentId must be 0 (or left out) to add an assignment, or the id of the assignment to edit."
            };
        }

        if (request.OrganizationId <= 0)
        {
            failures[nameof(TimesheetRoleAssignmentSaveRequest.OrganizationId)] = new[]
            {
                "OrganizationId is required and must be greater than 0."
            };
        }

        if (request.UserId <= 0)
        {
            failures[nameof(TimesheetRoleAssignmentSaveRequest.UserId)] = new[]
            {
                "UserId is required and must be greater than 0."
            };
        }

        if (request.LoginUserId <= 0)
        {
            failures[nameof(TimesheetRoleAssignmentSaveRequest.LoginUserId)] = new[]
            {
                "LoginUserId is required: the row records who created or last updated it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Then the database. No foreign key protects UserID, and the procedure
        // does not check it, so a wrong id would be saved as is.
        if (!await _timesheetProjectRepository.UserExistsAsync(request.UserId, request.OrganizationId, cancellationToken))
        {
            throw new ValidationException(
                nameof(TimesheetRoleAssignmentSaveRequest.UserId),
                $"User '{request.UserId}' is not a live user of organization '{request.OrganizationId}'.");
        }

        // Null and 0 both mean "add" - a negative id was refused above.
        var assignmentId = request.AssignmentId is > 0 ? request.AssignmentId : null;

        // One assignment per user. The assignment being edited is excluded,
        // so an edit that keeps its user is not a conflict with itself. The
        // procedure checks again under a lock, for two saves racing.
        if (await _timesheetProjectRepository.RoleAssignmentExistsForUserAsync(
                request.UserId,
                assignmentId ?? 0,
                cancellationToken))
        {
            throw new ConflictException(
                $"User '{request.UserId}' already has a user access setup. Edit that one instead.");
        }

        // Only an edit can come back empty: the assignment it names does not
        // exist.
        var saved = await _timesheetProjectRepository.SaveRoleAssignmentAsync(
                assignmentId,
                request.UserId,
                request.IsClientSetup,
                request.IsProject,
                request.IsProjectBudget,
                request.IsProjectCategory,
                request.IsStatusSetup,
                request.IsRoleSetup,
                request.IsUserSetup,
                request.IsTimeLog,
                request.IsViewTimesheet,
                request.LoginUserId,
                _dateTimeProvider.UtcNow,
                cancellationToken)
            ?? throw NotFoundException.For("User access setup", assignmentId ?? 0);

        _logger.LogInformation(
            "User access setup {AssignmentId} {Action} for user {UserId} by user {LoginUserId}.",
            saved.AssignmentId,
            assignmentId is null ? "added" : "updated",
            request.UserId,
            request.LoginUserId);

        return saved.ToResponse();
    }

    public async Task DeleteRoleAssignmentAsync(
        TimesheetRoleAssignmentDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AssignmentId <= 0)
        {
            throw new ValidationException(
                nameof(TimesheetRoleAssignmentDeleteRequest.AssignmentId),
                "AssignmentId is required and must be greater than 0.");
        }

        var assignment = await _timesheetProjectRepository.GetRoleAssignmentForUpdateAsync(
                request.AssignmentId,
                cancellationToken)
            ?? throw NotFoundException.For("User access setup", request.AssignmentId);

        // A hard delete: the table has no soft-delete column.
        await _timesheetProjectRepository.DeleteRoleAssignmentAsync(assignment, cancellationToken);

        _logger.LogInformation(
            "User access setup {AssignmentId} of user {UserId} deleted.",
            assignment.AssignmentId,
            assignment.UserId);
    }

    #region Project Time Log

    /// <summary>The values <c>dbo.TimesheetProjectTimeLog.Status</c> accepts.</summary>
    private static readonly IReadOnlySet<int> ProjectTimeLogStatuses = new HashSet<int>
    {
        Constants.TimesheetProjectTimeLog.Status.Save,
        Constants.TimesheetProjectTimeLog.Status.Draft,
    };

    /// <summary>The prefix of every generated project sheet code.</summary>
    private const string ProjectSheetCodePrefix = "PSHT";

    /// <summary>The digits the first code uses, which makes it <c>PSHT0001</c> rather than <c>PSHT1</c>.</summary>
    private const int ProjectSheetCodeMinimumDigits = 4;

    /// <summary><c>dbo.TimesheetProjectSubmission.SheetSubmissionCode</c> is <c>nvarchar(20)</c>.</summary>
    private const int ProjectSheetCodeMaximumLength = 20;

    public async Task<IReadOnlyList<TimesheetProjectTimeLogResponse>> GetProjectTimeLogListAsync(
        int userId,
        int? projectId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (userId <= 0)
        {
            failures["userID"] = new[] { "userID is required and must be greater than 0." };
        }

        if (projectId < 0)
        {
            failures["projectID"] = new[] { "projectID must be greater than 0, or left out." };
        }

        if (fromDate is not null && toDate is not null && toDate.Value.Date < fromDate.Value.Date)
        {
            failures["toDate"] = new[] { "toDate must not be before fromDate." };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Null and 0 both mean "every project".
        var timeLogs = await _timesheetProjectRepository.GetProjectTimeLogListAsync(
            userId,
            projectId is > 0 ? projectId : null,
            fromDate?.Date,
            toDate?.Date,
            cancellationToken);

        return timeLogs.Select(timeLog => timeLog.ToResponse()).ToList();
    }

    public async Task<TimesheetProjectTimeLogResponse> SaveProjectTimeLogAsync(
        TimesheetProjectTimeLogSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // Both times are required, as hh:mm:ss strings on the 24-hour clock
        // (00:00:00 to 23:59:59), and are parsed once, here; a missing or
        // malformed one is a 400 naming the field.
        var startTime = TimeOfDay.Parse(request.StartTime, nameof(TimesheetProjectTimeLogSaveRequest.StartTime));
        var endTime = TimeOfDay.Parse(request.EndTime, nameof(TimesheetProjectTimeLogSaveRequest.EndTime));

        // Every rule on the payload itself is checked before anything is read,
        // and every failure is reported together.
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetProjectTimeLogId < 0)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.TimesheetProjectTimeLogId)] = new[]
            {
                "TimesheetProjectTimeLogId must be 0 (or left out) to add an entry, or the id of the entry to edit."
            };
        }

        if (request.UserId <= 0)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.UserId)] = new[]
            {
                "UserId is required and must be greater than 0."
            };
        }

        if (request.ProjectId <= 0)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.ProjectId)] = new[]
            {
                "ProjectId is required and must be greater than 0."
            };
        }

        if (request.LogDate == default)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.LogDate)] = new[]
            {
                "LogDate is required."
            };
        }

        if (!ProjectTimeLogStatuses.Contains(request.Status))
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.Status)] = new[]
            {
                "Status must be 1 (Save) or 2 (Draft)."
            };
        }

        if (request.AddedBy <= 0)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.AddedBy)] = new[]
            {
                "AddedBy is required: the entry records who created or last updated it."
            };
        }

        // The duration is never sent: it is the whole minutes from StartTime to
        // EndTime, on the same day. Seconds short of a minute are dropped.
        var durationMinutes = (int)(endTime - startTime).TotalMinutes;

        if (endTime <= startTime)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.EndTime)] = new[]
            {
                "EndTime must be after StartTime, on the same day."
            };
        }
        else if (durationMinutes < 1)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.EndTime)] = new[]
            {
                "EndTime must be at least one minute after StartTime."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var logDate = request.LogDate.Date;

        // An edit names the entry it changes, and the sheet it is on now. Both
        // are loaded first: the entry must be the request user's, and time on
        // a locked sheet cannot be moved out of it.
        TimesheetProjectTimeLog? original = null;
        TimesheetProjectSubmission? originalSubmission = null;

        if (request.TimesheetProjectTimeLogId > 0)
        {
            original = await _timesheetProjectRepository.GetProjectTimeLogForUpdateAsync(
                    request.TimesheetProjectTimeLogId,
                    cancellationToken)
                ?? throw NotFoundException.For("Time log", request.TimesheetProjectTimeLogId);

            originalSubmission = await RequireOwnSubmissionAsync(
                original,
                request.UserId,
                cancellationToken);

            RequireSubmissionIsOpen(originalSubmission);
        }

        var project = await _timesheetProjectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new ValidationException(
                nameof(TimesheetProjectTimeLogSaveRequest.ProjectId),
                $"Project '{request.ProjectId}' does not exist.");

        var (projectStartDate, submissionFrequency, organizationId) = RequireSubmissionSetup(project);

        if (logDate < projectStartDate.Date)
        {
            throw new ValidationException(
                nameof(TimesheetProjectTimeLogSaveRequest.LogDate),
                $"LogDate must not be before the project's start date, {projectStartDate:yyyy-MM-dd}.");
        }

        if (project.EndDate is { } projectEndDate && logDate > projectEndDate.Date)
        {
            throw new ValidationException(
                nameof(TimesheetProjectTimeLogSaveRequest.LogDate),
                $"LogDate must not be after the project's end date, {projectEndDate:yyyy-MM-dd}.");
        }

        if (!await _timesheetProjectRepository.UserProjectSetupExistsAsync(
                request.UserId,
                request.ProjectId,
                0,
                cancellationToken))
        {
            throw new ValidationException(
                nameof(TimesheetProjectTimeLogSaveRequest.ProjectId),
                $"User '{request.UserId}' is not assigned to project '{request.ProjectId}'.");
        }

        // The sheet the entry belongs on: the user's sheet for this project and
        // the period containing the date. When it is the sheet the entry is on
        // already, EF Core hands back the same tracked instance.
        var (periodStartDate, periodEndDate) = SubmissionPeriodOf(logDate, projectStartDate.Date, submissionFrequency);
        var now = _dateTimeProvider.UtcNow;

        var submission = await _timesheetProjectRepository.GetProjectSubmissionForPeriodForUpdateAsync(
            request.UserId,
            request.ProjectId,
            periodStartDate,
            cancellationToken);

        if (submission is not null)
        {
            RequireSubmissionIsOpen(submission);
        }
        else
        {
            // The period's first entry opens its sheet, with the next code.
            // Saved on its own, before the entry: should the entry then fail,
            // what is left is an empty pending sheet, which is a valid sheet.
            submission = await _timesheetProjectRepository.AddProjectSubmissionAsync(
                new TimesheetProjectSubmission
                {
                    SheetSubmissionCode = NextProjectSheetCode(
                        await _timesheetProjectRepository.GetLatestProjectSubmissionCodeAsync(cancellationToken)),
                    StatusId = Constants.TimesheetProjectSubmission.Status.Pending,
                    UserId = request.UserId,
                    ProjectId = request.ProjectId,
                    OrganizationId = organizationId,
                    PeriodStartDate = periodStartDate,
                    PeriodEndDate = periodEndDate,
                    TotalHoursLogged = 0,
                    TotalMinutesLogged = 0,
                    IsDelete = 0,
                    DateAdded = now,
                },
                cancellationToken);

            _logger.LogInformation(
                "Project sheet {SheetSubmissionCode} opened for user {UserId} on project {ProjectId}, " +
                "{PeriodStart:yyyy-MM-dd} to {PeriodEnd:yyyy-MM-dd}.",
                submission.SheetSubmissionCode,
                request.UserId,
                request.ProjectId,
                periodStartDate,
                periodEndDate);
        }

        var timeLog = original ?? new TimesheetProjectTimeLog
        {
            IsDelete = 0,
            CreateDate = now,
            CreatedBy = request.AddedBy,
        };

        timeLog.TimesheetProjectSubmissionId = submission.TimesheetProjectSubmissionId;
        timeLog.SheetSubmissionCode = submission.SheetSubmissionCode;
        timeLog.LogDate = logDate;
        timeLog.StartTime = startTime;
        timeLog.EndTime = endTime;
        timeLog.DurationMinutes = durationMinutes;
        timeLog.Description = request.Description;
        timeLog.Status = request.Status;

        if (original is not null)
        {
            timeLog.UpdateDate = now;
            timeLog.UpdatedBy = request.AddedBy;
        }

        // The totals: every other live entry of the sheet, plus this one. An
        // entry that moved to another sheet takes its minutes off the old one.
        await SetSubmissionTotalsAsync(
            submission,
            original?.TimesheetProjectTimeLogId ?? 0,
            durationMinutes,
            cancellationToken);

        if (originalSubmission is not null
            && originalSubmission.TimesheetProjectSubmissionId != submission.TimesheetProjectSubmissionId)
        {
            await SetSubmissionTotalsAsync(
                originalSubmission,
                original!.TimesheetProjectTimeLogId,
                0,
                cancellationToken);
        }

        var saved = await _timesheetProjectRepository.SaveProjectTimeLogAsync(timeLog, cancellationToken);

        _logger.LogInformation(
            "Project time log {TimesheetProjectTimeLogId} {Action} for user {UserId} on project {ProjectId}: " +
            "{DurationMinutes} minutes on {LogDate:yyyy-MM-dd}, sheet {SheetSubmissionCode}, by user {AddedBy}.",
            saved.TimesheetProjectTimeLogId,
            original is null ? "added" : "updated",
            request.UserId,
            request.ProjectId,
            durationMinutes,
            logDate,
            submission.SheetSubmissionCode,
            request.AddedBy);

        return saved.ToResponse(submission);
    }

    public async Task DeleteProjectTimeLogAsync(
        TimesheetProjectTimeLogDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetProjectTimeLogId <= 0)
        {
            failures[nameof(TimesheetProjectTimeLogDeleteRequest.TimesheetProjectTimeLogId)] = new[]
            {
                "TimesheetProjectTimeLogId is required and must be greater than 0."
            };
        }

        if (request.UserId <= 0)
        {
            failures[nameof(TimesheetProjectTimeLogDeleteRequest.UserId)] = new[]
            {
                "UserId is required and must be greater than 0."
            };
        }

        if (request.AddedBy <= 0)
        {
            failures[nameof(TimesheetProjectTimeLogDeleteRequest.AddedBy)] = new[]
            {
                "AddedBy is required: a soft delete records who performed it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // The repository hides deleted entries, so one deleted already is a
        // 404 - never re-stamped over the first DeletedDate.
        var timeLog = await _timesheetProjectRepository.GetProjectTimeLogForUpdateAsync(
                request.TimesheetProjectTimeLogId,
                cancellationToken)
            ?? throw NotFoundException.For("Time log", request.TimesheetProjectTimeLogId);

        var submission = await RequireOwnSubmissionAsync(timeLog, request.UserId, cancellationToken);

        RequireSubmissionIsOpen(submission);

        // A soft delete: the row stays, and the query filter stops returning it.
        timeLog.IsDelete = 1;
        timeLog.DeletedDate = _dateTimeProvider.UtcNow;
        timeLog.DeletedBy = request.AddedBy;

        await SetSubmissionTotalsAsync(submission, timeLog.TimesheetProjectTimeLogId, 0, cancellationToken);

        await _timesheetProjectRepository.SaveProjectTimeLogAsync(timeLog, cancellationToken);

        _logger.LogInformation(
            "Project time log {TimesheetProjectTimeLogId} on sheet {SheetSubmissionCode} soft-deleted by user {AddedBy}.",
            timeLog.TimesheetProjectTimeLogId,
            submission.SheetSubmissionCode,
            request.AddedBy);
    }

    /// <summary>
    /// Loads the sheet an entry is on, tracked, and confirms it is the user's.
    /// The table has no <c>UserID</c> of its own, so the sheet is what says
    /// whose the entry is.
    /// </summary>
    private async Task<TimesheetProjectSubmission> RequireOwnSubmissionAsync(
        TimesheetProjectTimeLog timeLog,
        int userId,
        CancellationToken cancellationToken)
    {
        var submission = await _timesheetProjectRepository.GetProjectSubmissionForUpdateAsync(
                timeLog.TimesheetProjectSubmissionId,
                cancellationToken)
            ?? throw new BusinessException(
                $"Time log '{timeLog.TimesheetProjectTimeLogId}' is on a sheet that no longer exists, so it cannot be changed.");

        if (submission.UserId != userId)
        {
            throw new BusinessException(
                $"Time log '{timeLog.TimesheetProjectTimeLogId}' does not belong to user '{userId}', so it " +
                "cannot be changed as theirs.");
        }

        return submission;
    }

    /// <summary>
    /// Rejects a change to a submitted or approved sheet. A pending sheet is
    /// open, and so is a rejected one - it is sent back for changes.
    /// </summary>
    private static void RequireSubmissionIsOpen(TimesheetProjectSubmission submission)
    {
        if (submission.StatusId is Constants.TimesheetProjectSubmission.Status.Submitted
            or Constants.TimesheetProjectSubmission.Status.Approved)
        {
            throw new BusinessException(
                $"Timesheet '{submission.SheetSubmissionCode}' has already been submitted, so its time can no " +
                "longer be added to or changed.");
        }
    }

    /// <summary>
    /// Sets a tracked sheet's totals to every other live entry on it plus
    /// <paramref name="addedMinutes"/>. Written by the next save, together with
    /// the entry.
    /// </summary>
    private async Task SetSubmissionTotalsAsync(
        TimesheetProjectSubmission submission,
        int excludedTimesheetProjectTimeLogId,
        int addedMinutes,
        CancellationToken cancellationToken)
    {
        var minutes = await _timesheetProjectRepository.GetProjectSubmissionLoggedMinutesAsync(
                submission.TimesheetProjectSubmissionId,
                excludedTimesheetProjectTimeLogId,
                cancellationToken)
            + addedMinutes;

        submission.TotalMinutesLogged = minutes;
        submission.TotalHoursLogged = TimesheetProjectMappings.ToHours(minutes);
    }

    /// <summary>
    /// The project's start date, submission frequency and organisation.
    /// Without the first two there is no period to put a sheet in, and without
    /// the third no organisation to file it under.
    /// </summary>
    /// <exception cref="BusinessException">Any of the three is missing.</exception>
    private static (DateTime StartDate, int SubmissionFrequency, int OrganizationId) RequireSubmissionSetup(
        TimesheetProject project)
    {
        if (project.StartDate is not { } startDate
            || project.SubmissionFrequency is not { } submissionFrequency
            || !SubmissionFrequencies.Contains(submissionFrequency)
            || project.OrganizationId is not { } organizationId)
        {
            throw new BusinessException(
                $"Project '{project.ProjectId}' needs a start date, a submission frequency and an organization " +
                "before time can be logged or submitted on it.");
        }

        return (startDate.Date, submissionFrequency, organizationId);
    }

    /// <summary>
    /// The submission period containing <paramref name="logDate"/>, counted
    /// from the project's start date: blocks of 7 days for weekly, 15 days for
    /// every 15 days, and calendar months from the start day for monthly - a
    /// project starting on the 10th has periods from the 10th to the 9th.
    /// Both dates are inclusive. <paramref name="logDate"/> is never before
    /// <paramref name="projectStartDate"/>.
    /// </summary>
    private static (DateTime Start, DateTime End) SubmissionPeriodOf(
        DateTime logDate,
        DateTime projectStartDate,
        int submissionFrequency)
    {
        if (submissionFrequency == Constants.TimesheetProject.SubmissionFrequency.Monthly)
        {
            // Every period is counted from the start date itself, never from
            // the previous period, so a start on the 31st does not drift to
            // the 28th after February.
            var months = (logDate.Year - projectStartDate.Year) * 12 + logDate.Month - projectStartDate.Month;

            if (projectStartDate.AddMonths(months) > logDate)
            {
                months--;
            }

            return (projectStartDate.AddMonths(months), projectStartDate.AddMonths(months + 1).AddDays(-1));
        }

        var days = submissionFrequency == Constants.TimesheetProject.SubmissionFrequency.Weekly ? 7 : 15;
        var periodStart = projectStartDate.AddDays((logDate - projectStartDate).Days / days * days);

        return (periodStart, periodStart.AddDays(days - 1));
    }

    /// <summary>
    /// The code after <paramref name="latest"/>: <c>PSHT0001</c> when there is
    /// none, then <c>PSHT0002</c> and so on. Past <c>PSHT9999</c> the number
    /// simply grows a digit - <c>PSHT10000</c>.
    /// </summary>
    /// <exception cref="BusinessException">The code would not fit the column.</exception>
    private static string NextProjectSheetCode(string? latest)
    {
        // The repository only returns codes of exactly this shape. TryParse
        // all the same: starting over is better than throwing on data nobody
        // can correct.
        var number = latest is not null
                     && long.TryParse(
                         latest.Trim()[ProjectSheetCodePrefix.Length..],
                         NumberStyles.None,
                         CultureInfo.InvariantCulture,
                         out var parsed)
            ? parsed + 1
            : 1;

        var code = ProjectSheetCodePrefix
            + number.ToString(new string('0', ProjectSheetCodeMinimumDigits), CultureInfo.InvariantCulture);

        if (code.Length > ProjectSheetCodeMaximumLength)
        {
            throw new BusinessException(
                $"The project sheet code sequence has run out of room: '{code}' is longer than the " +
                $"{ProjectSheetCodeMaximumLength} characters SheetSubmissionCode can hold.");
        }

        return code;
    }

    #endregion

    #region Project Time Sheet

    public async Task<IReadOnlyList<TimesheetProjectSheetResponse>> GetProjectSheetListAsync(
        int? userId,
        int? projectId,
        int? organizationId,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (userId < 0)
        {
            failures["userID"] = new[] { "userID must be greater than 0, or left out." };
        }

        if (projectId < 0)
        {
            failures["projectID"] = new[] { "projectID must be greater than 0, or left out." };
        }

        if (organizationId < 0)
        {
            failures["orgID"] = new[] { "orgID must be greater than 0, or left out." };
        }

        // Null and 0 both mean "not filtered". With no filter at all, every
        // sheet of every organisation would come back - refused.
        var userFilter = userId is > 0 ? userId : null;
        var projectFilter = projectId is > 0 ? projectId : null;
        var organizationFilter = organizationId is > 0 ? organizationId : null;

        if (failures.Count == 0 && userFilter is null && projectFilter is null && organizationFilter is null)
        {
            failures["userID"] = new[] { "userID, projectID or orgID is required." };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // The sheets are built per organisation, whole, and only then
        // filtered - see BuildProjectSheetListAsync. So first: which
        // organisations can hold a matching sheet.
        IReadOnlyList<int> organizationIds;

        if (organizationFilter is { } orgId)
        {
            organizationIds = new[] { orgId };
        }
        else if (projectFilter is { } filteredProjectId)
        {
            var project = await _timesheetProjectRepository.GetByIdAsync(filteredProjectId, cancellationToken);

            organizationIds = project?.OrganizationId is { } projectOrganizationId
                ? new[] { projectOrganizationId }
                : Array.Empty<int>();
        }
        else
        {
            organizationIds = await _timesheetProjectRepository.GetUserProjectOrganizationIdsAsync(
                userFilter!.Value,
                cancellationToken);
        }

        var sheets = new List<TimesheetProjectSheetResponse>();

        foreach (var listedOrganizationId in organizationIds)
        {
            sheets.AddRange(await BuildProjectSheetListAsync(listedOrganizationId, cancellationToken));
        }

        return sheets
            .Where(sheet => (userFilter is null || sheet.UserId == userFilter)
                            && (projectFilter is null || sheet.ProjectId == projectFilter))
            .OrderBy(sheet => sheet.PeriodStartDate)
            .ThenBy(sheet => sheet.ProjectName)
            .ThenBy(sheet => sheet.ProjectId)
            .ThenBy(sheet => sheet.UserId)
            .ToList();
    }

    public async Task<TimesheetProjectSheetResponse> SubmitProjectSheetAsync(
        TimesheetProjectSheetSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        var sheetSubmissionCode = string.IsNullOrWhiteSpace(request.SheetSubmissionCode)
            ? null
            : request.SheetSubmissionCode.Trim();
        var failures = new Dictionary<string, string[]>();

        if (request.UserId <= 0)
        {
            failures[nameof(TimesheetProjectSheetSubmitRequest.UserId)] = new[]
            {
                "UserId is required and must be greater than 0."
            };
        }

        if (request.ProjectId <= 0)
        {
            failures[nameof(TimesheetProjectSheetSubmitRequest.ProjectId)] = new[]
            {
                "ProjectId is required and must be greater than 0."
            };
        }

        if (request.AddedBy <= 0)
        {
            failures[nameof(TimesheetProjectSheetSubmitRequest.AddedBy)] = new[]
            {
                "AddedBy is required: the submit records who performed it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var now = _dateTimeProvider.UtcNow;

        var submission = await ResolveProjectSheetToSubmitAsync(
            request.UserId,
            request.ProjectId,
            sheetSubmissionCode,
            now,
            cancellationToken);

        // Pending is the first submit; Rejected is a resubmit after changes.
        if (submission.StatusId is not (Constants.TimesheetProjectSubmission.Status.Pending
            or Constants.TimesheetProjectSubmission.Status.Rejected))
        {
            throw new ConflictException(
                $"Timesheet '{submission.SheetSubmissionCode}' is already {ProjectSheetStatusName(submission.StatusId)}.");
        }

        // The totals as they stand in the time log, now. No entries is a
        // sheet submitted with no time - allowed.
        var minutes = await _timesheetProjectRepository.GetProjectSubmissionLoggedMinutesAsync(
            submission.TimesheetProjectSubmissionId,
            0,
            cancellationToken);

        submission.TotalMinutesLogged = minutes;
        submission.TotalHoursLogged = TimesheetProjectMappings.ToHours(minutes);
        submission.StatusId = Constants.TimesheetProjectSubmission.Status.Submitted;
        submission.SubmittedBy = request.AddedBy;
        submission.SubmittedDate = now;

        await SaveProjectSheetStatusAsync(submission, request.AddedBy, now, cancellationToken);

        if (sheetSubmissionCode is not null
            && !string.Equals(sheetSubmissionCode, submission.SheetSubmissionCode, StringComparison.OrdinalIgnoreCase))
        {
            // A calculated sheet whose predicted code was taken between the
            // working-out and the insert.
            _logger.LogInformation(
                "Timesheet for user {UserId}, project {ProjectId} from {PeriodStart:yyyy-MM-dd} was requested as " +
                "{RequestedCode} and submitted as {SheetSubmissionCode}.",
                submission.UserId,
                submission.ProjectId,
                submission.PeriodStartDate,
                sheetSubmissionCode,
                submission.SheetSubmissionCode);
        }

        _logger.LogInformation(
            "Timesheet {SheetSubmissionCode} of user {UserId} submitted by user {AddedBy} with {Minutes} minutes.",
            submission.SheetSubmissionCode,
            request.UserId,
            request.AddedBy,
            minutes);

        return await ProjectSheetResponseAsync(submission, cancellationToken);
    }

    public async Task<TimesheetProjectSheetResponse> UpdateProjectSheetStatusAsync(
        TimesheetProjectSheetStatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var rejectionReason = string.IsNullOrWhiteSpace(request.RejectionReason) ? null : request.RejectionReason.Trim();
        var isRejection = request.StatusId == Constants.TimesheetProjectSubmission.Status.Rejected;
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetProjectSubmissionId <= 0)
        {
            failures[nameof(TimesheetProjectSheetStatusUpdateRequest.TimesheetProjectSubmissionId)] = new[]
            {
                "TimesheetProjectSubmissionId is required and must be greater than 0."
            };
        }

        if (request.StatusId is not (Constants.TimesheetProjectSubmission.Status.Approved
            or Constants.TimesheetProjectSubmission.Status.Rejected))
        {
            failures[nameof(TimesheetProjectSheetStatusUpdateRequest.StatusId)] = new[]
            {
                "StatusId must be 2 (Approved) or 3 (Rejected)."
            };
        }

        if (isRejection && rejectionReason is null)
        {
            failures[nameof(TimesheetProjectSheetStatusUpdateRequest.RejectionReason)] = new[]
            {
                "RejectionReason is required when rejecting a timesheet."
            };
        }
        else if (isRejection && rejectionReason!.Length > RejectionReasonMaximumLength)
        {
            failures[nameof(TimesheetProjectSheetStatusUpdateRequest.RejectionReason)] = new[]
            {
                $"RejectionReason must be {RejectionReasonMaximumLength} characters or fewer."
            };
        }

        if (request.AddedBy <= 0)
        {
            failures[nameof(TimesheetProjectSheetStatusUpdateRequest.AddedBy)] = new[]
            {
                "AddedBy is required: the change records who made it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var submission = await _timesheetProjectRepository.GetProjectSubmissionForUpdateAsync(
                request.TimesheetProjectSubmissionId,
                cancellationToken)
            ?? throw NotFoundException.For("Timesheet", request.TimesheetProjectSubmissionId);

        // Only the project's approver reviews its sheets: a live row in
        // dbo.Timesheet_Approver_setup for this project and user, with
        // IsTimesheetApprover = 1. Anyone else is refused, whatever the status.
        if (!await _timesheetProjectRepository.IsProjectTimesheetApproverAsync(
                submission.ProjectId,
                request.AddedBy,
                cancellationToken))
        {
            throw new ForbiddenException(
                $"User '{request.AddedBy}' is not the timesheet approver of project '{submission.ProjectId}', " +
                "so cannot approve or reject its timesheets.");
        }

        // Only a submitted sheet is waiting for a decision. An approved one is
        // final, and a pending or rejected one is the user's to (re)submit.
        if (submission.StatusId != Constants.TimesheetProjectSubmission.Status.Submitted)
        {
            throw new ConflictException(
                $"Timesheet '{submission.SheetSubmissionCode}' is {ProjectSheetStatusName(submission.StatusId)}, so it " +
                "cannot be approved or rejected; only a submitted timesheet can.");
        }

        var now = _dateTimeProvider.UtcNow;

        submission.StatusId = request.StatusId;

        if (isRejection)
        {
            submission.RejectedBy = request.AddedBy;
            submission.RejectedDate = now;
            submission.RejectionReason = rejectionReason;
        }
        else
        {
            submission.ApprovedBy = request.AddedBy;
            submission.ApprovedDate = now;
        }

        await SaveProjectSheetStatusAsync(submission, request.AddedBy, now, cancellationToken);

        _logger.LogInformation(
            "Timesheet {SheetSubmissionCode} {Action} by user {AddedBy}.",
            submission.SheetSubmissionCode,
            isRejection ? "rejected" : "approved",
            request.AddedBy);

        return await ProjectSheetResponseAsync(submission, cancellationToken);
    }

    /// <summary>
    /// Every sheet of one organisation - every user, every project - ordered
    /// by period, then project name, then user: each stored sheet, and each
    /// period up to the current one with no row, as a calculated sheet.
    /// <para>
    /// The calculated sheets are given the codes they would get if submitted
    /// now, in that order. That is why the list is always built for the whole
    /// organisation and filtered afterwards: every filter of the get, and the
    /// submit, must agree on which period a predicted code belongs to, and on
    /// no two users being predicted the same code.
    /// </para>
    /// </summary>
    private async Task<List<TimesheetProjectSheetResponse>> BuildProjectSheetListAsync(
        int organizationId,
        CancellationToken cancellationToken)
    {
        // Every sheet with a row, whatever its period, and every live
        // assignment - the user-and-project pairs whose periods may have no row.
        var stored = await _timesheetProjectRepository.GetProjectSheetListAsync(null, null, organizationId, cancellationToken);
        var assignments = await _timesheetProjectRepository.GetUserProjectSetupListAsync(null, null, organizationId, cancellationToken);

        var projectIds = stored.Select(sheet => sheet.ProjectId)
            .Concat(assignments.Select(setup => setup.ProjectId!.Value))
            .Distinct()
            .ToList();

        var projects = (await _timesheetProjectRepository.GetListByIdsAsync(projectIds, cancellationToken))
            .ToDictionary(project => project.ProjectId);

        var sheets = stored
            .Select(sheet => sheet.ToResponse(projects.GetValueOrDefault(sheet.ProjectId), ProjectSheetStatusName(sheet.StatusId)))
            .ToList();

        var storedPeriods = stored
            .Select(sheet => (sheet.UserId, sheet.ProjectId, sheet.PeriodStartDate.Date))
            .ToHashSet();

        var today = _dateTimeProvider.UtcNow.Date;

        // Then every period up to and including the current one that has no
        // row: nothing was logged on it and it was never submitted. It still
        // gets a sheet in the list, with a calculated code.
        foreach (var assignment in assignments.GroupBy(setup => (setup.UserId, ProjectId: setup.ProjectId!.Value)))
        {
            if (!projects.TryGetValue(assignment.Key.ProjectId, out var project)
                || project.StartDate is not { } projectStartDate
                || project.SubmissionFrequency is not { } submissionFrequency
                || !SubmissionFrequencies.Contains(submissionFrequency))
            {
                // No start date or frequency: there are no periods to owe.
                continue;
            }

            // From the period the user joined the project in - or the
            // project's first, if the assignment has no date or predates it.
            var joinedOn = assignment.Min(setup => setup.CreateDate)?.Date;
            var firstDate = joinedOn > projectStartDate.Date ? joinedOn.Value : projectStartDate.Date;
            var period = SubmissionPeriodOf(firstDate, projectStartDate.Date, submissionFrequency);

            while (period.Start <= today && (project.EndDate is not { } endDate || period.Start <= endDate.Date))
            {
                if (!storedPeriods.Contains((assignment.Key.UserId, project.ProjectId, period.Start)))
                {
                    sheets.Add(new TimesheetProjectSheetResponse
                    {
                        TimesheetProjectSubmissionId = null,
                        IsCalculated = true,
                        StatusId = Constants.TimesheetProjectSubmission.Status.Pending,
                        StatusName = ProjectSheetStatusName(Constants.TimesheetProjectSubmission.Status.Pending),
                        UserId = assignment.Key.UserId,
                        ProjectId = project.ProjectId,
                        ProjectCode = project.Code,
                        ProjectName = project.ProjectName,
                        PeriodStartDate = period.Start,
                        PeriodEndDate = period.End,
                        TimeLogCount = 0,
                        TotalMinutesLogged = 0,
                        TotalHoursLogged = 0,
                    });
                }

                period = SubmissionPeriodOf(period.End.AddDays(1), projectStartDate.Date, submissionFrequency);
            }
        }

        foreach (var sheet in sheets)
        {
            sheet.IsPeriodEnded = sheet.PeriodEndDate.Date < today;
        }

        sheets = sheets
            .OrderBy(sheet => sheet.PeriodStartDate)
            .ThenBy(sheet => sheet.ProjectName)
            .ThenBy(sheet => sheet.ProjectId)
            .ThenBy(sheet => sheet.UserId)
            .ToList();

        // The calculated sheets are given the codes they would get if they
        // were submitted now, in order, oldest first. A prediction, not a
        // reservation: any sheet opened in the meantime takes the next code.
        var code = await _timesheetProjectRepository.GetLatestProjectSubmissionCodeAsync(cancellationToken);

        foreach (var sheet in sheets.Where(sheet => sheet.IsCalculated))
        {
            code = NextProjectSheetCode(code);
            sheet.SheetSubmissionCode = code;
        }

        return sheets;
    }

    /// <summary>
    /// The sheet a submit names, tracked - opened first when it is a calculated
    /// one. The period is never sent; it comes from the code, or from the
    /// project and its frequency:
    /// <list type="bullet">
    /// <item>A code of a stored sheet: that sheet, which must be this user's
    /// and this project's.</item>
    /// <item>Any other code: the calculated sheet of this project the get
    /// predicts that code for.</item>
    /// <item>No code: the most recent period of this project, up to the
    /// current one, that is pending or rejected - stored or calculated.</item>
    /// </list>
    /// </summary>
    private async Task<TimesheetProjectSubmission> ResolveProjectSheetToSubmitAsync(
        int userId,
        int projectId,
        string? sheetSubmissionCode,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (sheetSubmissionCode is not null)
        {
            var stored = await _timesheetProjectRepository.GetProjectSubmissionByCodeForUpdateAsync(
                sheetSubmissionCode,
                cancellationToken);

            if (stored is not null)
            {
                if (stored.UserId != userId || stored.ProjectId != projectId)
                {
                    throw new BusinessException(
                        $"Timesheet '{stored.SheetSubmissionCode}' is not user '{userId}''s timesheet for project " +
                        $"'{projectId}', so it cannot be submitted as theirs.");
                }

                return stored;
            }
        }

        // Worked out exactly as the get works it out - for the project's whole
        // organisation - so a predicted code names the same period here as it
        // did there.
        var project = await _timesheetProjectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new ValidationException(
                nameof(TimesheetProjectSheetSubmitRequest.ProjectId),
                $"Project '{projectId}' does not exist.");

        var (_, _, organizationId) = RequireSubmissionSetup(project);

        var sheets = (await BuildProjectSheetListAsync(organizationId, cancellationToken))
            .Where(sheet => sheet.UserId == userId && sheet.ProjectId == projectId)
            .ToList();

        var target = sheetSubmissionCode is not null
            ? sheets.FirstOrDefault(sheet => sheet.IsCalculated
                                             && string.Equals(sheet.SheetSubmissionCode, sheetSubmissionCode, StringComparison.OrdinalIgnoreCase))
              ?? throw new ValidationException(
                  nameof(TimesheetProjectSheetSubmitRequest.SheetSubmissionCode),
                  $"SheetSubmissionCode '{sheetSubmissionCode}' is not a timesheet of user '{userId}' on project " +
                  $"'{projectId}'. Codes of unsaved timesheets move when another timesheet is opened - get the " +
                  "timesheets again and submit the code returned.")
            : sheets
                  .Where(sheet => sheet.StatusId is Constants.TimesheetProjectSubmission.Status.Pending
                                  or Constants.TimesheetProjectSubmission.Status.Rejected)
                  .MaxBy(sheet => sheet.PeriodStartDate)
              ?? throw await NoProjectSheetToSubmitAsync(userId, projectId, sheets.Count, now, cancellationToken);

        return target.TimesheetProjectSubmissionId is { } submissionId
            ? await _timesheetProjectRepository.GetProjectSubmissionForUpdateAsync(submissionId, cancellationToken)
              ?? throw NotFoundException.For("Timesheet", submissionId)
            : await GetOrOpenProjectSheetAsync(userId, projectId, target.PeriodStartDate.Date, now, cancellationToken);
    }

    /// <summary>
    /// Why a submit with no code found nothing to submit. The sheet list skips
    /// a project it cannot work periods out for, so an empty list on its own
    /// would read as "everything is submitted" when the real reason is the
    /// setup. Each reason is checked in turn; only when the setup is complete
    /// and sheets exist is it genuinely "nothing left to submit".
    /// </summary>
    private async Task<Exception> NoProjectSheetToSubmitAsync(
        int userId,
        int projectId,
        int sheetCount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var project = await _timesheetProjectRepository.GetByIdAsync(projectId, cancellationToken);

        if (project is null)
        {
            return new ValidationException(
                nameof(TimesheetProjectSheetSubmitRequest.ProjectId),
                $"Project '{projectId}' does not exist.");
        }

        if (project.StartDate is null
            || project.SubmissionFrequency is not { } submissionFrequency
            || !SubmissionFrequencies.Contains(submissionFrequency))
        {
            return new BusinessException(
                $"Project '{projectId}' has no {(project.StartDate is null ? "start date" : "submission frequency")}, " +
                "so it has no timesheet periods to submit. Set it on the project first.");
        }

        if (!await _timesheetProjectRepository.UserProjectSetupExistsAsync(userId, projectId, 0, cancellationToken))
        {
            return new BusinessException(
                $"User '{userId}' is not assigned to project '{projectId}' (or the assignment was removed), so " +
                "they have no timesheets on it to submit.");
        }

        if (project.StartDate.Value.Date > now.Date)
        {
            return new BusinessException(
                $"Project '{projectId}' starts on {project.StartDate.Value:yyyy-MM-dd}; its first timesheet period " +
                "has not begun yet.");
        }

        if (sheetCount == 0)
        {
            return new BusinessException(
                $"User '{userId}' has no timesheet period on project '{projectId}' to submit: the project ended " +
                "before the user was assigned to it.");
        }

        return new ConflictException(
            $"Every timesheet of user '{userId}' on project '{projectId}' is already submitted or approved.");
    }

    /// <summary><c>dbo.TimesheetProjectSubmission.RejectionReason</c> is <c>nvarchar(500)</c>.</summary>
    private const int RejectionReasonMaximumLength = 500;

    /// <summary>
    /// The user's stored sheet for a project and period, tracked - or, when
    /// the period has none, a new empty Pending sheet with the next code.
    /// </summary>
    /// <exception cref="ValidationException">The project does not exist.</exception>
    /// <exception cref="BusinessException">
    /// The project has no start date, frequency or organisation; the date is
    /// not the start of one of its periods that has begun; or - for a period
    /// with no sheet yet - the user is not assigned to the project.
    /// </exception>
    private async Task<TimesheetProjectSubmission> GetOrOpenProjectSheetAsync(
        int userId,
        int projectId,
        DateTime periodStartDate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var project = await _timesheetProjectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new ValidationException(
                nameof(TimesheetProjectSheetSubmitRequest.ProjectId),
                $"Project '{projectId}' does not exist.");

        var (projectStartDate, submissionFrequency, organizationId) = RequireSubmissionSetup(project);

        // Only a real period start: anything else would open a sheet for a
        // period that does not exist, overlapping the ones that do.
        var isPeriodStart = periodStartDate >= projectStartDate
            && SubmissionPeriodOf(periodStartDate, projectStartDate, submissionFrequency).Start == periodStartDate;

        if (!isPeriodStart
            || periodStartDate > now.Date
            || (project.EndDate is { } endDate && periodStartDate > endDate.Date))
        {
            // Unreachable from the submit, which only passes periods it worked
            // out itself; kept so no caller can open a sheet for a made-up one.
            throw new BusinessException(
                $"{periodStartDate:yyyy-MM-dd} is not the start of a period of project '{projectId}' that has begun.");
        }

        var submission = await _timesheetProjectRepository.GetProjectSubmissionForPeriodForUpdateAsync(
            userId,
            projectId,
            periodStartDate,
            cancellationToken);

        if (submission is not null)
        {
            return submission;
        }

        if (!await _timesheetProjectRepository.UserProjectSetupExistsAsync(userId, projectId, 0, cancellationToken))
        {
            throw new BusinessException($"User '{userId}' is not assigned to project '{projectId}'.");
        }

        // Saved on its own, before the status change, so the change has an id
        // to record in the audit log. Should the submit then fail, what is left
        // is an empty pending sheet - which the period is owed anyway.
        submission = await _timesheetProjectRepository.AddProjectSubmissionAsync(
            new TimesheetProjectSubmission
            {
                SheetSubmissionCode = NextProjectSheetCode(
                    await _timesheetProjectRepository.GetLatestProjectSubmissionCodeAsync(cancellationToken)),
                StatusId = Constants.TimesheetProjectSubmission.Status.Pending,
                UserId = userId,
                ProjectId = projectId,
                OrganizationId = organizationId,
                PeriodStartDate = periodStartDate,
                PeriodEndDate = SubmissionPeriodOf(periodStartDate, projectStartDate, submissionFrequency).End,
                TotalHoursLogged = 0,
                TotalMinutesLogged = 0,
                IsDelete = 0,
                DateAdded = now,
            },
            cancellationToken);

        _logger.LogInformation(
            "Project sheet {SheetSubmissionCode} opened for user {UserId} on project {ProjectId} to be submitted " +
            "with no time logged, {PeriodStart:yyyy-MM-dd} to {PeriodEnd:yyyy-MM-dd}.",
            submission.SheetSubmissionCode,
            userId,
            projectId,
            submission.PeriodStartDate,
            submission.PeriodEndDate);

        return submission;
    }

    /// <summary>
    /// Saves a status change made to a tracked sheet, with its row in
    /// <c>dbo.TimesheetProjectSubmissionAuditLog</c> - every submit, approve
    /// and reject is recorded.
    /// </summary>
    private async Task SaveProjectSheetStatusAsync(
        TimesheetProjectSubmission submission,
        int addedBy,
        DateTime now,
        CancellationToken cancellationToken) =>
        await _timesheetProjectRepository.SaveProjectSubmissionStatusAsync(
            submission,
            new TimesheetProjectSubmissionAuditLog
            {
                TimesheetProjectSubmissionId = submission.TimesheetProjectSubmissionId,
                StatusId = submission.StatusId,
                CreatedBy = addedBy,
                CreateDate = now,
            },
            cancellationToken);

    /// <summary>A stored sheet as the get lists it, read back after a change.</summary>
    private async Task<TimesheetProjectSheetResponse> ProjectSheetResponseAsync(
        TimesheetProjectSubmission submission,
        CancellationToken cancellationToken)
    {
        var sheet = (await _timesheetProjectRepository.GetProjectSheetListAsync(
                submission.UserId,
                submission.ProjectId,
                null,
                cancellationToken))
            .First(detail => detail.TimesheetProjectSubmissionId == submission.TimesheetProjectSubmissionId);

        var project = await _timesheetProjectRepository.GetByIdAsync(submission.ProjectId, cancellationToken);
        var response = sheet.ToResponse(project, ProjectSheetStatusName(sheet.StatusId));

        response.IsPeriodEnded = response.PeriodEndDate.Date < _dateTimeProvider.UtcNow.Date;

        return response;
    }

    /// <summary>The name of a <c>dbo.TimesheetProjectSubmission.StatusID</c>.</summary>
    private static string ProjectSheetStatusName(int statusId) => statusId switch
    {
        Constants.TimesheetProjectSubmission.Status.Pending => "Pending",
        Constants.TimesheetProjectSubmission.Status.Submitted => "Submitted",
        Constants.TimesheetProjectSubmission.Status.Approved => "Approved",
        Constants.TimesheetProjectSubmission.Status.Rejected => "Rejected",
        _ => "Unknown",
    };

    #endregion

    public async Task<IReadOnlyList<TimesheetApproverSetupResponse>> GetApproverSetupListAsync(
        int? organizationId,
        int? projectId,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (organizationId < 0)
        {
            failures["orgID"] = new[] { "orgID must be greater than 0, or left out." };
        }

        if (projectId < 0)
        {
            failures["projectID"] = new[] { "projectID must be greater than 0, or left out." };
        }

        // Null and 0 both mean "not filtered". With neither filter the whole
        // table would come back, across every organisation - refused.
        var organizationFilter = organizationId is > 0 ? organizationId : null;
        var projectFilter = projectId is > 0 ? projectId : null;

        if (failures.Count == 0 && organizationFilter is null && projectFilter is null)
        {
            failures["orgID"] = new[] { "orgID or projectID is required." };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var setups = await _timesheetProjectRepository.GetApproverSetupListAsync(
            organizationFilter,
            projectFilter,
            cancellationToken);

        return setups.Select(setup => setup.ToResponse()).ToList();
    }

    public async Task<TimesheetApproverSetupResponse> SaveApproverSetupAsync(
        TimesheetApproverSetupSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every rule is checked here, before anything is written, and every
        // failure is collected - the caller is told about all of them in one
        // 400, keyed on the field names it sent.
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetApproverSetupId < 0)
        {
            failures[nameof(TimesheetApproverSetupSaveRequest.TimesheetApproverSetupId)] = new[]
            {
                "TimesheetApproverSetupId must be 0 (or left out) to add a setup, or the id of the setup to edit."
            };
        }

        if (request.OrganizationId <= 0)
        {
            failures[nameof(TimesheetApproverSetupSaveRequest.OrganizationId)] = new[]
            {
                "OrganizationId is required and must be greater than 0."
            };
        }

        if (request.ProjectId <= 0)
        {
            failures[nameof(TimesheetApproverSetupSaveRequest.ProjectId)] = new[]
            {
                "ProjectId is required and must be greater than 0."
            };
        }

        if (request.ApproverId <= 0)
        {
            failures[nameof(TimesheetApproverSetupSaveRequest.ApproverId)] = new[]
            {
                "ApproverId is required and must be greater than 0."
            };
        }

        if (request.LoginUserId <= 0)
        {
            failures[nameof(TimesheetApproverSetupSaveRequest.LoginUserId)] = new[]
            {
                "LoginUserId is required: the row records who created it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Then the database. No foreign key protects any of these columns, and
        // the procedure checks none of them, so a wrong id would be saved as is.
        var project = await _timesheetProjectRepository.GetByIdAsync(request.ProjectId, cancellationToken);

        if (project is null || project.OrganizationId != request.OrganizationId)
        {
            failures[nameof(TimesheetApproverSetupSaveRequest.ProjectId)] = new[]
            {
                $"Project '{request.ProjectId}' does not exist in organization '{request.OrganizationId}'."
            };
        }

        if (!await _timesheetProjectRepository.UserExistsAsync(request.ApproverId, request.OrganizationId, cancellationToken))
        {
            failures[nameof(TimesheetApproverSetupSaveRequest.ApproverId)] = new[]
            {
                $"User '{request.ApproverId}' is not a live user of organization '{request.OrganizationId}'."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Null and 0 both mean "add" - a negative id was refused above.
        var timesheetApproverSetupId = request.TimesheetApproverSetupId is > 0 ? request.TimesheetApproverSetupId : null;

        // One approver per project. The setup being edited is excluded, so an
        // edit that keeps its project is not a conflict with itself. The
        // procedure checks again under a lock, for two saves racing.
        if (await _timesheetProjectRepository.ApproverSetupExistsForProjectAsync(
                request.ProjectId,
                timesheetApproverSetupId ?? 0,
                cancellationToken))
        {
            throw new ConflictException(
                $"Project '{request.ProjectId}' already has an approver. Edit that setup instead.");
        }

        // Only an edit can come back empty: the setup it names does not
        // exist, is deleted, or belongs to another organisation.
        var saved = await _timesheetProjectRepository.SaveApproverSetupAsync(
                timesheetApproverSetupId,
                request.OrganizationId,
                request.ProjectId,
                request.ApproverId,
                request.IsTimesheetApprover,
                request.LoginUserId,
                _dateTimeProvider.UtcNow,
                cancellationToken)
            ?? throw NotFoundException.For("Approver setup", timesheetApproverSetupId ?? 0);

        _logger.LogInformation(
            "Approver setup {TimesheetApproverSetupId} {Action} for project {ProjectId} of organization {OrganizationId} by user {LoginUserId}.",
            saved.TimesheetApproverSetupId,
            timesheetApproverSetupId is null ? "added" : "updated",
            request.ProjectId,
            request.OrganizationId,
            request.LoginUserId);

        return saved.ToResponse();
    }

    public async Task DeleteApproverSetupAsync(
        TimesheetApproverSetupDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetApproverSetupId <= 0)
        {
            failures[nameof(TimesheetApproverSetupDeleteRequest.TimesheetApproverSetupId)] = new[]
            {
                "TimesheetApproverSetupId is required and must be greater than 0."
            };
        }

        if (request.DeletedBy <= 0)
        {
            failures[nameof(TimesheetApproverSetupDeleteRequest.DeletedBy)] = new[]
            {
                "DeletedBy is required: a soft delete records who performed it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // The repository hides deleted setups, so one deleted already is a
        // 404 - never re-stamped over the first DeletedDate.
        var setup = await _timesheetProjectRepository.GetApproverSetupForUpdateAsync(
                request.TimesheetApproverSetupId,
                cancellationToken)
            ?? throw NotFoundException.For("Approver setup", request.TimesheetApproverSetupId);

        // A soft delete, never a DELETE statement: the row stays, and the
        // entity's query filter and both approver procedures stop returning it.
        setup.IsDelete = true;
        setup.DeletedDate = _dateTimeProvider.UtcNow;
        setup.DeletedBy = request.DeletedBy;

        await _timesheetProjectRepository.UpdateApproverSetupAsync(setup, cancellationToken);

        _logger.LogInformation(
            "Approver setup {TimesheetApproverSetupId} of project {ProjectId} soft-deleted by user {DeletedBy}.",
            setup.TimesheetApproverSetupId,
            setup.ProjectId,
            request.DeletedBy);
    }
}
