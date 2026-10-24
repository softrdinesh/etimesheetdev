using Microsoft.AspNetCore.Http;

/*
    THE MASTER ADMIN MODULE'S MODEL - one file, every shape the module exchanges.

    MasterAdminController and MasterAdminService have exactly one model file
    between them, and this is it. Adding a shape to this module means adding a
    class HERE, not adding a file next to it.

    ORDER MATTERS in the response types. System.Text.Json writes properties in
    declaration order, so moving a property up or down here changes the JSON a
    client receives. Reordering is a contract change, not a tidy-up.
*/

namespace ETimeSheet.Application.Models;

/// <summary>
/// Body of <c>add-or-edit-client-setup</c> - <b>one payload for both add and
/// edit</b>.
/// <para>
/// <see cref="ClientId"/> decides which: absent or <c>0</c> adds a new client,
/// anything greater edits that client. There is no <c>ClientCode</c> here: it
/// is generated on add (<c>CL0001</c>, <c>CL0002</c> ...) and never changes.
/// There is no currency either: it is derived from <see cref="CountryId"/>.
/// </para>
/// <para>
/// <b>Sent as <c>multipart/form-data</c>, not JSON</b>, so that
/// <see cref="ClientLogo"/> can travel with the rest of the fields.
/// </para>
/// </summary>
public class ClientSaveRequest
{
    /// <summary>
    /// Absent or <c>0</c> to add a new client; the id of a live client to edit
    /// it. An id with no live (non-deleted) client is a 404.
    /// </summary>
    /// <example>0</example>
    public int? ClientId { get; set; }

    /// <summary>Required, at most 50 characters.</summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Optional. The client's logo - <b>the only way to set the avatar</b>;
    /// there is no avatar field in this payload. The file is uploaded to the
    /// document service under the <c>TSCLT</c> project code, and the
    /// <c>path</c> the service answers with is stored in the <c>Avatar</c>
    /// column and returned as <c>avatar</c>.
    /// <para>
    /// Left out on an edit, the client keeps the logo it already has. Left out
    /// on an add, the client has none.
    /// </para>
    /// </summary>
    public IFormFile? ClientLogo { get; set; }

    /// <summary>Required, a valid email address, at most 150 characters.</summary>
    public string? BillingEmail { get; set; }

    /// <summary>Required, at most 150 characters.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Optional, at most 150 characters.</summary>
    public string? AlternatePhoneNumber { get; set; }

    /// <summary>Optional, a valid email address when sent, at most 150 characters.</summary>
    public string? AlternateEmail { get; set; }

    /// <summary>Optional, any length.</summary>
    public string? BillingAddress { get; set; }

    /// <summary>Required, 0 or more.</summary>
    /// <example>30</example>
    public int? PaymentTermsDays { get; set; }

    /// <summary>
    /// Required, greater than 0, and a country with a currency in
    /// <c>dbo.CountryCurrency_Lookup</c>. The client's currency is <b>derived from
    /// this</b> - it is not sent - so changing a client's country on edit
    /// changes its currency too.
    /// </summary>
    /// <example>103</example>
    public int? CountryId { get; set; }

    /// <summary>Optional, at most 50 characters.</summary>
    public string? TaxIdentifier { get; set; }

    /// <summary>
    /// The organisation the client belongs to. Required, greater than 0.
    /// Written on add and on edit.
    /// </summary>
    /// <example>12</example>
    public int? OrganizationId { get; set; }
}

/// <summary>
/// Body of <c>remove-client-setup</c>.
/// <para>
/// A <b>soft</b> delete: the row is never removed. It is marked
/// <c>IsDeleted = 1</c> and stamped with <c>DeleteDate</c>, and from then on
/// the client list and <c>add-or-edit-client-setup</c> no longer see it. Its
/// client code stays spent and is never reissued.
/// </para>
/// </summary>
public class ClientDeleteRequest
{
    /// <summary>The client to remove. Must identify a live, non-deleted client.</summary>
    public int ClientId { get; set; }
}

