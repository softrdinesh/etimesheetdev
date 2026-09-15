using ETimeSheet.Application.Interfaces.Services;

namespace ETimeSheet.Tests.Helpers;

/// <summary>
/// A clock frozen at a known instant, so that rules about "the future" and
/// "too old to edit" are deterministic instead of depending on the wall clock.
/// </summary>
public class FixedDateTimeProvider : IDateTimeProvider
{
    public FixedDateTimeProvider(DateTime utcNow)
    {
        UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    public DateTime UtcNow { get; }

    public DateTime UtcToday => DateTime.SpecifyKind(UtcNow.Date, DateTimeKind.Utc);
}
