namespace ETimeSheet.Application.DTOs.TimeLogs;

/// <summary>
/// Body of the time logged details request. Everything the query needs travels
/// in the payload; nothing is taken from the route or the query string.
/// <para>
/// <c>UserId</c> is supplied by the caller. It would normally be taken from the
/// authenticated principal rather than trusted from the request; authentication
/// is switched off for now, so this is deliberately temporary.
/// </para>
/// </summary>
public class TimeLoggedDetailsForTaskRequest
{
    public int UserId { get; set; }

    public int TaskId { get; set; }

    /// <summary>
    /// Inclusive lower bound on the entry's <c>StartDate</c>. A calendar date,
    /// not an instant - the column is <c>date</c>.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>Inclusive upper bound on the entry's <c>StartDate</c>.</summary>
    public DateTime EndDate { get; set; }
}
