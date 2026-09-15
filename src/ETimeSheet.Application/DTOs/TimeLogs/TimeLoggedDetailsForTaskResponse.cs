namespace ETimeSheet.Application.DTOs.TimeLogs;

/// <summary>
/// What the time logged details endpoint returns: the totals for the period,
/// then the entries they were derived from.
/// <para>
/// <b>Property order matters here.</b> System.Text.Json writes properties in
/// declaration order, so <see cref="Summary"/> is declared first to put the
/// totals at the top of the payload - where they can be read without scrolling
/// past a long <see cref="Details"/> array.
/// </para>
/// </summary>
public class TimeLoggedDetailsForTaskResponse
{
    public TimeLoggedSummaryResponse Summary { get; init; } = new();

    public IReadOnlyCollection<TimeLoggedDetailResponse> Details { get; init; } =
        Array.Empty<TimeLoggedDetailResponse>();
}
