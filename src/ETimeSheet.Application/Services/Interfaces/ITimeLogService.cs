using ETimeSheet.Application.DTOs.TimeLogs;

namespace ETimeSheet.Application.Services.Interfaces;

/// <summary>
/// Application contract for the TimeLog module. Every public method of
/// <c>TimeLogService</c> is declared here; its private helpers are not.
/// <para>
/// This is the only surface <c>TimeLogController</c> is allowed to touch.
/// </para>
/// </summary>
public interface ITimeLogService
{
    /// <summary>
    /// Returns the entries one user logged against one task, via the
    /// <c>spc_GetTimeLoggedDetailsForTask</c> stored procedure.
    /// </summary>
    Task<TimeLoggedDetailsForTaskResponse> GetTimeLoggedDetailsForTaskAsync(
        TimeLoggedDetailsForTaskRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the timesheet setup that applies to one user - their maximum
    /// loggable time, contract type and the bounds of their timesheet week - via
    /// the <c>spc_GetTimesheetMasterSetupByUserID</c> stored procedure.
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.NotFoundException">
    /// The user has no setup row, which surfaces as a 404.
    /// </exception>
    Task<TimesheetMasterSetupResponse> GetTimesheetMasterSetupByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records one block of work for an employee, after checking it against the
    /// timesheet setup that applies to them.
    /// <para>
    /// The setup drives four of the rules - the working week, whether
    /// back-dating is still open, the cut-off time for it and the daily maximum -
    /// so a user with no setup row cannot log time at all. The remaining rule,
    /// that a new entry may not overlap one the user already has that day, comes
    /// from the entries themselves.
    /// </para>
    /// </summary>
    /// <exception cref="ETimeSheet.Shared.Exceptions.BusinessException">
    /// The entry breaks one of the setup's rules, which surfaces as a 400.
    /// </exception>
    /// <exception cref="ETimeSheet.Shared.Exceptions.ConflictException">
    /// The entry overlaps one the user already has that day, which surfaces as a 409.
    /// </exception>
    Task<TimeLogResponse> SaveTimeLogAsync(
        TimeLogSaveRequest request,
        CancellationToken cancellationToken = default);
}
