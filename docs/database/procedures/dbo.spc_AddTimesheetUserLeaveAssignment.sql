/*
    Procedure: dbo.spc_AddTimesheetUserLeaveAssignment
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-26 - DRAFT: new procedure - the add half of the former
               draft dbo.spc_AddEditTimesheetUserLeaveAssignment, split out
               with dbo.spc_EditTimesheetUserLeaveAssignment.

    Assigns a leave type to a user in dbo.Timesheet_Leave_assignment_user and
    returns the new row. Called by
    POST api/v1/TimesheetProject/add-timesheet-user-leave-assignment.

    Parameters:
      @POrgID         the organisation of the user. The table has no
                      organisation column; the row is read back only if the
                      user belongs to it (from dbo.Signup).
      @PUserID        the user the leave type is assigned to.
      @PLeaveTypeID   the leave type (dbo.Timesheet_Leavetype_Setup.LeaveTypeID).
      @PLoginUserID   written to CreatedBy.
      @PDate          written to CreatedDate - the API's clock (UTC), passed
                      in so the procedure and the API agree on "now".

    The balance:
      Not a parameter. BalanceLeave starts at the leave type's
      NoofLeaveAllocated.

    Add:
      Inserts a live row (IsDelete = 0). TimesheetLeaveAssignmentUserID is
      IDENTITY(1,1), so the database assigns it; the new id is read back with
      SCOPE_IDENTITY().

    One assignment per user and leave type:
      A user may hold each leave type only once. The API checks this before
      calling (409 Conflict), and the procedure checks it again so that a
      caller other than the API cannot break it: a second live assignment of
      the same leave type to the same user is refused with error 50001, and
      nothing is written. No unique constraint enforces it in the table.

    Result:
      The new assignment - the same columns as
      dbo.spc_GetTimesheetUserLeaveAssignment.

    Validation lives in the API (TimesheetProjectService): a positive
    @POrgID, @PUserID, @PLeaveTypeID and @PLoginUserID, a user who is a live
    user of @POrgID, and a live leave type of @POrgID. The procedure does not
    repeat it.

    The tables are recorded at:
      ../schema/dbo.Timesheet_Leave_assignment_user.sql
      ../schema/dbo.Timesheet_Leavetype_Setup.sql
    NOT RECORDED - dbo.Signup; the test container builds it from the EF
    mapping (SignupConfiguration).
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddTimesheetUserLeaveAssignment]
    @POrgID         INT,
    @PUserID        INT,
    @PLeaveTypeID   INT,
    @PLoginUserID   INT,
    @PDate          DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @AssignmentID INT;

    BEGIN TRANSACTION;

    -- UPDLOCK + HOLDLOCK so two adds for the same user and leave type cannot
    -- both pass the check before either has written.
    IF EXISTS
    (
        SELECT 1
        FROM dbo.Timesheet_Leave_assignment_user WITH (UPDLOCK, HOLDLOCK)
        WHERE UserID = @PUserID
          AND LeavetypeID = @PLeaveTypeID
          AND ISNULL(IsDelete, 0) = 0
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, 'The user already has this leave type assigned.', 1;
    END

    INSERT INTO dbo.Timesheet_Leave_assignment_user
        (UserID, LeavetypeID, BalanceLeave, IsDelete, CreatedDate, CreatedBy)
    VALUES
        (@PUserID,
         @PLeaveTypeID,
         (SELECT tls.NoofLeaveAllocated
          FROM dbo.Timesheet_Leavetype_Setup tls
          WHERE tls.LeaveTypeID = @PLeaveTypeID),
         0,
         @PDate,
         @PLoginUserID);

    SET @AssignmentID = CAST(SCOPE_IDENTITY() AS INT);

    COMMIT TRANSACTION;

    -- =================================================================
    -- The new row - same columns as spc_GetTimesheetUserLeaveAssignment
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
    WHERE tla.TimesheetLeaveAssignmentUserID = @AssignmentID
      AND s.OrganizationID = @POrgID;
END
GO
