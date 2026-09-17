using ETimeSheet.Application.DTOs.Admins;
using ETimeSheet.Shared.Utilities;
using FluentValidation;

namespace ETimeSheet.Application.Validators.Admins;

/// <summary>
/// Shape-level validation for the timesheet setup save payload.
/// <para>
/// This answers "is the payload well formed?" only. Whether the user already
/// has a setup, whether it was deleted and which of those makes this an insert
/// or an update are database questions, and they belong to
/// <c>AdminService</c>.
/// </para>
/// </summary>
public class AdminSaveRequestValidator : AbstractValidator<AdminSaveRequest>
{
    /// <summary>
    /// Exclusive upper bound for every <c>time(7)</c> column: the type holds a
    /// time of day, so it cannot reach 24 hours.
    /// </summary>
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    public AdminSaveRequestValidator()
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
            .OverridePropertyName(nameof(AdminSaveRequest.OrganizationId))
            .When(request => request.OrganizationId.HasValue);

        RuleFor(request => request.ContractType!.Value)
            .GreaterThan(0)
            .WithMessage("ContractType must be greater than 0 when it is supplied.")
            .OverridePropertyName(nameof(AdminSaveRequest.ContractType))
            .When(request => request.ContractType.HasValue);

        RuleFor(request => request.CountryId!.Value)
            .GreaterThan(0)
            .WithMessage("CountryId must be greater than 0 when it is supplied.")
            .OverridePropertyName(nameof(AdminSaveRequest.CountryId))
            .When(request => request.CountryId.HasValue);

        // time(7) columns. A negative or 24-hour-plus TimeSpan is perfectly
        // legal in .NET and would be rejected by SQL Server, so it is caught
        // here where the error can name the field.
        RuleFor(request => request.MaxTimeInHrs!.Value)
            .Must(BeATimeOfDay)
            .WithMessage("MaxTimeInHrs must be between 00:00:00 and 23:59:59.")
            .OverridePropertyName(nameof(AdminSaveRequest.MaxTimeInHrs))
            .When(request => request.MaxTimeInHrs.HasValue);

        RuleFor(request => request.MaxTimInMins!.Value)
            .Must(BeATimeOfDay)
            .WithMessage("MaxTimInMins must be between 00:00:00 and 23:59:59.")
            .OverridePropertyName(nameof(AdminSaveRequest.MaxTimInMins))
            .When(request => request.MaxTimInMins.HasValue);

        RuleFor(request => request.TimeEntryLockAt!.Value)
            .Must(BeATimeOfDay)
            .WithMessage("TimeEntryLockAt must be between 00:00:00 and 23:59:59.")
            .OverridePropertyName(nameof(AdminSaveRequest.TimeEntryLockAt))
            .When(request => request.TimeEntryLockAt.HasValue);

        // Day ids. These became dbo.DayMaster.DayID references on 2026-09-17,
        // which is what makes an exact range checkable at all: the lookup holds
        // exactly seven rows, so anything outside 1-7 names no day and the
        // caller can be told so now rather than storing a value nothing can
        // interpret. The previous letter-code rules only checked the shape,
        // because the vocabulary was not established.
        //
        // The check is a range, not a database lookup: a validator may not read
        // the database (see CLAUDE.md §12), and the seven rows are fixed
        // reference data that ships with the schema.
        RuleFor(request => request.StartDay!.Value)
            .InclusiveBetween(Constants.DayMaster.DayId.Monday, Constants.DayMaster.DayId.Sunday)
            .WithMessage(DayIdMessage("StartDay"))
            .OverridePropertyName(nameof(AdminSaveRequest.StartDay))
            .When(request => request.StartDay.HasValue);

        RuleFor(request => request.EndDay!.Value)
            .InclusiveBetween(Constants.DayMaster.DayId.Monday, Constants.DayMaster.DayId.Sunday)
            .WithMessage(DayIdMessage("EndDay"))
            .OverridePropertyName(nameof(AdminSaveRequest.EndDay))
            .When(request => request.EndDay.HasValue);

        RuleFor(request => request.ExceptionDay!.Value)
            .InclusiveBetween(Constants.DayMaster.DayId.Monday, Constants.DayMaster.DayId.Sunday)
            .WithMessage(DayIdMessage("ExceptionDay"))
            .OverridePropertyName(nameof(AdminSaveRequest.ExceptionDay))
            .When(request => request.ExceptionDay.HasValue);

        // A week needs both ends or neither: one alone cannot be interpreted.
        RuleFor(request => request)
            .Must(request => request.StartDay.HasValue == request.EndDay.HasValue)
            .WithMessage("StartDay and EndDay must be supplied together.")
            .WithName("StartDay");
    }

    /// <summary>
    /// One message for all three day fields, so they cannot describe the same
    /// rule differently. It spells the range out, because "must be between 1 and
    /// 7" alone does not tell a caller which end is Monday.
    /// </summary>
    private static string DayIdMessage(string field) =>
        $"{field} must be a DayMaster day id between " +
        $"{Constants.DayMaster.DayId.Monday} (Monday) and " +
        $"{Constants.DayMaster.DayId.Sunday} (Sunday).";

    /// <summary>
    /// True when the value fits a SQL Server <c>time(7)</c>: at or after
    /// midnight and strictly before the next one.
    /// </summary>
    private static bool BeATimeOfDay(TimeSpan value) =>
        value >= TimeSpan.Zero && value < OneDay;
}
