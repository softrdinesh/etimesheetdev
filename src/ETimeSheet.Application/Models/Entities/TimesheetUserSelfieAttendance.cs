namespace ETimeSheet.Application.Models.Entities;

/// <summary>
/// One attendance session of one user on one project, taken by selfie, mapped
/// onto the <c>dbo.TimesheetUserSelfieAttendance</c> table. The first selfie
/// opens the session (check-in); the next one closes it (check-out). A row
/// with no <see cref="CheckOutDateTimeUtc"/> is a session still open.
/// <para>
/// A persistence/domain model, never returned from a controller.
/// </para>
/// </summary>
public class TimesheetUserSelfieAttendance
{
    /// <summary>The <c>AttendanceID</c> column - <c>bigint IDENTITY(1,1)</c>, the primary key.</summary>
    public long AttendanceId { get; set; }

    /// <summary>The <c>UserID</c> column - <c>dbo.Signup.UserID</c>. No foreign key.</summary>
    public int UserId { get; set; }

    /// <summary>The <c>ProjectID</c> column - <c>dbo.TimesheetProject.ProjectID</c>. No foreign key.</summary>
    public int ProjectId { get; set; }

    /// <summary>The <c>OrganizationID</c> column - the project's organisation. No foreign key.</summary>
    public int OrganizationId { get; set; }

    /// <summary><c>datetime2(0)</c>, UTC, NOT NULL - the server's clock when the check-in selfie arrived.</summary>
    public DateTime CheckInDateTimeUtc { get; set; }

    /// <summary><c>nvarchar(500)</c>, NOT NULL - where the document service stored the check-in selfie.</summary>
    public string CheckInSelfiePath { get; set; } = string.Empty;

    /// <summary><c>nvarchar(500)</c>, as sent by the device.</summary>
    public string? CheckInLatitude { get; set; }

    /// <summary><c>nvarchar(500)</c>, as sent by the device.</summary>
    public string? CheckInLongitude { get; set; }

    /// <summary><c>datetime2(0)</c>, UTC. <see langword="null"/> while the session is open.</summary>
    public DateTime? CheckOutDateTimeUtc { get; set; }

    /// <summary><c>nvarchar(500)</c> - where the check-out selfie is stored. Null while open.</summary>
    public string? CheckOutSelfiePath { get; set; }

    /// <summary><c>nvarchar(500)</c>, as sent by the device.</summary>
    public string? CheckOutLatitude { get; set; }

    /// <summary><c>nvarchar(500)</c>, as sent by the device.</summary>
    public string? CheckOutLongitude { get; set; }

    /// <summary>The session's length in whole minutes, written at check-out. Null while open.</summary>
    public int? LoggedMinutes { get; set; }

    /// <summary><c>datetime2(0)</c>, UTC. The column has no default, so the API stamps it.</summary>
    public DateTime CreateDate { get; set; }

    public int CreatedBy { get; set; }

    /// <summary><c>datetime2(0)</c>, UTC.</summary>
    public DateTime? UpdateDate { get; set; }

    public int? UpdatedBy { get; set; }
}
