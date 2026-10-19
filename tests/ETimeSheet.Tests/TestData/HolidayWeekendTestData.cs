using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Shared arrangements for the holiday and weekend setup tests of
/// <c>TimesheetProjectController</c>: the reference rows they validate
/// against, row and payload builders, and seed/read helpers over
/// <see cref="ETimeSheetApiFactory.WithDbContextAsync(Func{ETimeSheet.Infrastructure.Data.Context, Task})"/>.
/// <para>
/// Every seeded row is inserted with an explicit id under
/// <c>IDENTITY_INSERT</c>. The reset reseeds identity to 0, so the first row a
/// never-used table receives gets id <b>0</b> - which the add/edit endpoints
/// read as "add", and which the one-setup-per-project check excludes. An
/// explicit id keeps both honest.
/// </para>
/// </summary>
public static class HolidayWeekendTestData
{
    /// <summary>The organisation the tests act for.</summary>
    public const int OrganizationId = 21;

    /// <summary>A second organisation, whose rows must never leak into the first.</summary>
    public const int OtherOrganizationId = 22;

    // dbo.Country is not a lookup table in the reset, so it is empty before
    // every test; these are the only countries that exist.
    public const int IndiaCountryId = 1;
    public const string IndiaCountryName = "India";
    public const int UnitedKingdomCountryId = 2;
    public const string UnitedKingdomCountryName = "United Kingdom";

    // Clients of OrganizationId, one deleted, and one of OtherOrganizationId.
    public const int AcmeClientId = 501;
    public const string AcmeClientName = "Acme Consulting";
    public const int GlobexClientId = 502;
    public const string GlobexClientName = "Globex";
    public const int OtherOrganizationClientId = 503;
    public const int DeletedClientId = 504;

    // Projects. The names are chosen so "ordered by project name" differs
    // from "ordered by id": Mobile App (602) sorts before Website Rebuild (601).
    public const int WebsiteProjectId = 601;
    public const string WebsiteProjectCode = "PRJ-601";
    public const string WebsiteProjectName = "Website Rebuild";
    public const int MobileProjectId = 602;
    public const string MobileProjectCode = "PRJ-602";
    public const string MobileProjectName = "Mobile App";
    public const int OtherOrganizationProjectId = 603;

    /// <summary>Who created a seeded row - deliberately not anyone a test sends.</summary>
    public const int SeedCreatorUserId = 40;

    /// <summary>Who a test says is saving or removing.</summary>
    public const int EditorUserId = 41;

    /// <summary>When a seeded row was created - well before the frozen clock.</summary>
    public static readonly DateTime SeededCreateDate = new(2026, 1, 15, 9, 0, 0);

    public static readonly DateTime ChristmasDay = new(2026, 12, 25);

    public static readonly DateTime NewYearsDay = new(2027, 1, 1);

    // ================================================================ reference data

