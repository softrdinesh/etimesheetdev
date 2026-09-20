namespace ETimeSheet.Application.Common;

/// <summary>
/// The single reader of <c>dbo.Country.TimeZone</c> - the one column in the
/// schema that packs a list into a string.
/// <para>
/// A country holds one IANA zone id, or several separated by commas when it
/// spans more than one:
/// <c>"Europe/London"</c>, <c>"Asia/Shanghai,Asia/Urumqi"</c>,
/// <c>"America/New_York,America/Detroit,..."</c> for the twenty-nine of the
/// United States. The first id is the country's primary zone.
/// </para>
/// <para>
/// Here rather than inline in <c>AdminService</c> so that "how many zones does
/// this country have?" is answered in exactly one place. Split the string at a
/// call site and the two answers can drift - one tolerating a stray space
/// around a comma, the other counting an empty entry after a trailing one as a
/// zone, and a country with a tidy-up in its row suddenly demanding a choice
/// the client cannot make.
/// </para>
/// <para>
/// It is pure string work with no rule in it: whether a caller must choose a
/// zone, and what happens when they choose one that is not here, is
/// <c>AdminService</c>'s decision.
/// </para>
/// </summary>
internal static class CountryTimeZones
{
    private static readonly string[] Empty = Array.Empty<string>();

    /// <summary>
    /// The zone ids a country carries, in the order the column lists them - so
    /// the first is the primary zone. Empty when the column is null, blank, or
    /// holds nothing but separators.
    /// <para>
    /// Entries are trimmed and blanks are dropped, so <c>"A, B,"</c> is two
    /// zones rather than three. The script that populates the column writes no
    /// spaces, but the column is maintained by hand and a value typed there by
    /// a person should not change what the API demands of a client.
    /// </para>
    /// </summary>
    internal static IReadOnlyList<string> Split(string? timeZones) =>
        string.IsNullOrWhiteSpace(timeZones)
            ? Empty
            : timeZones!
                .Split(',')
                .Select(zone => zone.Trim())
                .Where(zone => zone.Length > 0)
                .ToArray();

    /// <summary>
    /// Finds one zone in a country's list, ignoring case and surrounding
    /// whitespace, and returns it <b>as the country spells it</b> - or null when
    /// the country does not have it.
    /// <para>
    /// The country's spelling is what gets stored, not the caller's: IANA ids
    /// are case-sensitive in every library that consumes them, so accepting
    /// <c>"europe/london"</c> and writing it back verbatim would persist a value
    /// that <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> later rejects.
    /// Matching leniently and storing canonically is the only combination that
    /// is both forgiving at the edge and correct in the row.
    /// </para>
    /// </summary>
    internal static string? Find(IReadOnlyList<string> zones, string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
        {
            return null;
        }

        var wanted = timeZone!.Trim();

        foreach (var zone in zones)
        {
            if (string.Equals(zone, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return zone;
            }
        }

        return null;
    }
}
