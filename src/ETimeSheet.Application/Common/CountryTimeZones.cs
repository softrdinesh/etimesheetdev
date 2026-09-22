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
/// It is pure string work with no rule in it, and no longer has a rule to
/// serve: the timesheet save used to resolve a setup's zone through this class,
/// and now stores whatever the payload sends. What is left is the unpacking that
/// <c>get-country-list-with-timezones</c> needs to turn one row into one entry
/// per zone. A <c>Find</c> that matched a caller's zone against a country's list
/// went when that rule did.
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
}
