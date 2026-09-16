using ETimeSheet.Application.DTOs.AdminSetups;
using FluentValidation;

namespace ETimeSheet.Application.Validators.AdminSetups;

/// <summary>
/// Shape-level validation for the timesheet setup save payload.
/// <para>
/// This answers "is the payload well formed?" only. Whether the user already
/// has a setup, whether it was deleted and which of those makes this an insert
/// or an update are database questions, and they belong to
/// <c>AdminSetupService</c>.
/// </para>
/// </summary>
public class AdminSetupSaveRequestValidator : AbstractValidator<AdminSetupSaveRequest>
{
    /// <summary>
    /// Exclusive upper bound for every <c>time(7)</c> column: the type holds a
    /// time of day, so it cannot reach 24 hours.
    /// </summary>
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    public AdminSetupSaveRequestValidator()
    {
        // There is no SetupId to validate: the payload does not carry one.
        // UserId is the identity of the row, which makes it the one field the
        // save cannot proceed without.
        RuleFor(request => request.UserId)
            .GreaterThan(0)
            .WithMessage("UserId is required: a setup must belong to a user.");

        RuleFor(request => request.CreatedBy)
            .GreaterThan(0)
            .WithMessage("CreatedBy is required: the row records who created or changed it.");

        // Optional foreign keys: absent is fine, present-but-nonsense is not.
        RuleFor(request => request.OrganizationId!.Value)
            .GreaterThan(0)
            .WithMessage("OrganizationId must be greater than 0 when it is supplied.")
            // OverridePropertyName, not WithName: without it the client is told
            // the field is called "OrganizationId.Value" - a C# detail, and not
            // a field it ever sent. WithName only changes the {PropertyName}
            // placeholder inside the message text, while the key the client
            // actually reads comes from the property name. Every rule written
            // against a nullable's .Value needs the same treatment.
            .OverridePropertyName(nameof(AdminSetupSaveRequest.OrganizationId))
            .When(request => request.OrganizationId.HasValue);

        RuleFor(request => request.ContractType!.Value)
            .GreaterThan(0)
            .WithMessage("ContractType must be greater than 0 when it is supplied.")
            .OverridePropertyName(nameof(AdminSetupSaveRequest.ContractType))
            .When(request => request.ContractType.HasValue);

        RuleFor(request => request.CountryId!.Value)
            .GreaterThan(0)
            .WithMessage("CountryId must be greater than 0 when it is supplied.")
            .OverridePropertyName(nameof(AdminSetupSaveRequest.CountryId))
            .When(request => request.CountryId.HasValue);

        // time(7) columns. A negative or 24-hour-plus TimeSpan is perfectly
        // legal in .NET and would be rejected by SQL Server, so it is caught
        // here where the error can name the field.
        RuleFor(request => request.MaxTimeInHrs!.Value)
            .Must(BeATimeOfDay)
            .WithMessage("MaxTimeInHrs must be between 00:00:00 and 23:59:59.")
            .OverridePropertyName(nameof(AdminSetupSaveRequest.MaxTimeInHrs))
            .When(request => request.MaxTimeInHrs.HasValue);

        RuleFor(request => request.MaxTimInMins!.Value)
            .Must(BeATimeOfDay)
            .WithMessage("MaxTimInMins must be between 00:00:00 and 23:59:59.")
            .OverridePropertyName(nameof(AdminSetupSaveRequest.MaxTimInMins))
            .When(request => request.MaxTimInMins.HasValue);

        RuleFor(request => request.TimeEntryLockAt!.Value)
            .Must(BeATimeOfDay)
            .WithMessage("TimeEntryLockAt must be between 00:00:00 and 23:59:59.")
            .OverridePropertyName(nameof(AdminSetupSaveRequest.TimeEntryLockAt))
            .When(request => request.TimeEntryLockAt.HasValue);

        // Day codes. The columns are fixed-width char, so anything longer is
        // truncated by SQL Server rather than rejected - which would store a
        // different value than the caller sent, silently. Length is enforced
        // here instead.
        //
        // Only the SHAPE is checked, not membership of a fixed list: the code
        // vocabulary ("MO"/"TU"/... and "SUN"/"MON"/...) has not been confirmed
        // against the data, and rejecting a code that is actually in use would
        // be worse than accepting one that is not. Tighten this to an explicit
        // set once the vocabulary is agreed.
        RuleFor(request => request.StartDay)
            .Matches("^[A-Za-z]{2}$")
            .WithMessage("StartDay must be a two-letter day code, for example 'MO'.")
            .When(request => !string.IsNullOrWhiteSpace(request.StartDay));

        RuleFor(request => request.EndDay)
            .Matches("^[A-Za-z]{2}$")
            .WithMessage("EndDay must be a two-letter day code, for example 'FR'.")
            .When(request => !string.IsNullOrWhiteSpace(request.EndDay));

        RuleFor(request => request.ExceptionDay)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("ExceptionDay must be a three-letter day code, for example 'SUN'.")
            .When(request => !string.IsNullOrWhiteSpace(request.ExceptionDay));

        // A week needs both ends or neither: one alone cannot be interpreted.
        RuleFor(request => request)
            .Must(request =>
                string.IsNullOrWhiteSpace(request.StartDay) == string.IsNullOrWhiteSpace(request.EndDay))
            .WithMessage("StartDay and EndDay must be supplied together.")
            .WithName("StartDay");
    }

    /// <summary>
    /// True when the value fits a SQL Server <c>time(7)</c>: at or after
    /// midnight and strictly before the next one.
    /// </summary>
    private static bool BeATimeOfDay(TimeSpan value) =>
        value >= TimeSpan.Zero && value < OneDay;
}
