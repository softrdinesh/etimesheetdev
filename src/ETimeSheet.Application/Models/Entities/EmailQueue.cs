namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One email waiting to be sent, or already sent, mapped onto the existing
/// <c>dbo.EmailQueue</c> table. This is a persistence model and is never
/// returned from a controller.
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the table has its own
/// <c>CreatedDate</c> and no soft-delete or update columns.
/// </para>
/// </summary>
public class EmailQueue
{
    /// <summary>Primary key - a <c>bigint</c> IDENTITY.</summary>
    public long EmailQueueId { get; set; }

    /// <summary>The user it is for, when it is for one.</summary>
    public int? UserId { get; set; }

    /// <summary>Where it goes. <c>varchar(255)</c>, NOT NULL.</summary>
    public string EmailAddress { get; set; } = string.Empty;

    /// <summary>The organisation it concerns, when it concerns one.</summary>
    public int? OrgId { get; set; }

    /// <summary>The kind of email - <c>Constants.EmailType.Id</c>. <c>int</c>, NOT NULL.</summary>
    public int EmailTypeId { get; set; }

    /// <summary>How far sending has got - <c>Constants.EmailQueue.Status</c>. <c>tinyint</c>.</summary>
    public byte EmailStatusId { get; set; }

    /// <summary>
    /// When the row was queued - filled by the column's <c>GETDATE()</c>
    /// default, so SQL Server local time, never set by the API.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>When it was sent successfully.</summary>
    public DateTime? SentDate { get; set; }

    /// <summary>When sending was last attempted, successful or not.</summary>
    public DateTime? LastAttemptDate { get; set; }

    /// <summary>How many attempts have been made to send it.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Why the last attempt failed. <c>varchar(2000)</c>.</summary>
    public string? ErrorMessage { get; set; }
}
