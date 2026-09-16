using ETimeSheet.Application.DTOs.TimeLogs;
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
/// </summary>
public class TimeLogSaveRequestValidator : AbstractValidator<TimeLogSaveRequest>
{
    /// <summary>
    /// Exclusive upper bound for the <c>time(7)</c> columns: they hold a time of
    /// day, so they cannot reach 24 hours.
    /// </summary>
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

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

        // varchar(15): a longer value is silently truncated by SQL Server rather
        // than rejected, which stores something the caller never sent.
        RuleFor(request => request.SheetCode)
            .MaximumLength(15)
            .WithMessage("SheetCode cannot be longer than 15 characters.")
            .When(request => !string.IsNullOrWhiteSpace(request.SheetCode));

        // time(7) columns: .NET is happy with a negative or 25-hour TimeSpan and
        // SQL Server is not, so it is caught here where the error can name the
        // field rather than surfacing as a failed insert.
        RuleFor(request => request.StartTime)
            .Must(BeATimeOfDay)
            .WithMessage("StartTime must be between 00:00:00 and 23:59:59.");

        RuleFor(request => request.EndTime)
            .Must(BeATimeOfDay)
            .WithMessage("EndTime must be between 00:00:00 and 23:59:59.");

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

        // The entry has to cover some time, and it has to run forwards. Checked
        // across both ends including their dates, so an overnight shift -
        // 22:00 on Monday to 06:00 on Tuesday - passes, while 17:00 to 09:00 on
        // a single day does not.
        RuleFor(request => request)
            .Must(HaveAPositiveDuration)
            .WithMessage("EndTime must be after StartTime.")
            .WithName(nameof(TimeLogSaveRequest.EndTime))
            .When(request => BeATimeOfDay(request.StartTime) && BeATimeOfDay(request.EndTime));
    }

    private static bool BeATimeOfDay(TimeSpan value) =>
        value >= TimeSpan.Zero && value < OneDay;

    private static bool HaveAPositiveDuration(TimeLogSaveRequest request)
    {
        var startsAt = request.StartDate.Date + request.StartTime;
        var endsAt = (request.EndDate?.Date ?? request.StartDate.Date) + request.EndTime;

        return endsAt > startsAt;
    }
}
