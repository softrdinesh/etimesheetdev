using ETimeSheet.Application.Models;
using ETimeSheet.Application.Models.Entities;

namespace ETimeSheet.Tests.TestData;

/// <summary>
/// Shared arrangements for the Master Admin (client setup) tests.
/// </summary>
public static class MasterAdminTestData
{
    /// <summary>
    /// India. Its <c>dbo.CountryCurrency_Lookup</c> row, inserted by the recorded
    /// schema script, is <see cref="IndiaCurrencyId"/> - the Indian Rupee.
    /// </summary>
    public const int IndiaCountryId = 103;

    public const int IndiaCurrencyId = 102;

    /// <summary>The United States, whose currency row is <see cref="UnitedStatesCurrencyId"/>.</summary>
    public const int UnitedStatesCountryId = 236;

    public const int UnitedStatesCurrencyId = 235;

    /// <summary>Antarctica - the one country the schema script gives no currency.</summary>
    public const int CountryWithoutCurrencyId = 8;

    /// <summary>The organisation every test client belongs to; <c>OrganizationID</c> is NOT NULL.</summary>
    public const int OrganizationId = 12;

    /// <summary>
    /// A save payload that passes validation; a test changes only what it is
    /// about. Each call carries a different billing email.
    /// </summary>
    public static ClientSaveRequest ValidRequest(
        int? clientId = null,
        string clientName = "Acme Consulting",
        int? countryId = IndiaCountryId) => new()
    {
        ClientId = clientId,
        ClientName = clientName,
        // Unique per call: billing emails must be unique among live clients,
        // and several tests add more than one client from this payload.
        BillingEmail = $"billing-{Guid.NewGuid():N}@acme.test",
        PhoneNumber = "+91 98765 43210",
        AlternatePhoneNumber = "+91 91234 56789",
        AlternateEmail = "accounts@acme.test",
        BillingAddress = "12 MG Road, Bengaluru",
        PaymentTermsDays = 30,
        CountryId = countryId,
        TaxIdentifier = "29ABCDE1234F1Z5",
        OrganizationId = OrganizationId,
    };

    /// <summary>A client row as it would sit in <c>dbo.TimesheetClientSetup</c>.</summary>
    public static TimesheetClientSetup Client(
        int clientId,
        string clientCode,
        string clientName = "Seeded Client",
        int countryId = IndiaCountryId,
        int currencyId = IndiaCurrencyId,
        bool isDeleted = false) => new()
    {
        ClientId = clientId,
        ClientCode = clientCode,
        ClientName = clientName,
        BillingEmail = $"client{clientId}@etimesheet.test",
        PhoneNumber = "0000000000",
        PaymentTermsDays = 15,
        CurrencyCode = currencyId,
        CountryId = countryId,
        IsDeleted = isDeleted,
        DeleteDate = isDeleted ? TimeLogTestData.Now.AddDays(-1) : null,
        OrganizationId = OrganizationId,
    };

    /// <summary>A row as <c>spc_AddEditClientSetup</c> returns it.</summary>
    public static ClientSaveDetail SavedRow(int clientId = 5, string clientCode = "CL0005") => new()
    {
        ClientId = clientId,
        ClientCode = clientCode,
        ClientName = "Acme Consulting",
        Avatar = "acme.png",
        BillingEmail = "billing@acme.test",
        PhoneNumber = "+91 98765 43210",
        AlternatePhoneNumber = "+91 91234 56789",
        AlternateEmail = "accounts@acme.test",
        BillingAddress = "12 MG Road, Bengaluru",
        PaymentTermsDays = 30,
        CurrencyId = IndiaCurrencyId,
        CurrencyName = "Indian Rupee",
        CurrencySymbol = "₹",
        CountryId = IndiaCountryId,
        TaxIdentifier = "29ABCDE1234F1Z5",
        OrganizationId = OrganizationId,
    };
}
