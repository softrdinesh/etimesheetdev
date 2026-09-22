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
/// Two reads, one per caller: the whole lookup for the country picker, and one
/// country by id so a setup read can name the country it holds.
/// </para>
/// </summary>
public interface ICountryRepository
{
    /// <summary>
    /// Returns one country by its <c>ID</c>, untracked, or <see langword="null"/>
    /// when no row has that id.
    /// <para>
    /// Null is an ordinary answer here, not an error. Since the timesheet save
    /// stopped resolving anything, it stores <c>CountryID</c> without checking
    /// it, so a setup can genuinely name a country the lookup has no row for -
    /// and a read of that setup has to be able to say so.
    /// </para>
    /// <para>
    /// The whole row rather than just <c>Name</c>: the entity is what the
    /// caller already works in, and this table is narrow enough that projecting
    /// one column would buy nothing.
    /// </para>
    /// </summary>
    Task<Country?> GetByIdAsync(
        int countryId,
        CancellationToken cancellationToken = default);

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
