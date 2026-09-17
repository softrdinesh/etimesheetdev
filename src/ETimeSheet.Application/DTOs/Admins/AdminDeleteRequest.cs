namespace ETimeSheet.Application.DTOs.Admins;

/// <summary>
/// Body of the timesheet setup delete request.
/// <para>
/// The delete is a <b>soft</b> delete: the row is never removed. It is marked
/// <c>IsDelete = 1</c> and stamped with who deleted it and when, so the history
/// survives and a global query filter simply stops returning it.
/// </para>
/// </summary>
public class AdminDeleteRequest
{
    /// <summary>The row to delete. Must identify a live, non-deleted row.</summary>
    public int SetupId { get; set; }

    /// <summary>
    /// The user performing the delete, written to the <c>Deletedby</c> column.
    /// <para>
    /// <b>Temporary</b>, exactly as <c>AdminSaveRequest.CreatedBy</c> is:
    /// it comes from the token once authentication is on.
    /// </para>
    /// </summary>
    public int DeletedBy { get; set; }
}
