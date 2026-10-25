/*
    Procedure: dbo.spc_GetTimesheetLeaveSetup
    Recorded:  2026-10-25 - DRAFT: new procedure.
    Revised:   2026-10-25 - body below is the version supplied by the
               database owner: CreateDate, CreatedBy, UpdatedDate and
               UpdatedBy removed from the result.

    Returns the live leave types of one organisation from
    dbo.Timesheet_Leavetype_Setup, for
    GET api/v1/TimesheetProject/get-timesheet-leave-setup?orgID=.

    Parameters:
      @POrgID   the organisation (dbo.Timesheet_Leavetype_Setup.OrgID).
                Required - the API refuses a missing or non-positive id with
                a 400 before the procedure is called, so the whole table is
                never returned.

    Live rows only:
      ISNULL(IsDelete, 0) = 0 - the add writes 0, the API's soft delete writes
      1, and a NULL is treated as live.

    Result columns:
      LeaveTypeID, OrganizationID (aliased from OrgID), LeaveType,
      NoOfLeaveAllocated (aliased from NoofLeaveAllocated). The aliases are
      what the EF Core result mapping binds to. The audit columns are not
      returned.

    Order:
      By LeaveType, then LeaveTypeID so the order is always the same.

    Result:
      No leave types is an empty result, not an error.

    The table is recorded at:
      ../schema/dbo.Timesheet_Leavetype_Setup.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetLeaveSetup]
    @POrgID   INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tls.LeaveTypeID             AS LeaveTypeID,
        tls.OrgID                   AS OrganizationID,
        tls.LeaveType               AS LeaveType,
        tls.NoofLeaveAllocated      AS NoOfLeaveAllocated
    FROM dbo.Timesheet_Leavetype_Setup tls
    WHERE ISNULL(tls.IsDelete, 0) = 0
      AND tls.OrgID = @POrgID
    ORDER BY tls.LeaveType, tls.LeaveTypeID;
END
GO
