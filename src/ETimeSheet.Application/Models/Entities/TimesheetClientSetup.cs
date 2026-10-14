namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A client, mapped onto the existing <c>dbo.TimesheetClientSetup</c> table.
/// This is a persistence/domain model and is never returned from a controller.
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the table has only
/// <c>IsDeleted</c> (a <c>bit</c>, not the <c>int</c> the base class carries)
/// and <c>DeleteDate</c>, and no created/updated columns.
/// </para>
/// </summary>
public class TimesheetClientSetup
{
    /// <summary>The <c>ClientID</c> column - <c>IDENTITY(1,1)</c>.</summary>
    public int ClientId { get; set; }

    /// <summary><c>nvarchar(15)</c>, NOT NULL.</summary>
    public string ClientCode { get; set; } = string.Empty;

    /// <summary>The <c>Clientname</c> column. <c>nvarchar(50)</c>, NOT NULL.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary><c>nvarchar(500)</c> - the uploaded logo's path.</summary>
    public string? Avatar { get; set; }

    /// <summary><c>nvarchar(150)</c>, NOT NULL.</summary>
    public string BillingEmail { get; set; } = string.Empty;

    /// <summary><c>nvarchar(150)</c>, NOT NULL.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    public string? AlternatePhoneNumber { get; set; }

    public string? AlternateEmail { get; set; }

    /// <summary><c>nvarchar(max)</c>.</summary>
    public string? BillingAddress { get; set; }

    /// <summary>The <c>payment_terms_days</c> column.</summary>
    public int PaymentTermsDays { get; set; }

    /// <summary>
    /// The <c>Currencycode</c> column, which holds a
    /// <c>dbo.CountryCurrency.CurrencyID</c> - derived from the country by
    /// <c>spc_AddEditClientSetup</c>, never sent by a caller.
    /// </summary>
    public int CurrencyCode { get; set; }

    /// <summary>Presumably <c>dbo.Country.ID</c>; no foreign key is recorded.</summary>
    public int? CountryId { get; set; }

    /// <summary>The <c>tax_identifier</c> column. <c>nvarchar(50)</c>.</summary>
    public string? TaxIdentifier { get; set; }

    /// <summary>Soft-delete flag. A <c>bit</c>, NOT NULL.</summary>
    public bool IsDeleted { get; set; }

    public DateTime? DeleteDate { get; set; }

    /// <summary>The <c>OrganizationID</c> column - the organisation the client belongs to. NOT NULL.</summary>
    public int OrganizationId { get; set; }
}
