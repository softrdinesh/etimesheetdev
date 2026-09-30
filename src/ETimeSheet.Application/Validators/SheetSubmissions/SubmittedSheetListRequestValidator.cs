using ETimeSheet.Application.Models;
using FluentValidation;

namespace ETimeSheet.Application.Validators.SheetSubmissions;

/// <summary>Shape-level validation for the get-submitted-sheet-list payload.</summary>
public class SubmittedSheetListRequestValidator : AbstractValidator<SubmittedSheetListRequest>
{
    public SubmittedSheetListRequestValidator()
    {
        RuleFor(request => request.AdminId)
            .GreaterThan(0)
            .WithMessage("AdminId is required and must be greater than 0.");

        // Zero is meaningful here - every user - so only a negative id is wrong.
        RuleFor(request => request.UserId)
            .GreaterThanOrEqualTo(0)
            .WithMessage("UserId must be 0 (every user) or a user id.");

        RuleFor(request => request.OrgId)
            .GreaterThan(0)
            .WithMessage("OrgId is required and must be greater than 0.");
    }
}
