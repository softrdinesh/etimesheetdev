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

    /// <summary><c>dbo.TimesheetProjectSubmission.RejectionReason</c> is <c>nvarchar(500)</c>.</summary>
    private const int RejectionReasonMaximumLength = 500;

    /// <summary>
    /// What every generated project sheet code starts with. The user's id is
    /// part of the code on purpose - see
    /// <see cref="NextSheetSubmissionCode"/>.
    /// </summary>
    private const string SheetSubmissionCodePrefix = "PSH";

    /// <summary>
    /// How many digits the sequence starts with, which is what makes a user's
    /// first sheet <c>PSH-58-0001</c> rather than <c>PSH-58-1</c>. It is a
    /// minimum, not a limit: a user past 9999 sheets gets a longer number, and
    /// <c>PSH-2147483647-10000</c> still fits the column's 20 characters.
    /// </summary>
    private const int SheetSubmissionCodeInitialDigits = 4;

    /// <summary>The values <c>dbo.TimesheetProjectTimeLog.Status</c> accepts.</summary>
    private static readonly IReadOnlySet<int> TimeLogStatuses = new HashSet<int>
    {
        Constants.TimeLog.Status.Save,
        Constants.TimeLog.Status.Draft,
    };

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

    // =====================================================================
    //  PROJECT TIME LOG
    // =====================================================================

    public async Task<IReadOnlyList<TimesheetProjectTimeLogResponse>> GetProjectTimeLogListAsync(
        int? userId,
        int? projectId,
        string? sheetSubmissionCode,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var code = string.IsNullOrWhiteSpace(sheetSubmissionCode) ? null : sheetSubmissionCode.Trim();
        var failures = new Dictionary<string, string[]>();

        if (userId < 0)
        {
            failures["userID"] = new[] { "userID must be greater than 0, or left out." };
        }

        if (projectId < 0)
        {
            failures["projectID"] = new[] { "projectID must be greater than 0, or left out." };
        }

        // 0 means "not filtered", the same as leaving it out.
        var userFilter = userId is > 0 ? userId : null;
        var projectFilter = projectId is > 0 ? projectId : null;

        // The whole table is never returned: without a filter this would hand
        // every organisation's time log to any caller, and authentication is
        // off.
        if (failures.Count == 0 && userFilter is null && projectFilter is null && code is null)
        {
            failures["userID"] = new[] { "userID, projectID or sheetSubmissionCode is required." };
        }

        if (fromDate is { } from && toDate is { } to && to < from)
        {
            failures["toDate"] = new[] { "toDate must not be before fromDate." };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Dates only: the column is a date, so any time part sent would
        // otherwise exclude the whole of toDate.
        var entries = await _timesheetProjectRepository.GetProjectTimeLogListAsync(
            userFilter,
            projectFilter,
            code,
            fromDate?.Date,
            toDate?.Date,
            cancellationToken);

        if (entries.Count == 0)
        {
            return entries;
        }

        // One query for every code on the page, rather than one per row.
        var submitted = await _timesheetProjectRepository.GetSubmittedSheetCodesAsync(
            entries.Select(entry => entry.SheetSubmissionCode).Distinct().ToArray(),
            cancellationToken);

        foreach (var entry in entries)
        {
            entry.StatusName = entry.Status.ToString();
            entry.IsSubmitted = submitted.Contains(entry.SheetSubmissionCode);
        }

        return entries;
    }

    public async Task<TimesheetProjectTimeLogResponse> SaveProjectTimeLogAsync(
        TimesheetProjectTimeLogSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetProjectTimeLogId < 0)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.TimesheetProjectTimeLogId)] = new[]
            {
                "TimesheetProjectTimeLogId must be 0 (or left out) to log a new entry, or the id of the entry to edit."
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

        if (request.LogDate is null)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.LogDate)] = new[] { "LogDate is required." };
        }

        // Times arrive as hh:mm:ss strings and are read by the one parser every
        // endpoint uses. TryParse rather than TimeOfDay.Parse because this
        // module reports every failing field together, and Parse throws on the
        // first - the message is the shared one either way.
        var startTime = ReadTimeOfDay(
            failures,
            request.StartTime,
            nameof(TimesheetProjectTimeLogSaveRequest.StartTime));

        var endTime = ReadTimeOfDay(
            failures,
            request.EndTime,
            nameof(TimesheetProjectTimeLogSaveRequest.EndTime));

        // An entry that starts and ends at the same moment has no duration to
        // work out, so it is a mistake rather than a zero-length block.
        if (startTime is { } from && endTime is { } to && from == to)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.EndTime)] = new[]
            {
                "EndTime must not be the same as StartTime: an entry has to cover some time."
            };
        }

        if (request.Status is not { } status)
        {
            failures[nameof(TimesheetProjectTimeLogSaveRequest.Status)] = new[]
            {
                "Status is required: 1 (Save) or 2 (Draft)."
            };
        }
        else if (!TimeLogStatuses.Contains((int)status))
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
                "AddedBy is required: the entry records who logged or last changed it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Worked out, never sent: the stored figure and the clock times cannot
        // disagree if only one of them is an input.
        var durationMinutes = MinutesBetween(startTime!.Value, endTime!.Value);

        // Then the database. No foreign key protects UserID or ProjectID, so
        // without this an entry could name a pair that was never assigned.
        if (!await _timesheetProjectRepository.UserProjectAssignmentExistsAsync(
                request.UserId,
                request.ProjectId,
                cancellationToken))
        {
            throw new ValidationException(
                nameof(TimesheetProjectTimeLogSaveRequest.UserId),
                $"User '{request.UserId}' has no live assignment to project '{request.ProjectId}'.");
        }

        // Null and 0 both mean "add" - a negative id was refused above.
        var timesheetProjectTimeLogId = request.TimesheetProjectTimeLogId is > 0
            ? request.TimesheetProjectTimeLogId
            : null;

        var now = _dateTimeProvider.UtcNow;
        TimesheetProjectTimeLog entry;

        if (timesheetProjectTimeLogId is { } editedId)
        {
            entry = await _timesheetProjectRepository.GetProjectTimeLogForUpdateAsync(editedId, cancellationToken)
                ?? throw NotFoundException.For("Project time log entry", editedId);

            // An entry never moves between sheets: it stays on the one it was
            // logged against, so editing it cannot change which submission it
            // belongs to. All that is checked is that that sheet is still open.
            await RequireSheetIsOpenAsync(entry.SheetSubmissionCode, cancellationToken);

            // Everything else is overwritten as sent - a field left out is
            // saved as null, not left as it was.
            entry.UserId = request.UserId;
            entry.ProjectId = request.ProjectId;
            entry.LogDate = request.LogDate!.Value.Date;
            entry.StartTime = startTime;
            entry.EndTime = endTime;
            entry.DurationMinutes = durationMinutes;
            entry.Description = description;
            entry.Status = (int)request.Status!.Value;
            entry.UpdateDate = now;
            entry.UpdatedBy = request.AddedBy;

            await _timesheetProjectRepository.UpdateProjectTimeLogAsync(entry, cancellationToken);
        }
        else
        {
            entry = new TimesheetProjectTimeLog
            {
                UserId = request.UserId,
                ProjectId = request.ProjectId,
                SheetSubmissionCode = await OpenSheetCodeForUserAsync(request.UserId, cancellationToken),
                LogDate = request.LogDate!.Value.Date,
                StartTime = startTime,
                EndTime = endTime,
                DurationMinutes = durationMinutes,
                Description = description,
                Status = (int)request.Status!.Value,

                // Both columns are NOT NULL with no default, so the insert has
                // to say so itself.
                IsDelete = 0,
                CreateDate = now,
                CreatedBy = request.AddedBy,
            };

            entry = await _timesheetProjectRepository.AddProjectTimeLogAsync(entry, cancellationToken);
        }

        _logger.LogInformation(
            "Project time log entry {TimesheetProjectTimeLogId} {Action} for user {UserId} on project {ProjectId}, sheet {SheetSubmissionCode}, by user {PerformedBy}.",
            entry.TimesheetProjectTimeLogId,
            timesheetProjectTimeLogId is null ? "logged" : "edited",
            entry.UserId,
            entry.ProjectId,
            entry.SheetSubmissionCode,
            request.AddedBy);

        // Read back through the list query so the saved entry comes out in the
        // same shape, names included, as every other read of it.
        var saved = await GetProjectTimeLogListAsync(
            null,
            null,
            entry.SheetSubmissionCode,
            null,
            null,
            cancellationToken);

        return saved.FirstOrDefault(row => row.TimesheetProjectTimeLogId == entry.TimesheetProjectTimeLogId)
            ?? throw new InvalidOperationException(
                "The project time log entry was saved but cannot be read back.");
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

        if (request.DeletedBy <= 0)
        {
            failures[nameof(TimesheetProjectTimeLogDeleteRequest.DeletedBy)] = new[]
            {
                "DeletedBy is required: a soft delete records who performed it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Deleted already is not found, so removing twice is a 404 rather than
        // a silent success.
        var entry = await _timesheetProjectRepository.GetProjectTimeLogForUpdateAsync(
                request.TimesheetProjectTimeLogId,
                cancellationToken)
            ?? throw NotFoundException.For("Project time log entry", request.TimesheetProjectTimeLogId);

        await RequireSheetIsOpenAsync(entry.SheetSubmissionCode, cancellationToken);

        entry.IsDelete = 1;
        entry.DeletedDate = _dateTimeProvider.UtcNow;
        entry.DeletedBy = request.DeletedBy;

        await _timesheetProjectRepository.UpdateProjectTimeLogAsync(entry, cancellationToken);

        _logger.LogInformation(
            "Project time log entry {TimesheetProjectTimeLogId} on sheet {SheetSubmissionCode} soft-deleted by user {DeletedBy}.",
            entry.TimesheetProjectTimeLogId,
            entry.SheetSubmissionCode,
            request.DeletedBy);
    }

    // =====================================================================
    //  PROJECT SHEET SUBMISSION
    // =====================================================================

    public async Task<ProjectTimesheetCurrentResponse> GetCurrentProjectTimesheetAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new ValidationException("userID", "userID is required and must be greater than 0.");
        }

        var code = await CurrentSheetCodeForUserAsync(userId, cancellationToken);

        // The sheet is the user's, so another user's entries on the same code
        // are not part of it. Nothing in the table prevents that, so it is
        // filtered rather than assumed.
        var entries = (await _timesheetProjectRepository.GetEntriesForSheetAsync(code, cancellationToken))
            .Where(entry => entry.UserId == userId)
            .ToList();

        var submission = await _timesheetProjectRepository.GetSubmissionByCodeForUpdateAsync(code, cancellationToken);
        var totalMinutes = entries.Sum(entry => entry.DurationMinutes);
        var awaitingReview = submission is not null
                             && submission.StatusId != Constants.TimesheetSubmission.Status.Rejected;

        foreach (var entry in entries)
        {
            entry.StatusName = entry.Status.ToString();
            entry.IsSubmitted = awaitingReview;
        }

        return new ProjectTimesheetCurrentResponse
        {
            UserId = userId,
            SheetSubmissionCode = code,
            PeriodStart = entries.Count == 0 ? null : entries.Min(entry => entry.LogDate),
            PeriodEnd = entries.Count == 0 ? null : entries.Max(entry => entry.LogDate),
            TotalMinutes = totalMinutes,
            TotalHours = ToHours(totalMinutes),
            EntryCount = entries.Count,

            // An empty sheet is submittable on purpose: handing in a period
            // with nothing logged is a statement, not a mistake.
            CanSubmit = !awaitingReview,

            Submission = submission is null ? null : ToResponse(submission),
            Projects = ProjectLinesOf(entries),
            Entries = entries,
        };
    }

    public async Task<ProjectTimesheetSubmissionResponse> SubmitProjectTimesheetAsync(
        ProjectTimesheetSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (request.UserId <= 0)
        {
            failures[nameof(ProjectTimesheetSubmitRequest.UserId)] = new[]
            {
                "UserId is required and must be greater than 0."
            };
        }

        if (request.SubmittedBy <= 0)
        {
            failures[nameof(ProjectTimesheetSubmitRequest.SubmittedBy)] = new[]
            {
                "SubmittedBy is required: the sheet records who handed it in."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Deliberately NOT OpenSheetCodeForUserAsync: that allocates a fresh
        // code when there is no open sheet, which is right for logging and
        // wrong here. Submitting with nothing open would open a brand new
        // sheet and immediately hand it in empty - so a double-clicked Submit
        // would leave a junk submission behind every time.
        var code = await _timesheetProjectRepository.GetOpenSheetCodeForUserAsync(request.UserId, cancellationToken);

        if (code is null)
        {
            var spentCodes = await _timesheetProjectRepository.GetSheetCodesForUserAsync(
                request.UserId,
                cancellationToken);

            // Nothing open and nothing ever logged is a user submitting their
            // very first sheet, empty. Nothing open but sheets behind them
            // means the last one is already in and nothing has been logged
            // since - there is nothing to submit.
            if (spentCodes.Count > 0)
            {
                throw new ConflictException(
                    $"User '{request.UserId}' has nothing to submit: their last sheet has already been handed in, " +
                    "and no time has been logged since.");
            }

            code = NextSheetSubmissionCode(request.UserId, spentCodes);
        }

        var entries = (await _timesheetProjectRepository.GetEntriesForSheetAsync(code, cancellationToken))
            .Where(entry => entry.UserId == request.UserId)
            .ToList();

        // dbo.TimesheetProjectSubmission has no UserID column: whose sheet it
        // is can only be read back from its entries, or from SubmittedBy. An
        // EMPTY sheet has no entries, so if somebody else submits it the row
        // records the wrong person and nothing can recover the right one.
        // Refused rather than stored wrong.
        if (entries.Count == 0 && request.SubmittedBy != request.UserId)
        {
            throw new ValidationException(
                nameof(ProjectTimesheetSubmitRequest.SubmittedBy),
                $"An empty sheet can only be submitted by the user it belongs to: the submission row has no column " +
                $"for whose sheet it is, so submitting user '{request.UserId}' empty sheet as user " +
                $"'{request.SubmittedBy}' would leave nothing to tie it back to them.");
        }

        var totalMinutes = entries.Sum(entry => entry.DurationMinutes);
        var now = _dateTimeProvider.UtcNow;

        var submission = await _timesheetProjectRepository.GetSubmissionByCodeForUpdateAsync(code, cancellationToken);

        if (submission is not null
            && submission.StatusId != Constants.TimesheetSubmission.Status.Rejected)
        {
            throw new ConflictException(
                $"Sheet '{code}' has already been submitted and is awaiting review.");
        }

        if (submission is null)
        {
            submission = await _timesheetProjectRepository.AddSubmissionAsync(
                new TimesheetProjectSubmission
                {
                    SheetSubmissionCode = code,
                    TotalMinutes = totalMinutes,
                    TotalHours = ToHours(totalMinutes),
                    StatusId = Constants.TimesheetSubmission.Status.Submitted,
                    SubmittedBy = request.SubmittedBy,
                    SubmittedDate = now,
                    IsDelete = 0,
                },
                cancellationToken);
        }
        else
        {
            // A rejected sheet is resubmitted in place rather than as a second
            // row, so one code never means two sheets. The rejection is not
            // lost: it is in the audit trail, which is append-only.
            submission.TotalMinutes = totalMinutes;
            submission.TotalHours = ToHours(totalMinutes);
            submission.StatusId = Constants.TimesheetSubmission.Status.Submitted;
            submission.SubmittedBy = request.SubmittedBy;
            submission.SubmittedDate = now;
            submission.RejectedBy = null;
            submission.RejectedDate = null;
            submission.RejectionReason = null;

            await _timesheetProjectRepository.UpdateSubmissionAsync(submission, cancellationToken);
        }

        await _timesheetProjectRepository.AddSubmissionAuditAsync(
            new TimesheetProjectSubmissionAuditLog
            {
                TimesheetProjectSubmissionId = submission.TimesheetProjectSubmissionId,
                StatusId = Constants.TimesheetSubmission.Status.Submitted,
                Remark = entries.Count == 0 ? "Submitted with no time logged." : null,
                CreatedBy = request.SubmittedBy,
                CreateDate = now,
            },
            cancellationToken);

        _logger.LogInformation(
            "Project sheet {SheetSubmissionCode} ({TimesheetProjectSubmissionId}) submitted for user {UserId} by user {SubmittedBy}: {EntryCount} entries, {TotalMinutes} minutes.",
            code,
            submission.TimesheetProjectSubmissionId,
            request.UserId,
            request.SubmittedBy,
            entries.Count,
            totalMinutes);

        return ToResponse(submission);
    }

    public async Task<ProjectTimesheetSubmissionResponse> UpdateProjectTimesheetStatusAsync(
        ProjectTimesheetStatusUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = string.IsNullOrWhiteSpace(request.RejectionReason) ? null : request.RejectionReason.Trim();
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetProjectSubmissionId <= 0)
        {
            failures[nameof(ProjectTimesheetStatusUpdateRequest.TimesheetProjectSubmissionId)] = new[]
            {
                "TimesheetProjectSubmissionId is required and must be greater than 0."
            };
        }

        // 1 (Submitted) is reached by submitting, never by reviewing.
        if (request.StatusId != Constants.TimesheetSubmission.Status.Approved
            && request.StatusId != Constants.TimesheetSubmission.Status.Rejected)
        {
            failures[nameof(ProjectTimesheetStatusUpdateRequest.StatusId)] = new[]
            {
                "StatusId must be 2 (Approved) or 3 (Rejected): a sheet is moved to 1 (Submitted) by submitting it."
            };
        }

        // A rejection a user cannot act on is a rejection that wastes their
        // time, so the reason is required rather than merely allowed.
        if (request.StatusId == Constants.TimesheetSubmission.Status.Rejected && reason is null)
        {
            failures[nameof(ProjectTimesheetStatusUpdateRequest.RejectionReason)] = new[]
            {
                "RejectionReason is required when rejecting a sheet: the user has to know what to correct."
            };
        }
        else if (reason?.Length > RejectionReasonMaximumLength)
        {
            failures[nameof(ProjectTimesheetStatusUpdateRequest.RejectionReason)] = new[]
            {
                $"RejectionReason must be {RejectionReasonMaximumLength} characters or fewer."
            };
        }

        if (request.ReviewedBy <= 0)
        {
            failures[nameof(ProjectTimesheetStatusUpdateRequest.ReviewedBy)] = new[]
            {
                "ReviewedBy is required: the sheet records who reviewed it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var submission = await _timesheetProjectRepository.GetSubmissionForUpdateAsync(
                request.TimesheetProjectSubmissionId,
                cancellationToken)
            ?? throw NotFoundException.For("Project sheet", request.TimesheetProjectSubmissionId);

        var now = _dateTimeProvider.UtcNow;

        // The stamps describe the CURRENT status, so the pair that no longer
        // applies is cleared as the new one is written. The history is in the
        // audit trail.
        if (request.StatusId == Constants.TimesheetSubmission.Status.Approved)
        {
            submission.StatusId = Constants.TimesheetSubmission.Status.Approved;
            submission.ApprovedBy = request.ReviewedBy;
            submission.ApprovedDate = now;
            submission.RejectedBy = null;
            submission.RejectedDate = null;
            submission.RejectionReason = null;
        }
        else
        {
            submission.StatusId = Constants.TimesheetSubmission.Status.Rejected;
            submission.RejectedBy = request.ReviewedBy;
            submission.RejectedDate = now;
            submission.RejectionReason = reason;
            submission.ApprovedBy = null;
            submission.ApprovedDate = null;
        }

        await _timesheetProjectRepository.UpdateSubmissionAsync(submission, cancellationToken);

        await _timesheetProjectRepository.AddSubmissionAuditAsync(
            new TimesheetProjectSubmissionAuditLog
            {
                TimesheetProjectSubmissionId = submission.TimesheetProjectSubmissionId,
                StatusId = request.StatusId,
                Remark = reason,
                CreatedBy = request.ReviewedBy,
                CreateDate = now,
            },
            cancellationToken);

        _logger.LogInformation(
            "Project sheet {SheetSubmissionCode} ({TimesheetProjectSubmissionId}) moved to status {StatusId} by user {ReviewedBy}.",
            submission.SheetSubmissionCode,
            submission.TimesheetProjectSubmissionId,
            request.StatusId,
            request.ReviewedBy);

        return ToResponse(submission);
    }

    public async Task DeleteProjectTimesheetAsync(
        ProjectTimesheetDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var failures = new Dictionary<string, string[]>();

        if (request.TimesheetProjectSubmissionId <= 0)
        {
            failures[nameof(ProjectTimesheetDeleteRequest.TimesheetProjectSubmissionId)] = new[]
            {
                "TimesheetProjectSubmissionId is required and must be greater than 0."
            };
        }

        if (request.DeletedBy <= 0)
        {
            failures[nameof(ProjectTimesheetDeleteRequest.DeletedBy)] = new[]
            {
                "DeletedBy is required: a delete records who performed it."
            };
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var submission = await _timesheetProjectRepository.GetSubmissionForUpdateAsync(
                request.TimesheetProjectSubmissionId,
                cancellationToken)
            ?? throw NotFoundException.For("Project sheet", request.TimesheetProjectSubmissionId);

        submission.IsDelete = 1;

        await _timesheetProjectRepository.UpdateSubmissionAsync(submission, cancellationToken);

        // Worth being loud about: the sheet was the only thing holding its
        // entries shut, so removing it reopens them for editing and the user
        // can submit the same code again.
        _logger.LogWarning(
            "Project sheet {SheetSubmissionCode} ({TimesheetProjectSubmissionId}) removed by user {DeletedBy}; its entries are editable again. The table has no DeletedBy column, so this log is the only record of who removed it.",
            submission.SheetSubmissionCode,
            submission.TimesheetProjectSubmissionId,
            request.DeletedBy);
    }

    /// <summary>One line per project on a sheet, ordered by project name.</summary>
    private static IReadOnlyList<ProjectTimesheetProjectLineResponse> ProjectLinesOf(
        IReadOnlyList<TimesheetProjectTimeLogResponse> entries) =>
        entries
            .GroupBy(entry => new { entry.ProjectId, entry.ProjectCode, entry.ProjectName })
            .Select(group =>
            {
                var minutes = group.Sum(entry => entry.DurationMinutes);

                return new ProjectTimesheetProjectLineResponse
                {
                    ProjectId = group.Key.ProjectId,
                    ProjectCode = group.Key.ProjectCode,
                    ProjectName = group.Key.ProjectName,
                    TotalMinutes = minutes,
                    TotalHours = ToHours(minutes),
                    EntryCount = group.Count(),
                };
            })
            .OrderBy(line => line.ProjectName)
            .ThenBy(line => line.ProjectId)
            .ToList();

    private static ProjectTimesheetSubmissionResponse ToResponse(TimesheetProjectSubmission submission) => new()
    {
        TimesheetProjectSubmissionId = submission.TimesheetProjectSubmissionId,
        SheetSubmissionCode = submission.SheetSubmissionCode,
        TotalMinutes = submission.TotalMinutes,
        TotalHours = submission.TotalHours,
        StatusId = submission.StatusId,
        StatusName = SubmissionStatusName(submission.StatusId),
        SubmittedBy = submission.SubmittedBy,
        SubmittedDate = submission.SubmittedDate,
        ApprovedBy = submission.ApprovedBy,
        ApprovedDate = submission.ApprovedDate,
        RejectedBy = submission.RejectedBy,
        RejectedDate = submission.RejectedDate,
        RejectionReason = submission.RejectionReason,
    };

    /// <summary>
    /// Nothing in the table restricts <c>StatusID</c> to the three known
    /// values, so an unrecognised one is reported as itself rather than
    /// guessed at or thrown over.
    /// </summary>
    private static string SubmissionStatusName(int statusId) => statusId switch
    {
        Constants.TimesheetSubmission.Status.Submitted => "Submitted",
        Constants.TimesheetSubmission.Status.Approved => "Approved",
        Constants.TimesheetSubmission.Status.Rejected => "Rejected",
        _ => statusId.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Minutes as hours to two places, rounded away from zero like SQL ROUND -
    /// the same rounding <c>dbo.TimesheetSubmission.TotalHours</c> uses, so the
    /// two modules report one number the same way.
    /// </summary>
    private static decimal ToHours(int minutes) =>
        Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The sheet a newly logged entry belongs to: the user's open sheet when
    /// they have one, otherwise a fresh code.
    /// <para>
    /// This is the whole of the rule. One code covers every entry a user logs
    /// between one submission and the next - across every project, and for
    /// however long that is - and <b>submitting the sheet is what ends it</b>.
    /// The next entry logged after a submission finds no open sheet and starts
    /// a new code.
    /// </para>
    /// <para>
    /// Note what does NOT end a sheet: a date. The project's
    /// <c>SubmissionFrequency</c> says how often a sheet is EXPECTED to be
    /// handed in, but nothing here rolls the code over when that long has
    /// passed - so a user who never submits keeps one code indefinitely. It is
    /// also why the per-project frequency does not have to be reconciled with
    /// a per-user sheet to allocate a code.
    /// </para>
    /// </summary>
    private async Task<string> OpenSheetCodeForUserAsync(int userId, CancellationToken cancellationToken) =>
        await _timesheetProjectRepository.GetOpenSheetCodeForUserAsync(userId, cancellationToken)
            ?? NextSheetSubmissionCode(
                userId,
                await _timesheetProjectRepository.GetSheetCodesForUserAsync(userId, cancellationToken));

    /// <summary>
    /// The sheet a REVIEW screen should show: the user's open sheet when they
    /// have one, otherwise the last one they handed in, and only for a user
    /// with no history at all the code their first sheet would take.
    /// <para>
    /// The fallback matters. Straight after a submission the user has no open
    /// sheet, and allocating the next code here would show them a fresh empty
    /// sheet that looks submittable - while
    /// <see cref="SubmitProjectTimesheetAsync"/> would refuse it, because
    /// nothing has been logged since. Showing the sheet they just handed in,
    /// with its status, is both honest and what they want to see.
    /// </para>
    /// </summary>
    private async Task<string> CurrentSheetCodeForUserAsync(int userId, CancellationToken cancellationToken)
    {
        var open = await _timesheetProjectRepository.GetOpenSheetCodeForUserAsync(userId, cancellationToken);

        if (open is not null)
        {
            return open;
        }

        var spentCodes = await _timesheetProjectRepository.GetSheetCodesForUserAsync(userId, cancellationToken);

        // Codes run PSH-{user}-0001, 0002, ... so the latest is the highest.
        return HighestSequenceOf(userId, spentCodes) is { } highest
            ? SheetSubmissionCodeFor(userId, highest)
            : SheetSubmissionCodeFor(userId, 1);
    }

    /// <summary>
    /// The user's next sheet code - <c>PSH-58-0001</c>, then
    /// <c>PSH-58-0002</c>, and so on.
    /// <para>
    /// The sequence is <b>per user</b>, not global like
    /// <c>dbo.TimeLog.SheetCode</c>'s, and that is what makes allocating one
    /// safe without a unique index to fall back on. Two requests racing to
    /// start the same user's first sheet both read the same highest code and
    /// both write the same next one - so they land on the SAME sheet, which is
    /// where they belonged anyway. Two different users cannot collide at all,
    /// because their ids differ. The global sequence can do neither: there,
    /// two racing allocations hand one code to two people.
    /// </para>
    /// </summary>
    private static string NextSheetSubmissionCode(int userId, IReadOnlyCollection<string> existingCodes) =>
        SheetSubmissionCodeFor(userId, (HighestSequenceOf(userId, existingCodes) ?? 0) + 1);

    /// <summary>
    /// The highest sequence number the user's codes have reached, or
    /// <see langword="null"/> when they have none.
    /// <para>
    /// A code not of this shape is ignored rather than guessed at: one written
    /// by hand, or by an older rule, must not stall the sequence.
    /// </para>
    /// </summary>
    private static int? HighestSequenceOf(int userId, IReadOnlyCollection<string> existingCodes)
    {
        var prefix = $"{SheetSubmissionCodePrefix}-{userId}-";

        var sequences = existingCodes
            .Where(code => code.StartsWith(prefix, StringComparison.Ordinal))
            .Select(code => int.TryParse(
                code[prefix.Length..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number)
                ? number
                : 0)
            .ToArray();

        return sequences.Length == 0 ? null : sequences.Max();
    }

    private static string SheetSubmissionCodeFor(int userId, int sequence) =>
        $"{SheetSubmissionCodePrefix}-{userId}-" + sequence.ToString(
            new string('0', SheetSubmissionCodeInitialDigits),
            CultureInfo.InvariantCulture);

    /// <summary>
    /// Refuses the write when the sheet has been submitted and not rejected.
    /// A REJECTED sheet leaves its entries open: that is how a user corrects
    /// them and submits again.
    /// </summary>
    private async Task RequireSheetIsOpenAsync(string sheetSubmissionCode, CancellationToken cancellationToken)
    {
        if (await _timesheetProjectRepository.SheetIsSubmittedAsync(sheetSubmissionCode, cancellationToken))
        {
            throw new ConflictException(
                $"Sheet '{sheetSubmissionCode}' has been submitted. Its entries cannot be changed until it is rejected.");
        }
    }

    /// <summary>
    /// Reads a required <c>hh:mm:ss</c> time, recording the shared message
    /// rather than throwing, so one bad time does not hide the other failures
    /// in the same payload.
    /// </summary>
    private static TimeSpan? ReadTimeOfDay(
        Dictionary<string, string[]> failures,
        string? value,
        string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures[field] = new[] { TimeOfDay.RequiredMessage(field) };
            return null;
        }

        if (TimeOfDay.TryParse(value, out var parsed))
        {
            return parsed;
        }

        failures[field] = new[] { TimeOfDay.MalformedMessage(field) };
        return null;
    }

    /// <summary>
    /// How long a block ran, from its clock times alone.
    /// <para>
    /// An end <b>before</b> the start is read as crossing midnight rather than
    /// as a negative duration, so 22:00:00 to 02:00:00 is 240 minutes on the
    /// day it started. The two times are never equal by the time this is
    /// called, so the result is always between 1 and 1439 - an entry can never
    /// be empty, nor longer than the day that holds it.
    /// </para>
    /// </summary>
    private static int MinutesBetween(TimeSpan startTime, TimeSpan endTime) =>
        (int)(endTime > startTime
            ? (endTime - startTime).TotalMinutes
            : (endTime - startTime + TimeSpan.FromDays(1)).TotalMinutes);
}
