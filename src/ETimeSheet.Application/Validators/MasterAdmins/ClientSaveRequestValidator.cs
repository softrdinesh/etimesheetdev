using ETimeSheet.Application.Models;
using FluentValidation;

namespace ETimeSheet.Application.Validators.MasterAdmins;

/// <summary>
/// Shape-level validation for the client add/edit payload.
/// <para>
/// This answers "is the payload well formed?" only. Whether the client being
/// edited exists, and which code a new client gets, are database questions and
/// belong to <c>MasterAdminService</c>.
/// </para>
/// <para>
/// The lengths are the column lengths recorded in
/// <c>docs/database/schema/dbo.Timesheetclientsetup.sql</c>: SQL Server would
/// otherwise reject an over-long value with a 500 rather than a named field.
/// </para>
/// </summary>
public class ClientSaveRequestValidator : AbstractValidator<ClientSaveRequest>
{
    public ClientSaveRequestValidator()
    {
        // Null and 0 both mean "add"; only a negative id is malformed.
        RuleFor(request => request.ClientId)
            .GreaterThanOrEqualTo(0)
            .When(request => request.ClientId.HasValue)
            .WithMessage("ClientId must be 0 (or left out) to add a client, or the id of the client to edit.");

        RuleFor(request => request.ClientName)
            .NotEmpty()
            .WithMessage("ClientName is required.")
            .MaximumLength(50)
            .WithMessage("ClientName must be 50 characters or fewer.");

        // Optional, but an empty file is a mistake rather than "no logo".
        RuleFor(request => request.ClientLogo!.Length)
            .GreaterThan(0)
            .When(request => request.ClientLogo is not null)
            .OverridePropertyName(nameof(ClientSaveRequest.ClientLogo))
            .WithMessage("ClientLogo is an empty file.");

        RuleFor(request => request.BillingEmail)
            .NotEmpty()
            .WithMessage("BillingEmail is required.")
            .MaximumLength(150)
            .WithMessage("BillingEmail must be 150 characters or fewer.")
            .EmailAddress()
            .WithMessage("BillingEmail must be a valid email address.");

        RuleFor(request => request.PhoneNumber)
            .NotEmpty()
            .WithMessage("PhoneNumber is required.")
            .MaximumLength(150)
            .WithMessage("PhoneNumber must be 150 characters or fewer.");

        RuleFor(request => request.AlternatePhoneNumber)
            .MaximumLength(150)
            .WithMessage("AlternatePhoneNumber must be 150 characters or fewer.");

        RuleFor(request => request.AlternateEmail)
            .MaximumLength(150)
            .WithMessage("AlternateEmail must be 150 characters or fewer.")
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.AlternateEmail))
            .WithMessage("AlternateEmail must be a valid email address.");

        RuleFor(request => request.PaymentTermsDays)
            .NotNull()
            .WithMessage("PaymentTermsDays is required.")
            .GreaterThanOrEqualTo(0)
            .WithMessage("PaymentTermsDays must be 0 or more.");

        // Required: the client's currency is derived from it. Whether the
        // country actually has a currency is a database question, answered by
        // MasterAdminService.
        RuleFor(request => request.CountryId)
            .NotNull()
            .WithMessage("CountryId is required: the client's currency is taken from its country.")
            .GreaterThan(0)
            .WithMessage("CountryId must be greater than 0.");

        RuleFor(request => request.TaxIdentifier)
            .MaximumLength(50)
            .WithMessage("TaxIdentifier must be 50 characters or fewer.");
    }
}
