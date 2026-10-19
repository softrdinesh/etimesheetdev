using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;
using ETimeSheet.Shared.Utilities;
using ETimeSheet.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Shared arrangements for the Admin endpoint tests: payload builders, row
/// builders, and seed / read-back helpers over
/// <c>ETimeSheetApiFactory.WithDbContextAsync</c>.
/// <para>
/// Every helper reaches the database through <c>WithDbContextAsync</c>, so the
/// throwaway-container guard runs before a single row is touched. Rows only -
/// nothing here creates, alters or drops anything.
/// </para>
/// <para>
/// <b>Ids are picked by the test wherever a test edits or deletes by id.</b>
/// The reset reseeds identity to 0, and the first row a never-used table
/// receives after that gets id <b>0</b> - which the add-or-edit endpoints read
/// as "add", and which the delete validators refuse. So those rows go in with
/// <c>IDENTITY_INSERT</c>, as <c>SeedClientsAsync</c> does.
/// </para>
/// </summary>
public static class AdminTestData
{
    /// <summary>The organisation under test.</summary>
    public const int OrganizationId = 900;

    /// <summary>A second organisation, so the by-organisation filters have something to exclude.</summary>
    public const int OtherOrganizationId = 901;

    public const int UserId = 6001;
    public const int OtherUserId = 6002;
    public const int ThirdUserId = 6003;
    public const int FourthUserId = 6004;

    /// <summary>Whoever the payload says is acting - authentication is off, so it comes from the body.</summary>
    public const int ActingUserId = 42;

    /// <summary>Someone other than <see cref="ActingUserId"/>, who created a seeded row.</summary>
    public const int OriginalCreatorId = 7;

    public const int IndiaCountryId = 103;
    public const string IndiaName = "India";
    public const string IndiaZone = "Asia/Kolkata";

    public const int FullTime = Constants.TimesheetMasterSetup.ContractType.FullTime;
    public const int PartTime = Constants.TimesheetMasterSetup.ContractType.PartTime;

    /// <summary>A fixed instant, earlier than the frozen clock, for rows a test arranges as "created before".</summary>
    public static readonly DateTime EarlierDate = new(2026, 1, 15, 9, 30, 0);

    // ================================================================ payloads

    /// <summary>
    /// A setup save payload that passes validation and the service's time
    /// checks; a test changes only what it is about.
    /// </summary>
    public static AdminSaveRequest SaveRequest(
        int userId = UserId,
        int? countryId = IndiaCountryId,
        string? timeZone = IndiaZone,
        int createdBy = ActingUserId) => new()
    {
        UserId = userId,
        MaxTimeInHrs = "08:00:00",
        MaxTimInMins = "00:30:00",
        OrganizationId = OrganizationId,
        ContractType = FullTime,
        StartDay = Constants.DayMaster.DayId.Monday,
        EndDay = Constants.DayMaster.DayId.Friday,
        ExceptionDay = Constants.DayMaster.DayId.Saturday,
        CountryId = countryId,
        TimeZone = timeZone,
        TimeEntryLockAt = "18:00:00",
        NeedToSendReminder = true,
        ReminderTimeBeforeCutoff = "01:00:00",
        CreatedBy = createdBy,
    };

    public static AdminDeleteRequest DeleteRequest(int setupId, int deletedBy = ActingUserId) => new()
    {
        SetupId = setupId,
        DeletedBy = deletedBy,
    };

    public static UserRoleSaveRequest RoleSaveRequest(
        string? roleName,
        int? roleId = null,
        int organizationId = OrganizationId,
        int createdBy = ActingUserId) => new()
    {
        RoleId = roleId,
        OrganizationId = organizationId,
        RoleName = roleName,
        CreatedBy = createdBy,
    };

    public static UserRoleDeleteRequest RoleDeleteRequest(
        int roleId,
        int organizationId = OrganizationId,
        int deletedBy = ActingUserId) => new()
    {
        RoleId = roleId,
        OrganizationId = organizationId,
        DeletedBy = deletedBy,
    };

    public static ProjectCategorySaveRequest CategorySaveRequest(
        string? categoryName,
        int? categoryId = null,
        int organizationId = OrganizationId,
        int createdBy = ActingUserId) => new()
    {
        CategoryId = categoryId,
        OrganizationId = organizationId,
        CategoryName = categoryName,
        CreatedBy = createdBy,
    };

    public static ProjectCategoryDeleteRequest CategoryDeleteRequest(
        int categoryId,
        int organizationId = OrganizationId,
        int deletedBy = ActingUserId) => new()
    {
        CategoryId = categoryId,
        OrganizationId = organizationId,
        DeletedBy = deletedBy,
    };

    // ================================================================ rows

    /// <summary>A <c>dbo.Country</c> row. The table is emptied before every test, so each test seeds its own.</summary>
    public static Country Country(int id, string? name, string? timeZone) => new()
    {
        Id = id,
        Name = name,
        TimeZone = timeZone,
    };

