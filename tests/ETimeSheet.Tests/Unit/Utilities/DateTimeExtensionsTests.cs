using ETimeSheet.Shared.Utilities;

namespace ETimeSheet.Tests.Unit.Utilities;

[Trait("Category", "Unit")]
public class DateTimeExtensionsTests
{
    [Fact]
    public void ToUtcDate_StripsTheTimeAndMarksTheResultUtc()
    {
        var value = new DateTime(2026, 3, 10, 17, 45, 31, DateTimeKind.Utc);

        var result = value.ToUtcDate();

        Assert.Equal(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void ToUtcDate_IsIdempotent()
    {
        var midnight = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(midnight, midnight.ToUtcDate());
    }
}
