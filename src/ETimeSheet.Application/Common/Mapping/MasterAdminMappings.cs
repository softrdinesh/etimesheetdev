using ETimeSheet.Application.Models;

namespace ETimeSheet.Application.Common.Mapping;

/// <summary>
/// Hand-written projections between the Master Admin procedure rows and the
/// Master Admin DTOs. Explicit, for the same reason as
/// <see cref="AdminMappings"/>: a convention-based mapper would silently drop a
/// column the day one of these names changes.
/// </summary>
internal static class MasterAdminMappings
{
    internal static ClientResponse ToClientResponse(this ClientListDetail client) => new()
    {
        ClientId = client.ClientId,
        ClientCode = client.ClientCode,
        ClientName = client.ClientName,
        Avatar = client.Avatar,
        BillingEmail = client.BillingEmail,
        PhoneNumber = client.PhoneNumber,
        AlternatePhoneNumber = client.AlternatePhoneNumber,
        AlternateEmail = client.AlternateEmail,
        BillingAddress = client.BillingAddress,
        PaymentTermsDays = client.PaymentTermsDays,
        CurrencyId = client.CurrencyId,
        CurrencyName = client.CurrencyName,
        CurrencySymbol = client.CurrencySymbol,
        CountryId = client.CountryId,
        TaxIdentifier = client.TaxIdentifier,
        OrganizationId = client.OrganizationId,
    };

    /// <summary>Projects the saved client to the same shape the client list returns.</summary>
    internal static ClientResponse ToClientResponse(this ClientSaveDetail client) => new()
    {
        ClientId = client.ClientId,
        ClientCode = client.ClientCode,
        ClientName = client.ClientName,
        Avatar = client.Avatar,
        BillingEmail = client.BillingEmail,
        PhoneNumber = client.PhoneNumber,
        AlternatePhoneNumber = client.AlternatePhoneNumber,
        AlternateEmail = client.AlternateEmail,
        BillingAddress = client.BillingAddress,
        PaymentTermsDays = client.PaymentTermsDays,
        CurrencyId = client.CurrencyId,
        CurrencyName = client.CurrencyName,
        CurrencySymbol = client.CurrencySymbol,
        CountryId = client.CountryId,
        TaxIdentifier = client.TaxIdentifier,
        OrganizationId = client.OrganizationId,
    };

    /// <summary>Projects an organisation row from <c>spc_GetOrganizationList</c> to the organisation response.</summary>
    internal static OrganizationResponse ToOrganizationResponse(this OrganizationListDetail organization) => new()
    {
        OrganizationId = organization.OrganizationId,
        OrganizationName = organization.OrganizationName,
        CountryId = organization.CountryId,
    };
}