    /// <summary>
    /// A <c>dbo.TimesheetMasterSetup</c> row, deliberately different from
    /// <see cref="SaveRequest"/> in every caller-settable column, so an update
    /// that missed a column would show.
    /// </summary>
    public static TimesheetMasterSetup Setup(
        int setupId,
        int userId = UserId,
        bool? isDelete = false,
        int? contractType = PartTime,
        int? startDay = Constants.DayMaster.DayId.Tuesday,
        int? endDay = Constants.DayMaster.DayId.Thursday,
        TimeSpan? maxTimeInHrs = null,
        TimeSpan? maxTimInMins = null,
        int? countryId = IndiaCountryId,
        string? timeZone = IndiaZone) => new()
    {
        SetupId = setupId,
        UserId = userId,
        OrganizationId = OtherOrganizationId,
        ContractType = contractType,
        StartDay = startDay,
        EndDay = endDay,
        ExceptionDay = null,
        MaxTimeInHrs = maxTimeInHrs ?? TimeSpan.FromHours(6),
        MaxTimInMins = maxTimInMins ?? TimeSpan.FromMinutes(15),
        CountryId = countryId,
        TimeZone = timeZone,
        TimeEntryLockAt = new TimeSpan(20, 0, 0),
        NeedToSendReminder = false,
        ReminderTimeBeforeCutoff = new TimeSpan(0, 30, 0),
        IsDelete = isDelete,
        CreateDate = EarlierDate,
        CreatedBy = OriginalCreatorId,
        DeleteDate = isDelete == true ? EarlierDate.AddDays(1) : null,
        DeletedBy = isDelete == true ? OriginalCreatorId : null,
    };

    /// <summary>
    /// A setup for the dashboard, whose figures come from <c>StartDay</c>,
    /// <c>EndDay</c>, the two daily-maximum columns and <c>ContractType</c>.
    /// Seeded through <c>SeedSetupsAsync</c>, which lets the database pick the id.
    /// </summary>
    public static TimesheetMasterSetup DashboardSetup(
        int userId,
        int contractType,
        int startDay = Constants.DayMaster.DayId.Monday,
        int endDay = Constants.DayMaster.DayId.Friday,
        TimeSpan? maxTimeInHrs = null,
        TimeSpan? maxTimInMins = null,
        bool isDelete = false) => new()
    {
        UserId = userId,
        OrganizationId = OrganizationId,
        ContractType = contractType,
        StartDay = startDay,
        EndDay = endDay,
        MaxTimeInHrs = maxTimeInHrs ?? TimeSpan.FromHours(8),
        MaxTimInMins = maxTimInMins ?? TimeSpan.Zero,
        IsDelete = isDelete,
    };

    /// <summary>A queued email for <paramref name="userId"/> in the given status.</summary>
    public static EmailQueue Email(int userId, byte status, string? errorMessage = null) => new()
    {
        UserId = userId,
        EmailAddress = $"user{userId}@etimesheet.test",
        OrgId = OrganizationId,
        EmailTypeId = Constants.EmailType.Id.TimeLogReminder,
        EmailStatusId = status,
        CreatedDate = EarlierDate,
        AttemptCount = 0,
        ErrorMessage = errorMessage,
    };

    public static TimesheetUserRole Role(
        int roleId,
        string? roleName,
        int? organizationId = OrganizationId,
        bool? isDelete = false) => new()
    {
        RoleId = roleId,
        RoleName = roleName,
        OrganizationId = organizationId,
        CreateDate = EarlierDate,
        CreatedBy = OriginalCreatorId,
        IsDelete = isDelete,
        DeletedDate = isDelete == true ? EarlierDate.AddDays(1) : null,
        DeletedBy = isDelete == true ? OriginalCreatorId : null,
    };

    public static TimesheetProjectCategory Category(
        int categoryId,
        string? categoryName,
        int organizationId = OrganizationId,
        bool? isDelete = false) => new()
    {
        CategoryId = categoryId,
        CategoryName = categoryName,
        OrganizationId = organizationId,
        CreateDate = EarlierDate,
        CreatedBy = OriginalCreatorId,
        IsDelete = isDelete,
        DeletedDate = isDelete == true ? EarlierDate.AddDays(1) : null,
        DeletedBy = isDelete == true ? OriginalCreatorId : null,
    };

    // ================================================================ seed / read back

    public static Task SeedAdminCountriesAsync(this ETimeSheetApiFactory factory, params Country[] countries) =>
        factory.WithDbContextAsync(async db =>
        {
            db.Country.AddRange(countries);
            await db.SaveChangesAsync();
        });

