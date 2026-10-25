/*
    Procedure: dbo.spc_GetTimesheetUserLeaveAssignment
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-25 - DRAFT: new procedure.

    Returns the live leave assignments in dbo.Timesheet_Leave_assignment_user,
    filtered by organisation, user or both, for
    GET api/v1/TimesheetProject/get-timesheet-user-leave-assignment?orgID=&userID=.

    Parameters:
      @POrgID   the organisation - taken from the USER
                (dbo.Signup.OrganizationID), because
                dbo.Timesheet_Leave_assignment_user has no organisation
                column. NULL or 0 means "any organisation".
      @PUserID  the user (dbo.Timesheet_Leave_assignment_user.UserID). NULL or
                0 means "any user".

    Filter:
      - Only @POrgID given   -> every assignment of a live user of that
                                organisation.
      - Only @PUserID given  -> that user's assignments.
      - Both given           -> that user's assignments, if the user is in
                                that organisation.
      - Neither given        -> the API refuses this with a 400 before the
                                procedure is called, so the whole table is
                                never returned.
      OPTION (RECOMPILE) so each combination gets its own plan, rather than
      one plan cached for whichever combination ran first.

    Live rows only:
      ISNULL(IsDelete, 0) = 0 on the assignment - the add writes 0, the API's
      soft delete writes 1, and a NULL is treated as live - and a live user
      (dbo.Signup.IsDelete = 0), joined with an INNER JOIN.

    Result columns:
      TimesheetLeaveAssignmentUserID, UserID, UserName (from dbo.Signup.Name),
      Email (from dbo.Signup.Email), OrganizationID (from dbo.Signup),
      LeaveTypeID (aliased from LeavetypeID), LeaveType and
      NoOfLeaveAllocated (from dbo.Timesheet_Leavetype_Setup), BalanceLeave.
      The aliases are what the EF Core result mapping binds to. The audit
      columns are not returned, as in dbo.spc_GetTimesheetLeaveSetup.

      The leave type comes from a LEFT JOIN, so an assignment whose leave
      type is unknown or has been removed is still returned, with NULL
      LeaveType and NoOfLeaveAllocated.

    Order:
      By user name, then leave type, then TimesheetLeaveAssignmentUserID so
      the order is always the same.

    Result:
      No matching assignments is an empty result, not an error.

    The tables are recorded at:
      ../schema/dbo.Timesheet_Leave_assignment_user.sql
      ../schema/dbo.Timesheet_Leavetype_Setup.sql
    NOT RECORDED - dbo.Signup; the test container builds it from the EF
    mapping (SignupConfiguration).
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetUserLeaveAssignment]
    @POrgID   INT = NULL,
    @PUserID  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tla.TimesheetLeaveAssignmentUserID  AS TimesheetLeaveAssignmentUserID,
        tla.UserID                          AS UserID,
        s.Name                              AS UserName,
        s.Email                             AS Email,
        s.OrganizationID                    AS OrganizationID,
        tla.LeavetypeID                     AS LeaveTypeID,
        tls.LeaveType                       AS LeaveType,
        tls.NoofLeaveAllocated              AS NoOfLeaveAllocated,
        tla.BalanceLeave                    AS BalanceLeave
    FROM dbo.Timesheet_Leave_assignment_user tla
    INNER JOIN dbo.Signup s ON s.UserID = tla.UserID
    LEFT JOIN dbo.Timesheet_Leavetype_Setup tls ON tls.LeaveTypeID = tla.LeavetypeID
    WHERE ISNULL(tla.IsDelete, 0) = 0
      AND s.IsDelete = 0
      AND (ISNULL(@POrgID, 0) = 0 OR s.OrganizationID = @POrgID)
      AND (ISNULL(@PUserID, 0) = 0 OR tla.UserID = @PUserID)
    ORDER BY s.Name, tls.LeaveType, tla.TimesheetLeaveAssignmentUserID
    OPTION (RECOMPILE);
END
GO
