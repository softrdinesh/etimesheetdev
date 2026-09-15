using ETimeSheet.Application.Interfaces.Services;

namespace ETimeSheet.Infrastructure.Services;

/// <summary>
/// The real clock. Tests substitute a fixed implementation instead.
/// </summary>
public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime UtcToday => DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
}