    /// <summary>Inserts setups with the ids the test picked.</summary>
    public static Task SeedAdminSetupsWithIdsAsync(this ETimeSheetApiFactory factory, params TimesheetMasterSetup[] setups) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var s in setups)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetMasterSetup ON;
                    INSERT INTO dbo.TimesheetMasterSetup
                        (SetupID, MaxTimeinhrs, MaxTiminmins, UserID, OrganizationID, ContractType,
                         CreateDate, CreatedBy, StartDay, EndDay, CountryID, IsDelete,
                         UpdateDate, UpdatedBy, DeleteDate, Deletedby, Exceptionday,
                         TimeEntryLockAt, TimeZone, NeedToSendReminder, ReminderTimeBeforeCutoff)
                    VALUES
                        ({s.SetupId}, {s.MaxTimeInHrs}, {s.MaxTimInMins}, {s.UserId}, {s.OrganizationId}, {s.ContractType},
                         {s.CreateDate}, {s.CreatedBy}, {s.StartDay}, {s.EndDay}, {s.CountryId}, {s.IsDelete},
                         {s.UpdateDate}, {s.UpdatedBy}, {s.DeleteDate}, {s.DeletedBy}, {s.ExceptionDay},
                         {s.TimeEntryLockAt}, {s.TimeZone}, {s.NeedToSendReminder}, {s.ReminderTimeBeforeCutoff});
                    SET IDENTITY_INSERT dbo.TimesheetMasterSetup OFF;");
            }
        });

    /// <summary>Every setup row a user has, deleted or not, lowest id first.</summary>
    public static Task<List<TimesheetMasterSetup>> FindAdminSetupsForUserAsync(this ETimeSheetApiFactory factory, int userId) =>
        factory.WithDbContextAsync(db => db.TimesheetMasterSetup
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(setup => setup.UserId == userId)
            .OrderBy(setup => setup.SetupId)
            .ToListAsync());

    public static Task<TimesheetMasterSetup?> FindAdminSetupAsync(this ETimeSheetApiFactory factory, int setupId) =>
        factory.WithDbContextAsync(db => db.TimesheetMasterSetup
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(setup => setup.SetupId == setupId));

    public static Task SeedAdminEmailsAsync(this ETimeSheetApiFactory factory, params EmailQueue[] emails) =>
        factory.WithDbContextAsync(async db =>
        {
            db.EmailQueue.AddRange(emails);
            await db.SaveChangesAsync();
        });

    public static Task<List<EmailQueue>> FindAdminEmailsForUserAsync(this ETimeSheetApiFactory factory, int userId) =>
        factory.WithDbContextAsync(db => db.EmailQueue
            .AsNoTracking()
            .Where(email => email.UserId == userId)
            .OrderBy(email => email.EmailQueueId)
            .ToListAsync());

    /// <summary>Inserts roles with the ids the test picked.</summary>
    public static Task SeedAdminRolesAsync(this ETimeSheetApiFactory factory, params TimesheetUserRole[] roles) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var r in roles)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetUserRole ON;
                    INSERT INTO dbo.TimesheetUserRole
                        (RoleID, Rolename, OrganizationID, CreateDate, Createdby, IsDelete, Deleteddate, Deletedby)
                    VALUES
                        ({r.RoleId}, {r.RoleName}, {r.OrganizationId}, {r.CreateDate}, {r.CreatedBy},
                         {r.IsDelete}, {r.DeletedDate}, {r.DeletedBy});
                    SET IDENTITY_INSERT dbo.TimesheetUserRole OFF;");
            }
        });

    public static Task<TimesheetUserRole?> FindAdminRoleAsync(this ETimeSheetApiFactory factory, int roleId) =>
        factory.WithDbContextAsync(db => db.TimesheetUserRole
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(role => role.RoleId == roleId));

    /// <summary>Every role row of one organisation, deleted or not.</summary>
    public static Task<List<TimesheetUserRole>> FindAdminRolesAsync(this ETimeSheetApiFactory factory, int organizationId) =>
        factory.WithDbContextAsync(db => db.TimesheetUserRole
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(role => role.OrganizationId == organizationId)
            .OrderBy(role => role.RoleId)
            .ToListAsync());

    /// <summary>Inserts categories with the ids the test picked.</summary>
    public static Task SeedAdminCategoriesAsync(this ETimeSheetApiFactory factory, params TimesheetProjectCategory[] categories) =>
        factory.WithDbContextAsync(async db =>
        {
            foreach (var c in categories)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
                    SET IDENTITY_INSERT dbo.TimesheetProjectCategory ON;
                    INSERT INTO dbo.TimesheetProjectCategory
                        (ID, Categoryname, OrganizationID, IsDelete, CreateDate, CreatedBy, DeletedDate, Deletedby)
                    VALUES
                        ({c.CategoryId}, {c.CategoryName}, {c.OrganizationId}, {c.IsDelete}, {c.CreateDate},
                         {c.CreatedBy}, {c.DeletedDate}, {c.DeletedBy});
                    SET IDENTITY_INSERT dbo.TimesheetProjectCategory OFF;");
            }
        });

    public static Task<TimesheetProjectCategory?> FindAdminCategoryAsync(this ETimeSheetApiFactory factory, int categoryId) =>
        factory.WithDbContextAsync(db => db.TimesheetProjectCategory
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(category => category.CategoryId == categoryId));

    /// <summary>Every category row of one organisation, deleted or not.</summary>
    public static Task<List<TimesheetProjectCategory>> FindAdminCategoriesAsync(this ETimeSheetApiFactory factory, int organizationId) =>
        factory.WithDbContextAsync(db => db.TimesheetProjectCategory
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(category => category.OrganizationId == organizationId)
            .OrderBy(category => category.CategoryId)
            .ToListAsync());
}
