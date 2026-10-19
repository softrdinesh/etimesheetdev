using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Tests.Fixtures;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Arrangements for <c>GET api/v1/MasterAdmin/get-all-organization</c>.
/// <para>
/// <c>dbo.Organization</c> is built in the container from its EF mapping, not
/// from recorded SQL, and the API never writes to it - so these rows are the
/// only way it ever holds anything in a test.
/// </para>
/// </summary>
public static class OrganizationTestData
{
    public const int IndiaCountryId = MasterAdminTestData.IndiaCountryId;

    public const int UnitedStatesCountryId = MasterAdminTestData.UnitedStatesCountryId;

    public static Organization Row(string? name, int? countryId = IndiaCountryId) => new()
    {
        OrganizationName = name,
        CountryId = countryId,
        CreateDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
    };

    /// <summary>
    /// Inserts organisations and returns them with the ids the database
    /// generated. Ids are never asserted as literals: the reset reseeds
    /// identity, and the first row a fresh table receives can be 0.
    /// </summary>
    public static Task<Organization[]> SeedOrganizationsAsync(
        this ETimeSheetApiFactory factory,
        params Organization[] organizations) =>
        factory.WithDbContextAsync(async db =>
        {
            db.Organization.AddRange(organizations);
            await db.SaveChangesAsync();
            return organizations;
        });
}
