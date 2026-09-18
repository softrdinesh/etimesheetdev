using ETimeSheet.Application.Models;
using FluentValidation;

namespace ETimeSheet.Application.Validators.TimeLogs;

/// <summary>
/// Shape-level validation for the "log my time" payload.
/// <para>
/// This answers "is the payload well formed?" only - the fields the table can
/// physically hold, and the two ends of the entry making sense against each
/// other. Everything that needs the database to answer - the user's daily
/// maximum, their working week, whether back-dating is still open, whether the
/// slot is already taken - lives in <c>TimeLogService</c>, because a validator
/// may not read the database.
/// </para>
/// <para>
/// <b>StartTime and EndTime are deliberately absent.</b> They arrive as
/// <c>hh:mm:ss</c> strings and are read by <c>TimeLogService</c> through
/// <see cref="ETimeSheet.Shared.Utilities.TimeOfDay"/>, so that one parser
/// decides what a time of day is for the whole API. The rule that the entry
/// runs forwards went with them: it cannot be stated without the parsed values,
/// and parsing the same strings twice invites the two answers to disagree.
/// </para>
/// </summary>
public class TimeLogSaveRequestValidator : AbstractValidator<TimeLogSaveRequest>
{
    /// <summary>
    /// How far past the start date the end date may sit. One day covers a shift
    /// running past midnight, which is the only reason the table has two dates;
    /// more than that is a typo, not a shift.
    /// </summary>
    private const int MaximumSpanInDays = 1;

    public TimeLogSaveRequestValidator()
    {
        RuleFor(request => request.UserId)
            .GreaterThan(0)
            .WithMessage("UserId is required: an entry must belong to an employee.");

        RuleFor(request => request.TaskId)
            .GreaterThan(0)
            .WithMessage("TaskId is required: time is always logged against a task.");

        RuleFor(request => request.CreatedBy)
            .GreaterThan(0)
            .WithMessage("CreatedBy is required: the row records who logged the time.");

        RuleFor(request => request.StartDate)
            .NotEmpty()
            .WithMessage("StartDate is required.");

        // Status arrives as an enum, so an unknown number binds to an undefined
        // member rather than failing. Without this, Status: 7 would be written
        // to the column and every reader would have to guess what it meant.
        RuleFor(request => request.Status)
            .IsInEnum()
            .WithMessage("Status must be 1 (Save) or 2 (Draft).");

        // There is no SheetCode rule, because there is no SheetCode field: the
        // service generates it. Nothing arrives from the caller to validate.

        // StartTime and EndTime are not validated here, and neither is the
        // ordering between them. See the class summary: TimeLogService reads
        // both through TimeOfDay and checks the span it gets back.

        RuleFor(request => request.EndDate!.Value)
            .GreaterThanOrEqualTo(request => request.StartDate.Date)
            .WithMessage("EndDate must not be earlier than StartDate.")
            .LessThanOrEqualTo(request => request.StartDate.Date.AddDays(MaximumSpanInDays))
            .WithMessage("EndDate must be the same day as StartDate or the day after it.")
            // OverridePropertyName, not WithName: without it the client is told
            // the field is called "EndDate.Value", which is a C# detail and not
            // something it sent.
            .OverridePropertyName(nameof(TimeLogSaveRequest.EndDate))
            .When(request => request.EndDate.HasValue);
    }
}
