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
/// It has no matching service, and needs none - there is no Country module and
/// no Country endpoint. It exists because <c>AdminService</c> has to read the
/// country a timesheet setup names, and a service combining two repositories is
/// exactly how that is done (CLAUDE.md §6). Give it a service the day something
/// asks for a country endpoint, not before.
/// </para>
/// </summary>
public interface ICountryRepository
{
    /// <summary>
    /// Returns one country by its <c>ID</c>, untracked, or null when no row has
    /// that id.
    /// <para>
    /// The whole row rather than just its <c>TimeZone</c> column: "the country
    /// does not exist" and "the country exists but names no zone" are different
    /// answers, and a bare string cannot tell them apart. Deciding what each one
    /// means is the service's job.
    /// </para>
    /// </summary>
    Task<Country?> GetByIdAsync(
        int countryId,
        CancellationToken cancellationToken = default);
}
