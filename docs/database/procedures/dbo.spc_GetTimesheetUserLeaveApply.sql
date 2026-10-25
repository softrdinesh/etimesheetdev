/*
    Procedure: dbo.spc_GetTimesheetUserLeaveApply
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-25 - DRAFT: new procedure.
    Revised:   2026-10-25 - DRAFT: filters and returns the table's own OrgID,
               added by the owner, instead of the user's organisation.

    Returns the leave applications in dbo.Timesheet_Leave_User_apply,
    filtered by organisation, user or both, for
    GET api/v1/TimesheetProject/get-timesheet-user-leave-apply?orgID=&userID=.

    Parameters:
      @POrgID   the organisation (dbo.Timesheet_Leave_User_apply.OrgID).
                NULL or 0 means "any organisation".
      @PUserID  the user (dbo.Timesheet_Leave_User_apply.UserID). NULL or 0
                means "any user".

    Filter:
      - Only @POrgID given   -> every application of that organisation, of
                                a live user.
      - Only @PUserID given  -> that user's applications.
      - Both given           -> that user's applications, if the user is in
                                that organisation.
      - Neither given        -> the API refuses this with a 400 before the
                                procedure is called, so the whole table is
                                never returned.
      OPTION (RECOMPILE) so each combination gets its own plan, rather than
      one plan cached for whichever combination ran first.

      The table has no IsDelete: a removed application is physically
      deleted, so every row is live. Only live users
      (dbo.Signup.IsDelete = 0) are included, through an INNER JOIN.

    Status:
      LeaveStatus holds a dbo.TimesheetLeaveStatus_Lookup.Value, and
      LeaveStatusName is that row's Text - the status vocabulary is the
      lookup's, never hard-coded here.

    Result columns:
      TimesheetLeaveUserApplyID, LeaveID, UserID, UserName (from
      dbo.Signup.Name), Email (from dbo.Signup.Email), OrganizationID
      (aliased from OrgID), LeaveTypeID (aliased from LeavetypeID), LeaveType (from
      dbo.Timesheet_Leavetype_Setup), StartDate, EndDate, NoOfDays (aliased
      from NoofDays), UserComments, ApproverComments, LeaveStatus,
      LeaveStatusName (from dbo.TimesheetLeaveStatus_Lookup.Text),
      ApprovedDate, ApprovedBy, RejectedDate, RejectedBy. The aliases are what
      the EF Core result mapping binds to. CreatedDate / CreatedBy are not
      returned.

      The leave type and status come from LEFT JOINs, so an application with
      an unknown one is still returned, with a NULL name.

    Order:
      Newest leave first: StartDate descending, then
      TimesheetLeaveUserApplyID descending so the order is always the same.

    Result:
      No matching applications is an empty result, not an error.

    The tables are recorded at:
      ../schema/dbo.Timesheet_Leave_User_apply.sql
      ../schema/dbo.Timesheet_Leavetype_Setup.sql
      ../schema/dbo.TimesheetLeaveStatus_Lookup.sql
    NOT RECORDED - dbo.Signup; the test container builds it from the EF
    mapping (SignupConfiguration).
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetUserLeaveApply]
    @POrgID   INT = NULL,
    @PUserID  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tla.TimesheetLeaveUserApplyID   AS TimesheetLeaveUserApplyID,
        tla.LeaveID                     AS LeaveID,
        tla.UserID                      AS UserID,
        s.Name                          AS UserName,
        s.Email                         AS Email,
        tla.OrgID                       AS OrganizationID,
        tla.LeavetypeID                 AS LeaveTypeID,
        tls.LeaveType                   AS LeaveType,
        tla.StartDate                   AS StartDate,
        tla.EndDate                     AS EndDate,
        tla.NoofDays                    AS NoOfDays,
        tla.UserComments                AS UserComments,
        tla.ApproverComments            AS ApproverComments,
        tla.LeaveStatus                 AS LeaveStatus,
        lsl.Text                        AS LeaveStatusName,
        tla.ApprovedDate                AS ApprovedDate,
        tla.ApprovedBy                  AS ApprovedBy,
        tla.RejectedDate                AS RejectedDate,
        tla.RejectedBy                  AS RejectedBy
    FROM dbo.Timesheet_Leave_User_apply tla
    INNER JOIN dbo.Signup s ON s.UserID = tla.UserID
    LEFT JOIN dbo.Timesheet_Leavetype_Setup tls ON tls.LeaveTypeID = tla.LeavetypeID
    LEFT JOIN dbo.TimesheetLeaveStatus_Lookup lsl ON lsl.Value = tla.LeaveStatus
    WHERE s.IsDelete = 0
      AND (ISNULL(@POrgID, 0) = 0 OR tla.OrgID = @POrgID)
      AND (ISNULL(@PUserID, 0) = 0 OR tla.UserID = @PUserID)
    ORDER BY tla.StartDate DESC, tla.TimesheetLeaveUserApplyID DESC
    OPTION (RECOMPILE);
END
GO
