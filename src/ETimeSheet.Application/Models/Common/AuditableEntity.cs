namespace ETimeSheet.Application.Models.Common;

/// <summary>
/// Base class for persisted entities that carry the database's audit and
/// soft-delete columns. The property names mirror the existing SQL Server
/// schema (<c>CreatedBy</c>, <c>CreateDate</c>, <c>UpdatedBy</c>,
/// <c>UpdateDate</c>, <c>DeletedBy</c>, <c>DeleteDate</c>) because the database
/// is the source of truth: this is a database-first model.
/// <para>
/// Values are populated centrally by an EF Core save interceptor, so no service
/// or repository has to remember to set them.
/// </para>
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>User who created the row. Null for rows created by a background process.</summary>
    public int? CreatedBy { get; set; }

    public DateTime? CreateDate { get; set; }

    public int? UpdatedBy { get; set; }

    public DateTime? UpdateDate { get; set; }

    /// <summary>
    /// Soft-delete marker: <c>int</c>, not null, where 1 means deleted. Modelled
    /// as an int rather than a bool because that is the column's real type, and
    /// because the filter has to agree exactly with the stored procedures, which
    /// test it as <c>IsDeleted &lt;&gt; 1</c>. A global query filter hides deleted
    /// rows, so normal queries never have to mention the column.
    /// </summary>
    public int IsDeleted { get; set; }

    /// <summary>When the row was soft-deleted. Audit detail only - <see cref="IsDeleted"/> is what queries filter on.</summary>
    public DateTime? DeleteDate { get; set; }

    public int? DeletedBy { get; set; }
}
