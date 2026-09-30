using ETimeSheet.Application.Models;
using ETimeSheet.Shared.Utilities;
using FluentValidation;

namespace ETimeSheet.Application.Validators.SheetSubmissions;

/// <summary>
/// Shape-level validation for the review-timesheet payload. Whether the sheet
/// exists, and whether it can still be reviewed, need the database to answer
/// and live in <c>SheetSubmissionService</c>.
/// <para>
/// <c>OrgId</c> is deliberately unchecked: nothing uses it yet.
/// </para>
/// </summary>
public class SheetSubmissionReviewRequestValidator : AbstractValidator<SheetSubmissionReviewRequest>
{
    public SheetSubmissionReviewRequestValidator()
    {
        RuleFor(request => request.AdminId)
            .GreaterThan(0)
            .WithMessage("AdminId is required and must be greater than 0.");

        // 15: dbo.TimesheetSubmission.Timesheetcode is varchar(15).
        RuleFor(request => request.SheetCode)
            .NotEmpty()
            .WithMessage("SheetCode is required.")
            .MaximumLength(15)
            .WithMessage("SheetCode must be 15 characters or fewer.");

        // A review decides; it never puts a sheet back to Submitted.
        RuleFor(request => request.Status)
            .Must(status => status is Constants.TimesheetSubmission.Status.Approved
                                   or Constants.TimesheetSubmission.Status.Rejected)
            .WithMessage("Status must be 2 (Approved) or 3 (Rejected).");
    }
}
