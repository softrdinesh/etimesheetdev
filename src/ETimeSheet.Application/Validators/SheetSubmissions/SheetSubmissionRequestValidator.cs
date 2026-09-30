using ETimeSheet.Application.Models;
using FluentValidation;

namespace ETimeSheet.Application.Validators.SheetSubmissions;

/// <summary>
/// Shape-level validation for the submit-timesheet payload. Whether the sheet
/// has anything on it, and whether it was already submitted, need the database
/// to answer and live in <c>SheetSubmissionService</c>.
/// <para>
/// <c>OrgId</c> is deliberately unchecked: nothing uses it yet.
/// </para>
/// </summary>
public class SheetSubmissionRequestValidator : AbstractValidator<SheetSubmissionRequest>
{
    public SheetSubmissionRequestValidator()
    {
        // 15: dbo.TimeLog.SheetCode and dbo.TimesheetSubmission.Timesheetcode
        // are both varchar(15).
        RuleFor(request => request.SheetCode)
            .NotEmpty()
            .WithMessage("SheetCode is required.")
            .MaximumLength(15)
            .WithMessage("SheetCode must be 15 characters or fewer.");

        RuleFor(request => request.UserId)
            .GreaterThan(0)
            .WithMessage("UserId is required and must be greater than 0.");
    }
}
