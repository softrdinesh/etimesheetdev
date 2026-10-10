namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// A user, mapped onto the existing <c>dbo.Signup</c> table. <b>Read-only</b>
/// here: the table is maintained by another application, and this API never
/// writes to it.
/// <para>
/// <b>The credential columns are deliberately not mapped</b> - <c>Pwd</c>,
/// <c>ResetOTP</c> and <c>OtpExpireAt</c>. Nothing here needs them, and an
/// unmapped column can never be loaded, logged or serialised by accident.
/// </para>
/// </summary>
public class Signup
{
    /// <summary>Primary key.</summary>
    public int UserId { get; set; }

    /// <summary><c>nvarchar(100)</c>, NOT NULL.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary><c>nvarchar(50)</c>, NOT NULL.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Maps the <c>Accountsource</c> column. <c>nvarchar(10)</c>.</summary>
    public string? AccountSource { get; set; }

    public DateTimeOffset CreateDate { get; set; }

    public int? CountryId { get; set; }

    /// <summary><c>nvarchar(20)</c>.</summary>
    public string? Latitude { get; set; }

    /// <summary>Maps the <c>Longtitude</c> column - the database's spelling. <c>nvarchar(20)</c>.</summary>
    public string? Longitude { get; set; }

    public DateTimeOffset? UpdatedDate { get; set; }

    public int? UpdateBy { get; set; }

    /// <summary><c>nvarchar(50)</c>.</summary>
    public string? OrganizationName { get; set; }

    /// <summary><c>nvarchar(15)</c>.</summary>
    public string? OrganizationSize { get; set; }

    /// <summary><c>nvarchar(max)</c>.</summary>
    public string? Address { get; set; }

    public int? OrganizationId { get; set; }

    /// <summary>
    /// Soft-delete marker - an <c>int</c>, nullable. Every procedure treats only
    /// <c>0</c> as live, so a NULL here is <b>not</b> live either.
    /// </summary>
    public int? IsDelete { get; set; }

    /// <summary><c>nvarchar(200)</c>.</summary>
    public string? ProfilePicture { get; set; }

    public bool? IsProductOwner { get; set; }

    public int? RoleId { get; set; }
}
