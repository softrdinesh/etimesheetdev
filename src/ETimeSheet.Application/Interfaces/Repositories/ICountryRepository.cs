using ETimeSheet.Application.Models.Entities;

namespace ETimeSheet.Application.Interfaces.Repositories;

/// <summary>
/// Data access contract for <see cref="Country"/> - the <c>dbo.Country</c>
/// lookup. Every public operation of <c>CountryRepository</c> is declared here.
/// <para>
/// <b>Read-only.</b> The table is reference data maintained by hand in SQL
/// Server, so there is no add, no update and no delete: an API that could write
/// here would be able to invent a time zone for a country.
/// </para>
/// <para>
/// It has no matching service, and needs none - there is no Country module.
/// <c>AdminService</c> owns the one endpoint that reads this table,
/// <c>get-country-list-with-timezones</c>, and a service combining two
/// repositories is exactly how that is done (CLAUDE.md §6). Give it a service
/// the day something asks for a Country module, not before.
/// </para>
/// <para>
/// <b>One read, because there is one caller.</b> A <c>GetByIdAsync</c> lived
/// here while the timesheet save resolved its time zone from the country; the
/// save now stores what the payload sends, so nothing looked a single country
/// up any more and the method went with the rule. Add it back when an endpoint
/// needs it, not before (CLAUDE.md §21.22).
/// </para>
/// </summary>
public interface ICountryRepository
{
    /// <summary>
    /// Returns every country in the lookup, untracked, ordered by name.
    /// <para>
    /// The whole table, and deliberately not paged: <c>dbo.Country</c> is a
    /// fixed reference list of a couple of hundred rows that a picker wants in
    /// one go. This is the one place in the solution where "load the whole
    /// table" is the right query rather than the mistake CLAUDE.md §7 warns
    /// about - the row count is bounded by the number of countries in the
    /// world, and it does not grow.
    /// </para>
    /// <para>
    /// Ordered here rather than by the caller so that two reads of the same
    /// table cannot disagree about the order, and so the sort happens in SQL
    /// Server with its own collation rather than on .NET's.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Country>> GetAllAsync(
        CancellationToken cancellationToken = default);
}
