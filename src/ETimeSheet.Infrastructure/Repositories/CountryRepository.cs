using ETimeSheet.Application.Interfaces.Repositories;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core data access over the existing <c>dbo.Country</c>
/// lookup table.
/// <para>
/// Reads only. The table is maintained by hand outside this repository, so
/// there is deliberately nothing here that writes to it.
/// </para>
/// </summary>
public class CountryRepository : ICountryRepository
{
    private readonly Context _db;

    public CountryRepository(Context db)
    {
        _db = db;
    }

    public async Task<Country?> GetByIdAsync(
        int countryId,
        CancellationToken cancellationToken = default) =>
        // AsNoTracking: this is a read of reference data that is never mutated,
        // so there is no reason to pay for change tracking - and no way for a
        // stray edit to a looked-up country to be saved by an unrelated
        // SaveChanges on the same request's context.
        await _db.Country
            .AsNoTracking()
            .FirstOrDefaultAsync(country => country.Id == countryId, cancellationToken);
}
