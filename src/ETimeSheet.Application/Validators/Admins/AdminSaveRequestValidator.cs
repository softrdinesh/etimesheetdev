using ETimeSheet.Application.Models;
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
/// <para>
/// <b>The three time fields are deliberately absent.</b> They arrive as
/// <c>hh:mm:ss</c> strings and are read by <c>AdminService</c> through
/// <see cref="ETimeSheet.Shared.Utilities.TimeOfDay"/>, so that one parser
/// decides what a time of day is for the whole API. Re-checking the format here
/// would be a second opinion that can drift from the first.
/// </para>
/// </summary>
public class AdminSaveRequestValidator : AbstractValidator<AdminSaveRequest>
{
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

        // Required, unlike the two above, even though the column is nullable.
        // It is no longer required in order to resolve the time zone - nothing
        // resolves anything any more, and CountryId is stored exactly as sent -
        // but a timesheet setup that names no country is still not a setup
        // anyone asked for, so the contract keeps it mandatory. Two rules rather
        // than one so the caller is told which of "you left it out" and "you
        // sent 0" happened.
        RuleFor(request => request.CountryId)
            .NotNull()
            .WithMessage("CountryId is required.");

        RuleFor(request => request.CountryId!.Value)
            .GreaterThan(0)
            .WithMessage("CountryId must be greater than 0.")
            .OverridePropertyName(nameof(AdminSaveRequest.CountryId))
            .When(request => request.CountryId.HasValue);

        // Shape only, and shape is now the ONLY check this field gets: the save
        // stores whatever arrives, so nothing downstream will notice a zone that
        // its country does not have, or one that is not an IANA id at all. These
        // two rules are the whole guard - a value that was sent has to be a real
        // value and has to fit the nvarchar(100) column.
        //
        // Deliberately still not a format or membership rule. Both need
        // dbo.Country or the system's zone database, and a validator may not
        // read either (CLAUDE.md §12); the caller owns that consistency and
        // builds its choice from get-country-list-with-timezones.
        RuleFor(request => request.TimeZone!)
            .NotEmpty()
            .WithMessage("TimeZone must not be blank when it is supplied; leave it out instead.")
            .MaximumLength(100)
            .WithMessage("TimeZone must be 100 characters or fewer.")
            .OverridePropertyName(nameof(AdminSaveRequest.TimeZone))
            .When(request => request.TimeZone is not null);

        // MaxTimeInHrs, MaxTimInMins and TimeEntryLockAt are not validated here.
        // See the class summary: AdminService reads all three through TimeOfDay.

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
}
