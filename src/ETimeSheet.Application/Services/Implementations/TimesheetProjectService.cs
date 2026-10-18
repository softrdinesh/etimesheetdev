using System.Globalization;
using System.Net;
using System.Net.Mail;
using ETimeSheet.Application.Common.Mapping;
using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Interfaces.Services;
using ETimeSheet.Application.Models;
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
}
