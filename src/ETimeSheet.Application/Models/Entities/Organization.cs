namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// An organisation - a company - mapped onto the existing <c>dbo.Organization</c>
/// table. <b>Read-only</b> here: the table is maintained by another
/// application, and this API never writes to it.
/// </summary>
public class Organization
{
    /// <summary>Primary key.</summary>
    public int OrganizationId { get; set; }

    /// <summary>The company name. <c>nvarchar(max)</c>, nullable.</summary>
    public string? OrganizationName { get; set; }

    public DateTimeOffset? CreateDate { get; set; }

    public int? CountryId { get; set; }
}
