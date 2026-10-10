using ETimeSheet.Shared.Enums;

namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One background job's schedule, mapped onto the existing
/// <c>dbo.SchedulerConfiguration</c> table. Read only: the rows are maintained
/// by hand in SQL Server.
/// <para>
/// It does not derive from <c>AuditableEntity</c>: the table has no soft-delete
/// or update columns.
/// </para>
/// </summary>
public class SchedulerConfiguration
{
    /// <summary>
    /// Primary key - an IDENTITY sequence number, and the id a job is looked up
    /// by: see <c>ETimeSheet.Shared.Constants.SchedulerJobs</c>.
    /// </summary>
    public int SchedulerConfigurationId { get; set; }

    /// <summary>The job's display name. <c>varchar(150)</c>, UNIQUE.</summary>
    public string SchedulerName { get; set; } = string.Empty;

    /// <summary>Whether the job runs at all. Defaults to 1.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>How runs are spaced. <c>tinyint</c>, NOT NULL.</summary>
    public ScheduleType ScheduleTypeId { get; set; }

    /// <summary>
    /// The first run, in UTC.
    /// <c>datetime2(0)</c>, nullable.
    /// </summary>
    public DateTime? ScheduleDateTime { get; set; }

    /// <summary>Whether the repeat columns apply. Defaults to 0.</summary>
    public bool IsRepeatEnabled { get; set; }

    /// <summary>How many <see cref="RepeatIntervalType"/> units between runs; &gt; 0 when present.</summary>
    public int? RepeatInterval { get; set; }

    /// <summary><c>"H"</c> hours, <c>"M"</c> minutes, <c>"S"</c> seconds. <c>char(1)</c>.</summary>
    public string? RepeatIntervalType { get; set; }

    /// <summary>When the row was created - SQL Server local time. <c>datetime2(0)</c>.</summary>
    public DateTime CreatedDate { get; set; }
}
