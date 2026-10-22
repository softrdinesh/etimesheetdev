using System.Globalization;
using System.Net;
using System.Net.Mail;
using ETimeSheet.Application.Common;
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
    /// <c>ProjectlocLongtitude</c> - and the procedure's <c>@Projectloclatitude</c>
    /// and <c>@Projectloclongtitude</c> - are <c>nvarchar(25)</c>.
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

    /// <summary>The email to a project's approver when a sheet is submitted, in <c>Infrastructure/Assets/EmailTemplates/</c>.</summary>
    private const string ProjectSheetSubmittedEmailTemplateName = "ProjectSheetSubmittedTemplate.html";

    private const string ProjectSheetSubmittedEmailSubject = "Timesheet Submitted for Approval";

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
        var longitude = string.IsNullOrWhiteSpace(request.ProjectLocationLongitude) ? null : request.ProjectLocationLongitude.Trim();
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
        AddCoordinateFailure(failures, nameof(TimesheetUserCreateRequest.ProjectLocationLongitude), longitude, 180m);

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
            longitude,
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

        // Each day's submission period, from its project's start date and
        // frequency. A project since stripped of either has no period to give.
        var projects = (await _timesheetProjectRepository.GetListByIdsAsync(
                timeLogs.Select(timeLog => timeLog.ProjectId).Distinct().ToList(),
                cancellationToken))
            .ToDictionary(project => project.ProjectId);

        return timeLogs
            .Select(timeLog =>
            {
                if (projects.TryGetValue(timeLog.ProjectId, out var project)
                    && project.StartDate is { } projectStartDate
                    && project.SubmissionFrequency is { } submissionFrequency
                    && SubmissionFrequencies.Contains(submissionFrequency)
                    && timeLog.LogDate.Date >= projectStartDate.Date)
                {
                    var (periodStartDate, periodEndDate) = SubmissionPeriodOf(
                        timeLog.LogDate.Date,
                        projectStartDate.Date,
                        submissionFrequency);

                    return timeLog.ToResponse(periodStartDate, periodEndDate);
                }

                return timeLog.ToResponse(null, null);
            })
            .ToList();
    }

    public async Task<TimesheetProjectTimeLogResponse> SaveProjectTimeLogAsync(
        TimesheetProjectTimeLogSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        // Every rule on the payload itself is checked before anything is read,
        // and every failure is reported together.
        var failures = new Dictionary<string, string[]>();

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

        if (request.HoursLogged <= 0)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.HoursLogged)] = new[]
            {
                "HoursLogged is required and must be greater than 0."
            };
        }
        else if (request.HoursLogged > MaxLogHoursPerDayMaximum)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.HoursLogged)] = new[]
            {
                $"HoursLogged must not be greater than {MaxLogHoursPerDayMaximum}: a day holds no more hours."
            };
        }

        if (request.AddedBy <= 0)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.AddedBy)] = new[]
            {
                "AddedBy is required: the entry records who created or last updated it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var logDate = request.LogDate.Date;
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var project = await _timesheetProjectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new ValidationException(
                nameof(TimesheetProjectTimeLogSaveRequest.ProjectId),
                $"Project '{request.ProjectId}' does not exist.");

        // The start date and frequency give the submission period the day is
        // in, and so its sheet code; OrganizationID is NOT NULL on the time log.
        var (projectStartDate, submissionFrequency, organizationId) = RequireSubmissionSetup(project);

        if (logDate < projectStartDate)
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

        // A submitted or approved period is closed; a rejected one is open
        // again, and changing it leaves its sheet as it is until resubmitted.
        var submission = await _timesheetProjectRepository.GetProjectSubmissionForDateAsync(
            request.UserId,
            request.ProjectId,
            logDate,
            cancellationToken);

        RequireProjectSheetIsOpen(submission);

        // The day's cap is the assignment's Max_log_hour_day; an assignment
        // with none is capped only by the day itself.
        var maxHoursPerDay = await _timesheetProjectRepository.GetUserProjectMaxLogHoursPerDayAsync(
                request.UserId,
                request.ProjectId,
                cancellationToken)
            ?? MaxLogHoursPerDayMaximum;

        // One row per user, project and day: a later log of the same day adds
        // its hours to the row the first one created.
        var timeLog = await _timesheetProjectRepository.GetProjectTimeLogForDayForUpdateAsync(
            request.UserId,
            request.ProjectId,
            logDate,
            cancellationToken);

        var alreadyLogged = timeLog?.HoursLogged ?? 0;

        if (alreadyLogged + request.HoursLogged > maxHoursPerDay)
        {
            var remaining = Math.Max(maxHoursPerDay - alreadyLogged, 0);

            throw new ValidationException(
                nameof(TimesheetProjectTimeLogSaveRequest.HoursLogged),
                $"You can log a maximum of {Hours(maxHoursPerDay)} per day on this project. " +
                $"{Hours(alreadyLogged)} {(alreadyLogged == 1 ? "is" : "are")} already logged on {logDate:yyyy-MM-dd}, " +
                (remaining > 0
                    ? $"so you can log only {Hours(remaining)} more."
                    : "so no more hours can be logged on that day."));
        }

        var now = _dateTimeProvider.UtcNow;
        var isNewDay = timeLog is null;
        var (periodStartDate, periodEndDate) = SubmissionPeriodOf(logDate, projectStartDate, submissionFrequency);

        if (timeLog is null)
        {
            timeLog = new TimesheetProjectTimeLog
            {
                UserId = request.UserId,
                ProjectId = request.ProjectId,
                OrganizationId = organizationId,
                SheetSubmissionCode = await ProjectTimeLogSheetCodeAsync(
                    submission,
                    request.UserId,
                    request.ProjectId,
                    periodStartDate,
                    periodEndDate,
                    cancellationToken),
                LogDate = logDate,
                HoursLogged = request.HoursLogged,
                Description = description,
                IsDelete = 0,
                CreateDate = now,
                CreatedBy = request.AddedBy,
            };
        }
        else
        {
            timeLog.HoursLogged += request.HoursLogged;

            // Each log's description is kept, one per line, so the morning's
            // note is not lost to the afternoon's.
            if (description is not null)
            {
                timeLog.Description = string.IsNullOrWhiteSpace(timeLog.Description)
                    ? description
                    : timeLog.Description + "\n" + description;
            }

            timeLog.UpdateDate = now;
            timeLog.UpdatedBy = request.AddedBy;
        }

        var saved = await _timesheetProjectRepository.SaveProjectTimeLogAsync(timeLog, cancellationToken);

        _logger.LogInformation(
            "Project time log {TimesheetProjectTimeLogId} {Action} for user {UserId} on project {ProjectId}: " +
            "{HoursAdded} hours added on {LogDate:yyyy-MM-dd}, {HoursLogged} hours in total, sheet " +
            "{SheetSubmissionCode}, by user {AddedBy}.",
            saved.TimesheetProjectTimeLogId,
            isNewDay ? "added" : "updated",
            request.UserId,
            request.ProjectId,
            request.HoursLogged,
            logDate,
            saved.HoursLogged,
            saved.SheetSubmissionCode,
            request.AddedBy);

        return saved.ToResponse(periodStartDate, periodEndDate);
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

        if (timeLog.UserId != request.UserId)
        {
            throw new BusinessException(
                $"Time log '{timeLog.TimesheetProjectTimeLogId}' does not belong to user '{request.UserId}', so it " +
                "cannot be removed as theirs.");
        }

        RequireProjectSheetIsOpen(await _timesheetProjectRepository.GetProjectSubmissionForDateAsync(
            timeLog.UserId,
            timeLog.ProjectId,
            timeLog.LogDate,
            cancellationToken));

        // A soft delete: the row stays, and the query filter stops returning it.
        timeLog.IsDelete = 1;
        timeLog.DeletedDate = _dateTimeProvider.UtcNow;
        timeLog.DeletedBy = request.AddedBy;

        await _timesheetProjectRepository.SaveProjectTimeLogAsync(timeLog, cancellationToken);

        _logger.LogInformation(
            "Project time log {TimesheetProjectTimeLogId} of user {UserId} on project {ProjectId} for " +
            "{LogDate:yyyy-MM-dd} soft-deleted by user {AddedBy}.",
            timeLog.TimesheetProjectTimeLogId,
            timeLog.UserId,
            timeLog.ProjectId,
            timeLog.LogDate,
            request.AddedBy);
    }

    /// <summary>
    /// The sheet code of a user's time on a project in one submission period:
    /// the submitted sheet's code when the period has one; else the code
    /// already on any of their days in the period - a deleted day included,
    /// since its code is spent; else, for the period's first day or a blank
    /// sheet, the next code in the sequence. Every day of one user, project and
    /// period so shares one code with its sheet, whichever came first.
    /// </summary>
    private async Task<string> ProjectTimeLogSheetCodeAsync(
        TimesheetProjectSubmission? submission,
        int userId,
        int projectId,
        DateTime periodStartDate,
        DateTime periodEndDate,
        CancellationToken cancellationToken) =>
        submission?.SheetSubmissionCode
        ?? await _timesheetProjectRepository.GetProjectTimeLogSheetCodeForPeriodAsync(
            userId,
            projectId,
            periodStartDate,
            periodEndDate,
            cancellationToken)
        ?? NextProjectSheetCode(
            await _timesheetProjectRepository.GetLatestProjectSheetCodeAsync(cancellationToken));

    /// <summary>
    /// Rejects a change to time in a period whose sheet is submitted or
    /// approved. No sheet, or a rejected one, is open.
    /// </summary>
    private static void RequireProjectSheetIsOpen(TimesheetProjectSubmission? submission)
    {
        if (submission?.StatusId is Constants.TimesheetProjectSubmission.Status.Submitted
            or Constants.TimesheetProjectSubmission.Status.Approved)
        {
            throw new BusinessException(
                $"Timesheet '{submission.SheetSubmissionCode}' for {submission.PeriodStartDate:yyyy-MM-dd} to " +
                $"{submission.PeriodEndDate:yyyy-MM-dd} is {ProjectSheetStatusName(submission.StatusId).ToLowerInvariant()}, " +
                "so its time can no longer be added to, changed or removed.");
        }
    }

    /// <summary>
    /// The user's clock, in the time zone of their timesheet setup. No setup,
    /// or a zone this system does not recognise, falls back to UTC - logged in
    /// the second case, as <c>TimeLogService</c> does, so the zone gets fixed.
    /// </summary>
    private async Task<EmployeeClock> ClockForUserAsync(int userId, CancellationToken cancellationToken)
    {
        var timeZone = await _timesheetProjectRepository.GetUserTimeZoneAsync(userId, cancellationToken);
        var zone = EmployeeClock.FindZone(timeZone);

        if (zone is null && !string.IsNullOrWhiteSpace(timeZone))
        {
            _logger.LogWarning(
                "Timesheet setup of user {UserId} names time zone {TimeZone}, which this system does not " +
                "recognise. Project timesheet periods are judged on UTC.",
                userId,
                timeZone);
        }

        return EmployeeClock.At(_dateTimeProvider.UtcNow, zone);
    }

    /// <summary>A number of hours as words: "1 hour", "7 hours".</summary>
    private static string Hours(int hours) => hours == 1 ? "1 hour" : $"{hours} hours";

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

        // A sheet exists only once its period is submitted, so the stored rows
        // are the whole list: nothing is worked out for periods without one.
        var stored = await _timesheetProjectRepository.GetProjectSheetListAsync(
            userFilter,
            projectFilter,
            organizationFilter,
            cancellationToken);

        var projects = (await _timesheetProjectRepository.GetListByIdsAsync(
                stored.Select(sheet => sheet.ProjectId).Distinct().ToList(),
                cancellationToken))
            .ToDictionary(project => project.ProjectId);

        return stored
            .Select(sheet => sheet.ToResponse(
                projects.GetValueOrDefault(sheet.ProjectId),
                ProjectSheetStatusName(sheet.StatusId)))
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

        if (sheetSubmissionCode is null && request.PeriodDate is null)
        {
            failures[nameof(TimesheetProjectSheetSubmitRequest.PeriodDate)] = new[]
            {
                "SheetSubmissionCode or PeriodDate is required: either names the period to submit."
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

        var project = await _timesheetProjectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new ValidationException(
                nameof(TimesheetProjectSheetSubmitRequest.ProjectId),
                $"Project '{request.ProjectId}' does not exist.");

        var (projectStartDate, submissionFrequency, organizationId) = RequireSubmissionSetup(project);

        // The period is never sent: it is the one the code belongs to - a
        // submitted sheet's, for a resubmit, or the time log's - or the one
        // containing PeriodDate. When both are sent they must agree.
        TimesheetProjectSubmission? submission = null;
        DateTime dayInPeriod;

        if (sheetSubmissionCode is not null)
        {
            submission = await _timesheetProjectRepository.GetProjectSubmissionByCodeForUpdateAsync(
                sheetSubmissionCode,
                cancellationToken);

            if (submission is not null
                && (submission.UserId != request.UserId || submission.ProjectId != request.ProjectId))
            {
                throw new BusinessException(
                    $"Timesheet '{submission.SheetSubmissionCode}' is not user '{request.UserId}''s timesheet for " +
                    $"project '{request.ProjectId}', so it cannot be submitted as theirs.");
            }

            dayInPeriod = submission?.PeriodStartDate.Date
                ?? await _timesheetProjectRepository.GetProjectTimeLogDateForCodeAsync(
                    request.UserId,
                    request.ProjectId,
                    sheetSubmissionCode,
                    cancellationToken)
                ?? throw new ValidationException(
                    nameof(TimesheetProjectSheetSubmitRequest.SheetSubmissionCode),
                    $"SheetSubmissionCode '{sheetSubmissionCode}' is not a timesheet of user '{request.UserId}' " +
                    $"on project '{request.ProjectId}'.");

            if (request.PeriodDate is { } periodDate
                && (periodDate.Date < projectStartDate
                    || SubmissionPeriodOf(periodDate.Date, projectStartDate, submissionFrequency).Start
                    != SubmissionPeriodOf(dayInPeriod, projectStartDate, submissionFrequency).Start))
            {
                throw new ValidationException(
                    nameof(TimesheetProjectSheetSubmitRequest.PeriodDate),
                    $"PeriodDate {periodDate:yyyy-MM-dd} is not in the period of timesheet '{sheetSubmissionCode}'.");
            }
        }
        else
        {
            dayInPeriod = request.PeriodDate!.Value.Date;
        }

        if (dayInPeriod < projectStartDate)
        {
            throw new ValidationException(
                nameof(TimesheetProjectSheetSubmitRequest.PeriodDate),
                $"PeriodDate must not be before the project's start date, {projectStartDate:yyyy-MM-dd}.");
        }

        var (periodStartDate, periodEndDate) = SubmissionPeriodOf(dayInPeriod, projectStartDate, submissionFrequency);

        if (project.EndDate is { } projectEndDate && periodStartDate > projectEndDate.Date)
        {
            throw new ValidationException(
                nameof(TimesheetProjectSheetSubmitRequest.PeriodDate),
                $"PeriodDate must not be after the project's end date, {projectEndDate:yyyy-MM-dd}.");
        }

        // A period can be submitted on its last day or any day after - judged
        // on the user's own clock, so a user ahead of UTC is not held back.
        var clock = await ClockForUserAsync(request.UserId, cancellationToken);

        if (clock.Today < periodEndDate)
        {
            throw new BusinessException(
                $"The timesheet for {periodStartDate:yyyy-MM-dd} to {periodEndDate:yyyy-MM-dd} can be submitted " +
                $"from {periodEndDate:yyyy-MM-dd}, the period's last day. Today is {clock.Today:yyyy-MM-dd} " +
                $"({clock.ZoneName}).");
        }

        // One row per user, project and period.
        submission ??= await _timesheetProjectRepository.GetProjectSubmissionForPeriodForUpdateAsync(
            request.UserId,
            request.ProjectId,
            periodStartDate,
            cancellationToken);

        // Pending is a row from before submission created its own rows;
        // Rejected is a resubmit after changes.
        if (submission is not null
            && submission.StatusId is not (Constants.TimesheetProjectSubmission.Status.Pending
                or Constants.TimesheetProjectSubmission.Status.Rejected))
        {
            throw new ConflictException(
                $"Timesheet '{submission.SheetSubmissionCode}' is already {ProjectSheetStatusName(submission.StatusId)}.");
        }

        var now = _dateTimeProvider.UtcNow;

        // The totals as they stand in the time log, now. No time is a blank
        // sheet - allowed.
        var minutes = await _timesheetProjectRepository.GetProjectLoggedMinutesForPeriodAsync(
            request.UserId,
            request.ProjectId,
            periodStartDate,
            periodEndDate,
            cancellationToken);

        if (submission is null)
        {
            if (!await _timesheetProjectRepository.UserProjectSetupExistsAsync(
                    request.UserId,
                    request.ProjectId,
                    0,
                    cancellationToken))
            {
                throw new BusinessException(
                    $"User '{request.UserId}' is not assigned to project '{request.ProjectId}', so they have no " +
                    "timesheet on it to submit.");
            }

            // The first submit of the period is what creates its row - already
            // Submitted, with its audit row, in one save. The code is the one
            // the period's time log carries, or the next one for a blank sheet.
            submission = await _timesheetProjectRepository.AddProjectSubmissionAsync(
                new TimesheetProjectSubmission
                {
                    SheetSubmissionCode = await ProjectTimeLogSheetCodeAsync(
                        null,
                        request.UserId,
                        request.ProjectId,
                        periodStartDate,
                        periodEndDate,
                        cancellationToken),
                    StatusId = Constants.TimesheetProjectSubmission.Status.Submitted,
                    UserId = request.UserId,
                    ProjectId = request.ProjectId,
                    OrganizationId = organizationId,
                    PeriodStartDate = periodStartDate,
                    PeriodEndDate = periodEndDate,
                    TotalMinutesLogged = minutes,
                    TotalHoursLogged = TimesheetProjectMappings.ToHours(minutes),
                    SubmittedBy = request.AddedBy,
                    SubmittedDate = now,
                    IsDelete = 0,
                    DateAdded = now,
                },
                new TimesheetProjectSubmissionAuditLog
                {
                    StatusId = Constants.TimesheetProjectSubmission.Status.Submitted,
                    CreatedBy = request.AddedBy,
                    CreateDate = now,
                },
                cancellationToken);
        }
        else
        {
            submission.TotalMinutesLogged = minutes;
            submission.TotalHoursLogged = TimesheetProjectMappings.ToHours(minutes);
            submission.StatusId = Constants.TimesheetProjectSubmission.Status.Submitted;
            submission.SubmittedBy = request.AddedBy;
            submission.SubmittedDate = now;

            await SaveProjectSheetStatusAsync(submission, request.AddedBy, now, cancellationToken);
        }

        _logger.LogInformation(
            "Timesheet {SheetSubmissionCode} of user {UserId} on project {ProjectId}, {PeriodStart:yyyy-MM-dd} to " +
            "{PeriodEnd:yyyy-MM-dd}, submitted by user {AddedBy} with {Minutes} minutes.",
            submission.SheetSubmissionCode,
            request.UserId,
            request.ProjectId,
            periodStartDate,
            periodEndDate,
            request.AddedBy,
            minutes);

        await SendProjectSheetSubmittedEmailAsync(submission);

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
    /// Emails the project's approver - the live setup in
    /// <c>dbo.Timesheet_Approver_setup</c> with <c>IsTimesheetApprover</c> = 1 -
    /// that a sheet was submitted, directly through the email service. Never
    /// throws: the sheet is submitted by now, and failing the request would
    /// tell the user it was not. A project with no approver, an approver with
    /// no email, or a failed send is logged and skipped.
    /// </summary>
    private async Task SendProjectSheetSubmittedEmailAsync(TimesheetProjectSubmission submission)
    {
        try
        {
            // CancellationToken.None throughout: the sheet is already
            // submitted, so a caller that disconnects now must not cost the
            // approver their email. The email service's own timeout still
            // bounds the wait.
            var approver = (await _timesheetProjectRepository.GetApproverSetupListAsync(
                    null,
                    submission.ProjectId,
                    CancellationToken.None))
                .FirstOrDefault(setup => setup.IsTimesheetApprover == true);

            if (approver is null || string.IsNullOrWhiteSpace(approver.ApproverEmail))
            {
                _logger.LogWarning(
                    "Timesheet {SheetSubmissionCode} submitted, but project {ProjectId} has no timesheet approver with an email; no email sent.",
                    submission.SheetSubmissionCode,
                    submission.ProjectId);
                return;
            }

            var employee = await _timesheetProjectRepository.GetUserByIdAsync(submission.UserId, CancellationToken.None);
            var employeeName = employee?.Name ?? string.Empty;

            var body = await _emailTemplateStore.GetAsync(ProjectSheetSubmittedEmailTemplateName, CancellationToken.None);

            // Every value is HTML-encoded: a name with a '<' in it is text, not markup.
            var values = new Dictionary<string, string>
            {
                ["ApproverName"] = approver.ApproverName ?? string.Empty,
                ["EmployeeName"] = employeeName,
                ["Period"] =
                    $"{submission.PeriodStartDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} - " +
                    $"{submission.PeriodEndDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}",
            };

            foreach (var (placeholder, value) in values)
            {
                body = body.Replace($"[{placeholder}]", WebUtility.HtmlEncode(value), StringComparison.Ordinal);
            }

            var result = await _emailSender.SendAsync(
                new EmailMessage(approver.ApproverEmail, ProjectSheetSubmittedEmailSubject, body),
                CancellationToken.None);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "Submission email for timesheet {SheetSubmissionCode} to approver {ApproverId} was not sent: {Error}",
                    submission.SheetSubmissionCode,
                    approver.ApproverId,
                    result.Error);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Submission email for timesheet {SheetSubmissionCode} could not be built or sent.",
                submission.SheetSubmissionCode);
        }
    }

    /// <summary><c>dbo.TimesheetProjectSubmission.RejectionReason</c> is <c>nvarchar(500)</c>.</summary>
    private const int RejectionReasonMaximumLength = 500;

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

        return sheet.ToResponse(project, ProjectSheetStatusName(sheet.StatusId));
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
