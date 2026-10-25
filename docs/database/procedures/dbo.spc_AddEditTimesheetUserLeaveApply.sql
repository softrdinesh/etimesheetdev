/*
    Procedure: dbo.spc_AddEditTimesheetUserLeaveApply
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-25 - DRAFT: new procedure.
    Revised:   2026-10-26 - DRAFT: header only - @PNoOfDays is now worked out
               by the API from the dates rather than sent by the caller. The
               parameters and the body are unchanged.
    Revised:   2026-10-25 - DRAFT: writes the table's own OrgID, added by the
               owner, on add, and matches on it on edit.

    Adds a leave application to dbo.Timesheet_Leave_User_apply, or edits
    one that is not approved, and returns the saved row. Called by
    POST api/v1/TimesheetProject/add-edit-timesheet-user-leave-apply.

    Parameters:
      @PTimesheetLeaveUserApplyID  NULL or 0 adds a new application; any
                                   other value edits that application.
      @POrgID                      the organisation, written to OrgID on
                                   add. On edit, the application must
                                   belong to THIS organisation - one of
                                   another organisation is treated as not
                                   found. Not changed by an edit.
      @PUserID                     the user applying. On edit it must be the
                                   application's own user - it is not moved
                                   to another user.
      @PLeaveTypeID                the leave type
                                   (dbo.Timesheet_Leavetype_Setup.LeaveTypeID).
      @PStartDate, @PEndDate       the first and last day of the leave.
      @PNoOfDays                   the days taken - worked out by the API
                                   from the dates, not sent by the caller:
                                   every calendar day from @PStartDate to
                                   @PEndDate, both counted.
      @PUserComments               the user's note; NULL clears it on edit.
      @PLoginUserID                written to CreatedBy on add. The table has
                                   no UpdatedDate / UpdatedBy, so an edit is
                                   not stamped.
      @PDate                       written to CreatedDate on add - the API's
                                   clock (UTC).

    Statuses:
      Read from dbo.TimesheetLeaveStatus_Lookup by Text - "Submitted" and
      "Approved" - so the ids belong to the lookup, not to this procedure.
      LeaveStatus stores the lookup's Value. A lookup without a "Submitted"
      row is refused with error 50002, and nothing is written.

    Add (@PTimesheetLeaveUserApplyID NULL or 0):
      Inserts the application as Submitted. TimesheetLeaveUserApplyID is
      IDENTITY(1,1), so the database assigns it; the new id is read back with
      SCOPE_IDENTITY(). LeaveID is NOT NULL and not an identity, so it is
      assigned here as the highest LeaveID in the table plus one - a running
      leave number - under UPDLOCK + HOLDLOCK so two adds racing cannot take
      the same number.

    Edit (@PTimesheetLeaveUserApplyID > 0):
      Updates LeavetypeID, StartDate, EndDate, NoofDays and UserComments only
      if the application belongs to @PUserID and to @POrgID, and
      the application is NOT approved. An approved application is never
      changed. An edit puts the application back to Submitted: a rejected
      one is resubmitted, and its RejectedDate / RejectedBy are cleared.
      ApproverComments are kept. LeaveID, CreatedDate and CreatedBy are left
      as they were.

    Result:
      The saved application - the same columns as
      dbo.spc_GetTimesheetUserLeaveApply. NO ROW when an edit names an
      application that does not exist, belongs to another user or
      organisation, or is approved; the API checks approval first and
      answers 400 for that, and 404 for the rest.

    Validation lives in the API (TimesheetProjectService): positive ids, a
    user who is a live user of @POrgID and holds a live assignment of the
    leave type, a live leave type of @POrgID, @PEndDate not before
    @PStartDate and at most 99 days from it. The procedure does not repeat it.

    The balance:
      Nothing here touches dbo.Timesheet_Leave_assignment_user.BalanceLeave.
      An application takes leave from the balance when it is approved, and
      the API's remove gives an approved application's days back.

    The tables are recorded at:
      ../schema/dbo.Timesheet_Leave_User_apply.sql
      ../schema/dbo.Timesheet_Leavetype_Setup.sql
      ../schema/dbo.TimesheetLeaveStatus_Lookup.sql
    NOT RECORDED - dbo.Signup; the test container builds it from the EF
    mapping (SignupConfiguration).
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditTimesheetUserLeaveApply]
    @PTimesheetLeaveUserApplyID  INT            = NULL,
    @POrgID                      INT,
    @PUserID                     INT,
    @PLeaveTypeID                INT,
    @PStartDate                  DATETIME,
    @PEndDate                    DATETIME,
    @PNoOfDays                   DECIMAL(4,2),
    @PUserComments               NVARCHAR(MAX)  = NULL,
    @PLoginUserID                INT,
    @PDate                       DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @ApplyID INT = ISNULL(@PTimesheetLeaveUserApplyID, 0);

    DECLARE @SubmittedStatus INT =
    (
        SELECT TOP (1) Value
        FROM dbo.TimesheetLeaveStatus_Lookup
        WHERE Text = N'Submitted'
        ORDER BY ID
    );

    DECLARE @ApprovedStatus INT =
    (
        SELECT TOP (1) Value
        FROM dbo.TimesheetLeaveStatus_Lookup
        WHERE Text = N'Approved'
        ORDER BY ID
    );

    IF @SubmittedStatus IS NULL
        THROW 50002, 'dbo.TimesheetLeaveStatus_Lookup has no Submitted row.', 1;

    BEGIN TRANSACTION;

    IF @ApplyID = 0
    BEGIN
        DECLARE @LeaveID INT =
        (
            SELECT ISNULL(MAX(LeaveID), 0) + 1
            FROM dbo.Timesheet_Leave_User_apply WITH (UPDLOCK, HOLDLOCK)
        );

        INSERT INTO dbo.Timesheet_Leave_User_apply
            (LeaveID, UserID, OrgID, LeavetypeID, StartDate, EndDate, NoofDays,
             UserComments, LeaveStatus, CreatedDate, CreatedBy)
        VALUES
            (@LeaveID, @PUserID, @POrgID, @PLeaveTypeID, @PStartDate, @PEndDate, @PNoOfDays,
             @PUserComments, @SubmittedStatus, @PDate, @PLoginUserID);

        SET @ApplyID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE tla
        SET LeavetypeID  = @PLeaveTypeID,
            StartDate    = @PStartDate,
            EndDate      = @PEndDate,
            NoofDays     = @PNoOfDays,
            UserComments = @PUserComments,
            LeaveStatus  = @SubmittedStatus,
            RejectedDate = NULL,
            RejectedBy   = NULL
        FROM dbo.Timesheet_Leave_User_apply tla
        WHERE tla.TimesheetLeaveUserApplyID = @ApplyID
          AND tla.UserID = @PUserID
          AND tla.OrgID = @POrgID
          AND (@ApprovedStatus IS NULL OR ISNULL(tla.LeaveStatus, 0) <> @ApprovedStatus);

        -- Nothing updated: not found, another user's, or approved. No row is
        -- returned, rather than an approved application read back as if saved.
        IF @@ROWCOUNT = 0
            SET @ApplyID = 0;
    END

    COMMIT TRANSACTION;

    -- =================================================================
    -- The saved row - same columns as spc_GetTimesheetUserLeaveApply
    -- =================================================================
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
    WHERE tla.TimesheetLeaveUserApplyID = @ApplyID
      AND tla.OrgID = @POrgID;
END
GO
