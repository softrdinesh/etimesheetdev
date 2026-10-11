namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A country's currency, mapped onto the existing <c>dbo.CountryCurrency</c>
/// lookup table. This is a persistence/domain model and is never returned from
/// a controller.
/// <para>
/// <b>Read-only reference data</b>, maintained by hand in SQL Server - see
/// <c>docs/database/data/dbo.CountryCurrency_Insert.sql</c>. One row per
/// country, so a currency used by several countries (the Euro) has several rows.
/// </para>
/// </summary>
public class CountryCurrency
{
    /// <summary>Primary key - <c>IDENTITY(1,1)</c>.</summary>
    public int CurrencyId { get; set; }

    /// <summary><c>nvarchar(10)</c> - <c>"₹"</c>, <c>"€"</c>. Not unique.</summary>
    public string CurrencySymbol { get; set; } = string.Empty;

    /// <summary><c>nvarchar(100)</c> - <c>"Indian Rupee"</c>.</summary>
    public string CurrencyName { get; set; } = string.Empty;

    /// <summary><c>dbo.Country.ID</c>. No foreign key on the table.</summary>
    public int CountryId { get; set; }
}
