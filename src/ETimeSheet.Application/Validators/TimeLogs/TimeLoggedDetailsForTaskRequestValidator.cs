using ETimeSheet.Application.Models;
using FluentValidation;

namespace ETimeSheet.Application.Validators.TimeLogs;

/// <summary>Shape-level validation for the time logged details payload.</summary>
public class TimeLoggedDetailsForTaskRequestValidator
    : AbstractValidator<TimeLoggedDetailsForTaskRequest>
{
    public TimeLoggedDetailsForTaskRequestValidator()
    {
        RuleFor(request => request.UserId)
            .GreaterThan(0);

        RuleFor(request => request.TaskId)
            .GreaterThan(0);

        RuleFor(request => request.StartDate)
            .NotEmpty();

        RuleFor(request => request.EndDate)
            .NotEmpty();

        RuleFor(request => request.EndDate)
            .GreaterThanOrEqualTo(request => request.StartDate)
            .WithMessage("The end of the range must not be earlier than its start.");
    }
}