    /// <summary>
    /// Inserts the countries, clients and projects above - everything the
    /// holiday and weekend saves look up before they write.
    /// </summary>
    public static Task SeedHolidayWeekendReferenceDataAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var (id, name, code) in new[]
                     {
                         (IndiaCountryId, IndiaCountryName, "IN"),
                         (UnitedKingdomCountryId, UnitedKingdomCountryName, "GB"),
                     })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO dbo.Country (ID, Name, Code) VALUES ({id}, {name}, {code});");
            }

            foreach (var (id, name, organizationId, isDeleted) in new[]
                     {
                         (AcmeClientId, AcmeClientName, OrganizationId, false),
                         (GlobexClientId, GlobexClientName, OrganizationId, false),
                         (OtherOrganizationClientId, "Initech", OtherOrganizationId, false),
                         (DeletedClientId, "Gone Ltd", OrganizationId, true),
                     })
            {
                var code = $"CL{id}";
                var email = $"client{id}@holiday.test";

                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetClientSetup ON;
                    INSERT INTO dbo.TimesheetClientSetup
                        (ClientID, ClientCode, Clientname, BillingEmail, PhoneNumber,
                         payment_terms_days, Currencycode, CountryID, IsDeleted, OrganizationID)
                    VALUES
                        ({id}, {code}, {name}, {email}, {"0000000000"},
                         {15}, {1}, {IndiaCountryId}, {isDeleted}, {organizationId});
                    SET IDENTITY_INSERT dbo.TimesheetClientSetup OFF;");
            }

            foreach (var (id, code, name, organizationId, clientId) in new[]
                     {
                         (WebsiteProjectId, WebsiteProjectCode, WebsiteProjectName, OrganizationId, AcmeClientId),
                         (MobileProjectId, MobileProjectCode, MobileProjectName, OrganizationId, GlobexClientId),
                         (OtherOrganizationProjectId, "PRJ-603", "Other Org Project", OtherOrganizationId, OtherOrganizationClientId),
                     })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetProject ON;
                    INSERT INTO dbo.TimesheetProject (ProjectID, Code, Projectname, OrganizationID, ClientID)
                    VALUES ({id}, {code}, {name}, {organizationId}, {clientId});
                    SET IDENTITY_INSERT dbo.TimesheetProject OFF;");
            }
        });

    // ================================================================ holidays

    /// <summary>A holiday row as it would sit in <c>dbo.TimesheetHolidaysetup</c>.</summary>
    public static TimesheetHolidaySetup Holiday(
        int id,
        DateTime holidayDate,
        string? description = "Seeded Holiday",
        int organizationId = OrganizationId,
        int? countryId = null,
        int? clientId = null,
        int? projectId = null,
        bool? isDelete = false) => new()
    {
        TimesheetHolidaySetupId = id,
        HolidayDate = holidayDate,
        Description = description,
        OrganizationId = organizationId,
        CountryId = countryId,
        ClientId = clientId,
        ProjectId = projectId,
        CreateDate = SeededCreateDate,
        CreatedBy = SeedCreatorUserId,
        IsDelete = isDelete,
    };

    /// <summary>
    /// An add payload that passes validation against the reference data; a
    /// test changes only what it is about.
    /// </summary>
    public static TimesheetHolidaySetupSaveRequest HolidayRequest(
        int? id = null,
        string? description = "Christmas Day",
        int? countryId = IndiaCountryId,
        int? clientId = AcmeClientId,
        int? projectId = WebsiteProjectId) => new()
    {
        TimesheetHolidaySetupId = id,
        HolidayDate = ChristmasDay,
        Description = description,
        OrganizationId = OrganizationId,
        CountryId = countryId,
        ClientId = clientId,
        ProjectId = projectId,
        CreatedBy = EditorUserId,
    };

    /// <summary>Inserts holidays directly, with the ids they carry.</summary>
    public static Task SeedHolidaysAsync(this ETimeSheetApiFactory factory, params TimesheetHolidaySetup[] holidays) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var holiday in holidays)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetHolidaysetup ON;
                    INSERT INTO dbo.TimesheetHolidaysetup
                        (TimesheetHolidaysetupID, HolidayDate, Description, OrganizationID,
                         CountryID, ClientID, ProjectID, CreateDate, CreatedBy, IsDelete)
                    VALUES
                        ({holiday.TimesheetHolidaySetupId}, {holiday.HolidayDate}, {holiday.Description},
                         {holiday.OrganizationId}, {holiday.CountryId}, {holiday.ClientId}, {holiday.ProjectId},
                         {holiday.CreateDate}, {holiday.CreatedBy}, {holiday.IsDelete});
                    SET IDENTITY_INSERT dbo.TimesheetHolidaysetup OFF;");
            }
        });

    /// <summary>Reads one holiday straight from the table, deleted or not.</summary>
    public static Task<TimesheetHolidaySetup?> FindHolidayAsync(this ETimeSheetApiFactory factory, int id) =>
        factory.WithDbContextAsync(db => db.TimesheetHolidaysetup
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(holiday => holiday.TimesheetHolidaySetupId == id));

    /// <summary>Every holiday row in the table, deleted or not.</summary>
    public static Task<List<TimesheetHolidaySetup>> AllHolidaysAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.TimesheetHolidaysetup
            .AsNoTracking()
            .IgnoreQueryFilters()
            .ToListAsync());

    // ================================================================ weekends

    /// <summary>
    /// A weekend setup row as it would sit in <c>dbo.TimeSheet_Weekend_setup</c>:
    /// Saturday and Sunday off unless a test says otherwise.
    /// </summary>
    public static TimesheetWeekendSetup WeekendSetup(
        int id,
        int? projectId,
        int organizationId = OrganizationId,
        int? clientId = null,
        bool isFridayWeekend = false,
        bool? isDelete = false) => new()
    {
        TimeSheetWeekendSetupId = id,
        OrganizationId = organizationId,
        ClientId = clientId,
        ProjectId = projectId,
        IsMondayWeekend = false,
        IsTuesdayWeekend = false,
        IsWednesdayWeekend = false,
        IsThursdayWeekend = false,
        IsFridayWeekend = isFridayWeekend,
        IsSaturdayWeekend = true,
        IsSundayWeekend = true,
        CreateDate = SeededCreateDate,
        CreatedBy = SeedCreatorUserId,
        IsDelete = isDelete,
    };

    /// <summary>
    /// An add payload for <see cref="WebsiteProjectId"/> that passes validation:
    /// Friday and Saturday off, so it differs from what
    /// <see cref="WeekendSetup"/> seeds.
    /// </summary>
    public static TimesheetWeekendSetupSaveRequest WeekendRequest(
        int? id = null,
        int projectId = WebsiteProjectId,
        int? clientId = AcmeClientId,
        int organizationId = OrganizationId) => new()
    {
        TimeSheetWeekendSetupId = id,
        OrganizationId = organizationId,
        ClientId = clientId,
        ProjectId = projectId,
        IsMondayWeekend = false,
        IsTuesdayWeekend = false,
        IsWednesdayWeekend = false,
        IsThursdayWeekend = false,
        IsFridayWeekend = true,
        IsSaturdayWeekend = true,
        IsSundayWeekend = false,
        UserId = EditorUserId,
    };

    /// <summary>Inserts weekend setups directly, with the ids they carry.</summary>
    public static Task SeedWeekendSetupsAsync(this ETimeSheetApiFactory factory, params TimesheetWeekendSetup[] setups) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var setup in setups)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimeSheet_Weekend_setup ON;
                    INSERT INTO dbo.TimeSheet_Weekend_setup
                        (TimeSheetWeekendSetupID, OrgID, ClientID, ProjectID,
                         IsMondayWeekend, IsTuesdayWeekend, IsWednesdayWeekend, IsThursdayWeekend,
                         IsFridayWeekend, IsSaturdayWeekend, IsSundayWeekend,
                         CreateDate, CreatedBy, IsDelete)
                    VALUES
                        ({setup.TimeSheetWeekendSetupId}, {setup.OrganizationId}, {setup.ClientId}, {setup.ProjectId},
                         {setup.IsMondayWeekend}, {setup.IsTuesdayWeekend}, {setup.IsWednesdayWeekend}, {setup.IsThursdayWeekend},
                         {setup.IsFridayWeekend}, {setup.IsSaturdayWeekend}, {setup.IsSundayWeekend},
                         {setup.CreateDate}, {setup.CreatedBy}, {setup.IsDelete});
                    SET IDENTITY_INSERT dbo.TimeSheet_Weekend_setup OFF;");
            }
        });

    /// <summary>Reads one weekend setup straight from the table, deleted or not.</summary>
    public static Task<TimesheetWeekendSetup?> FindWeekendSetupAsync(this ETimeSheetApiFactory factory, int id) =>
        factory.WithDbContextAsync(db => db.TimeSheet_Weekend_setup
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(setup => setup.TimeSheetWeekendSetupId == id));

    /// <summary>Every weekend setup row in the table, deleted or not.</summary>
    public static Task<List<TimesheetWeekendSetup>> AllWeekendSetupsAsync(this ETimeSheetApiFactory factory) =>
        factory.WithDbContextAsync(db => db.TimeSheet_Weekend_setup
            .AsNoTracking()
            .IgnoreQueryFilters()
            .ToListAsync());
}
