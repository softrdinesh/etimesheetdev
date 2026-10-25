/*
    Procedure: dbo.spc_EditTimesheetUserLeaveAssignment
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-26 - DRAFT: new procedure - the edit half of the former
               draft dbo.spc_AddEditTimesheetUserLeaveAssignment, split out
               with dbo.spc_AddTimesheetUserLeaveAssignment.

    Edits a leave assignment in dbo.Timesheet_Leave_assignment_user and
    returns the saved row. Called by
    POST api/v1/TimesheetProject/edit-timesheet-user-leave-assignment.

    Parameters:
      @PTimesheetLeaveAssignmentUserID  the assignment to edit.
      @POrgID                           the organisation. The table has no
                                        organisation column, so the
                                        assignment's CURRENT user must be a
                                        user of THIS organisation (from
                                        dbo.Signup) - an assignment of
                                        another organisation is treated as
                                        not found.
      @PUserID                          the user the leave type is assigned to.
      @PLeaveTypeID                     the leave type
                                        (dbo.Timesheet_Leavetype_Setup.LeaveTypeID).
      @PBalanceLeave                    the new balance, at most 99.99. NULL
                                        keeps the balance as it is - even when
                                        the leave type changes.
      @PLoginUserID                     written to UpdatedBy.
      @PDate                            written to UpdatedDate - the API's clock
                                        (UTC).

    Edit:
      Updates UserID, LeavetypeID and (unless @PBalanceLeave is NULL)
      BalanceLeave, and stamps UpdatedDate / UpdatedBy, only if the
      assignment is live and its user belongs to @POrgID. A deleted
      assignment is not edited back to life. CreatedDate / CreatedBy are left
      as they were.

    One assignment per user and leave type:
      Moving the assignment onto a user and leave type held by ANOTHER live
      assignment is refused with error 50001, and nothing is written. The API
      checks this first (409 Conflict); this is the check under a lock, for
      two saves racing.

    Result:
      The saved assignment - the same columns as
      dbo.spc_GetTimesheetUserLeaveAssignment. NO ROW when the assignment does
      not exist, is deleted, or belongs to another organisation; the API
      answers 404 for that.

    Validation lives in the API (TimesheetProjectService): positive ids, a
    user who is a live user of @POrgID, a live leave type of @POrgID, and a
    @PBalanceLeave (when sent) from 0 to 99.99 with at most two decimal
    places. The procedure does not repeat it.

    The tables are recorded at:
      ../schema/dbo.Timesheet_Leave_assignment_user.sql
      ../schema/dbo.Timesheet_Leavetype_Setup.sql
    NOT RECORDED - dbo.Signup; the test container builds it from the EF
    mapping (SignupConfiguration).
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_EditTimesheetUserLeaveAssignment]
    @PTimesheetLeaveAssignmentUserID  INT,
    @POrgID                           INT,
    @PUserID                          INT,
    @PLeaveTypeID                     INT,
    @PBalanceLeave                    DECIMAL(4,2)  = NULL,
    @PLoginUserID                     INT,
    @PDate                            DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- UPDLOCK + HOLDLOCK so two saves for the same user and leave type cannot
    -- both pass the check before either has written.
    IF EXISTS
    (
        SELECT 1
        FROM dbo.Timesheet_Leave_assignment_user WITH (UPDLOCK, HOLDLOCK)
        WHERE UserID = @PUserID
          AND LeavetypeID = @PLeaveTypeID
          AND ISNULL(IsDelete, 0) = 0
          AND TimesheetLeaveAssignmentUserID <> @PTimesheetLeaveAssignmentUserID
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, 'The user already has this leave type assigned.', 1;
    END

    UPDATE tla
    SET UserID       = @PUserID,
        LeavetypeID  = @PLeaveTypeID,
        BalanceLeave = ISNULL(@PBalanceLeave, tla.BalanceLeave),
        UpdatedDate  = @PDate,
        UpdatedBy    = @PLoginUserID
    FROM dbo.Timesheet_Leave_assignment_user tla
    INNER JOIN dbo.Signup s ON s.UserID = tla.UserID
    WHERE tla.TimesheetLeaveAssignmentUserID = @PTimesheetLeaveAssignmentUserID
      AND ISNULL(tla.IsDelete, 0) = 0
      AND s.OrganizationID = @POrgID;

    COMMIT TRANSACTION;

    -- =================================================================
    -- The saved row - same columns as spc_GetTimesheetUserLeaveAssignment
    -- =================================================================
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
    WHERE tla.TimesheetLeaveAssignmentUserID = @PTimesheetLeaveAssignmentUserID
      AND ISNULL(tla.IsDelete, 0) = 0
      AND s.OrganizationID = @POrgID;
END
GO
