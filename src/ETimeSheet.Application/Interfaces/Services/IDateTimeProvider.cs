namespace ETimeSheet.Application.Interfaces.Services;

/// <summary>
/// Injectable clock. Business rules about "today", "in the future" and
/// "too old to edit" are only testable if the current time is a dependency.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    /// <summary>Current date at UTC midnight.</summary>
    DateTime UtcToday { get; }
}
