using ETimeSheet.Application.DTOs.Admins;
using FluentValidation;

namespace ETimeSheet.Application.Validators.Admins;

/// <summary>Shape-level validation for the timesheet setup delete payload.</summary>
public class AdminDeleteRequestValidator : AbstractValidator<AdminDeleteRequest>
{
    public AdminDeleteRequestValidator()
    {
        RuleFor(request => request.SetupId)
            .GreaterThan(0)
            .WithMessage("SetupId is required and must be greater than 0.");

        RuleFor(request => request.DeletedBy)
            .GreaterThan(0)
            .WithMessage("DeletedBy is required: a soft delete records who performed it.");
    }
}