/// <summary>
/// One client from <c>dbo.TimesheetClientSetup</c>, as the client list returns
/// it and as <c>add-or-edit-client-setup</c> returns the saved row. Soft-deleted clients never appear, so the delete columns are not here.
/// </summary>
public class ClientResponse
{
    public int ClientId { get; set; }

    public string ClientCode { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public string BillingEmail { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? AlternatePhoneNumber { get; set; }

    public string? AlternateEmail { get; set; }

    public string? BillingAddress { get; set; }

    public int PaymentTermsDays { get; set; }

    /// <summary>
    /// The client's <c>dbo.CountryCurrency_Lookup.CurrencyID</c>, derived from its
    /// country. Stored in the <c>Currencycode</c> column.
    /// </summary>
    public int CurrencyId { get; set; }

    /// <summary>
    /// From <c>dbo.CountryCurrency_Lookup</c> - <c>"Indian Rupee"</c>. Null only for a
    /// client saved before currencies were derived, whose id names no row.
    /// </summary>
    public string? CurrencyName { get; set; }

    /// <summary>
    /// From <c>dbo.CountryCurrency_Lookup</c> - <c>"₹"</c>. Not unique: <c>"$"</c> is
    /// shared by many currencies, so show it beside <see cref="CurrencyName"/>.
    /// </summary>
    public string? CurrencySymbol { get; set; }

    public int? CountryId { get; set; }

    public string? TaxIdentifier { get; set; }

    public int OrganizationId { get; set; }
}

/// <summary>
/// One row of <c>dbo.spc_GetClientListForMasterAdmin</c> - a live client.
/// Property names follow the procedure's column aliases; the mapping is in
/// <c>Context.ConfigureStoredProcedureResults</c>.
/// </summary>
public class ClientListDetail
{
    public int ClientId { get; set; }

    public string ClientCode { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public string BillingEmail { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? AlternatePhoneNumber { get; set; }

    public string? AlternateEmail { get; set; }

    public string? BillingAddress { get; set; }

    public int PaymentTermsDays { get; set; }

    public int CurrencyId { get; set; }

    public string? CurrencyName { get; set; }

    public string? CurrencySymbol { get; set; }

    public int? CountryId { get; set; }

    public string? TaxIdentifier { get; set; }

    public int OrganizationId { get; set; }
}

/// <summary>
/// The row <c>dbo.spc_AddEditClientSetup</c> returns - the client just saved.
/// The same columns as <see cref="ClientListDetail"/>, but its own type: EF
/// Core maps one CLR type to one result set, and each procedure has its own.
/// </summary>
public class ClientSaveDetail
{
    public int ClientId { get; set; }

    public string ClientCode { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public string BillingEmail { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? AlternatePhoneNumber { get; set; }

    public string? AlternateEmail { get; set; }

    public string? BillingAddress { get; set; }

    public int PaymentTermsDays { get; set; }

    public int CurrencyId { get; set; }

    public string? CurrencyName { get; set; }

    public string? CurrencySymbol { get; set; }

    public int? CountryId { get; set; }

    public string? TaxIdentifier { get; set; }

    public int OrganizationId { get; set; }
}

/// <summary>
/// One organisation from <c>dbo.Organization</c>, as
/// <c>get-all-organization</c> returns it.
/// </summary>
public class OrganizationResponse
{
    public int OrganizationId { get; set; }

    /// <summary>The company name. Nullable on the table.</summary>
    public string? OrganizationName { get; set; }

    /// <summary>Presumably <c>dbo.Country.ID</c>; no foreign key is recorded.</summary>
    public int? CountryId { get; set; }
}

/// <summary>
/// One row of <c>dbo.spc_GetOrganizationList</c>. The mapping is in
/// <c>Context.ConfigureStoredProcedureResults</c>.
/// </summary>
public class OrganizationListDetail
{
    public int OrganizationId { get; set; }

    public string? OrganizationName { get; set; }

    public int? CountryId { get; set; }
}
