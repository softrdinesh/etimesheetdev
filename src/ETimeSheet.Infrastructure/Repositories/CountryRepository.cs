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

    public async Task<IReadOnlyList<Country>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        // AsNoTracking: this is reference data that is never mutated, and the
        // query materialises the entire lookup - tracking a couple of hundred
        // rows for the rest of the request buys nothing, and leaves no way for a
        // stray edit to be saved by an unrelated SaveChanges on the same
        // request's context.
        //
        // Ordered in SQL rather than in memory so the order is the database's
        // collation, and is the same for every caller. Names are nullable on
        // this table, so a nameless row sorts first under SQL Server's NULLs-low
        // ordering - it is broken reference data either way, and the service
        // decides what to do with it.
        await _db.Country
            .AsNoTracking()
            .OrderBy(country => country.Name)
            .ToListAsync(cancellationToken);
}
