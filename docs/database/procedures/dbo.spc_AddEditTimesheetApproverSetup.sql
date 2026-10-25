/*
    Procedure: dbo.spc_AddEditTimesheetApproverSetup
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-20 - DRAFT: new procedure.
    Revised:   2026-10-25 - DRAFT: @PIsLeaveApprover added (written on add and
               edit, returned as IsLeaveApprover). One setup per project
               replaced by one approver per area per project: errors 50001
               (timesheet approver taken) and 50002 (leave approver taken).
    Revised:   2026-10-26 - DRAFT: @PProjectID may be NULL for a leave-only
               setup, and the leave approver is now one per ORGANISATION,
               whatever the project (error 50002).

    Adds an approver setup to dbo.Timesheet_Approver_setup, or edits one, and
    returns the saved row. Called by
    POST api/v1/TimesheetProject/add-edit-timesheet-approver-setup.

    Parameters:
      @PTimesheetApproverSetupID  NULL or 0 adds a new setup; any other value
                                  edits that setup.
      @POrgID                     the organisation the setup belongs to. On
                                  edit, the setup must belong to THIS
                                  organisation - an id from another
                                  organisation is treated as not found.
      @PProjectID                 the project the approver is set up for.
                                  May be NULL for a leave-only setup - leave
                                  is approved for the whole organisation.
                                  The API refuses NULL for a timesheet
                                  approver.
      @PApproverID                the approving user (dbo.Signup.UserID).
      @PIsTimesheetApprover       1 makes the user the approver of the
                                  project's timesheets, 0 does not.
      @PIsLeaveApprover           1 makes the user the organisation's leave
                                  approver, 0 does not. The API
                                  refuses a save with both flags 0.
      @PLoginUserID               written to CreatedBy on add.
      @PDate                      written to CreateDate on add - the API's
                                  clock (UTC), passed in so the procedure and
                                  the API agree on "now".

    Add (@PTimesheetApproverSetupID NULL or 0):
      Inserts a live row (IsDelete = 0). TimesheetApproverSetupID is
      IDENTITY(1,1), so the database assigns it; the new id is read back with
      SCOPE_IDENTITY().

    Edit (@PTimesheetApproverSetupID > 0):
      Updates ProjectID, ApproverID, IsTimesheetApprover and IsLeaveApprover
      only if the setup
      belongs to @POrgID and is live (IsDelete 0 or NULL). A deleted setup is
      not edited back to life. CreateDate / CreatedBy are left as they were.
      The table has no UpdatedDate / UpdatedBy columns, so an edit is not
      stamped; @PLoginUserID and @PDate are not written on edit.

    One timesheet approver per project, one leave approver per organisation:
      A project may have one live timesheet approver (IsTimesheetApprover = 1),
      and an organisation one live leave approver (IsLeaveApprover = 1),
      whatever project its setup names - the same user or two different
      ones. Only the areas this save claims are checked, so a free area can
      be taken even when the other is held. The API checks this before
      calling (409 Conflict), and the procedure checks it again so that a
      caller other than the API cannot break it: claiming a held timesheet
      area is refused with error 50001, a held leave area with 50002, and
      nothing is written. No unique constraint enforces it in the table.

    Result:
      The saved setup - the same columns as dbo.spc_GetTimesheetApproverSetup.
      NO ROW when an edit names a setup that does not exist, is deleted, or
      belongs to another organisation; the API answers 404 for that.

    Validation lives in the API (TimesheetProjectService): a positive
    @POrgID, @PProjectID, @PApproverID and @PLoginUserID, a project of
    @POrgID, and an approver who is a live user of @POrgID. The procedure
    does not repeat it.

    The tables are recorded at:
      ../schema/dbo.Timesheet_Approver_setup.sql
      ../schema/dbo.TimesheetProject.sql
    NOT RECORDED - dbo.Signup; the test container builds it from the EF
    mapping (SignupConfiguration).
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditTimesheetApproverSetup]
    @PTimesheetApproverSetupID  INT  = NULL,
    @POrgID                     INT,
    @PProjectID                 INT  = NULL,
    @PApproverID                INT,
    @PIsTimesheetApprover       BIT,
    @PIsLeaveApprover           BIT,
    @PLoginUserID               INT,
    @PDate                      DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @TimesheetApproverSetupID INT = ISNULL(@PTimesheetApproverSetupID, 0);

    BEGIN TRANSACTION;

    -- UPDLOCK + HOLDLOCK so two saves for the same project cannot both pass
    -- the checks before either has written.
    IF ISNULL(@PIsTimesheetApprover, 0) = 1
       AND EXISTS
       (
           SELECT 1
           FROM dbo.Timesheet_Approver_setup WITH (UPDLOCK, HOLDLOCK)
           WHERE ProjectID = @PProjectID
             AND ISNULL(IsDelete, 0) = 0
             AND IsTimesheetApprover = 1
             AND TimesheetApproverSetupID <> @TimesheetApproverSetupID
       )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, 'The project already has a timesheet approver.', 1;
    END

    IF ISNULL(@PIsLeaveApprover, 0) = 1
       AND EXISTS
       (
           SELECT 1
           FROM dbo.Timesheet_Approver_setup WITH (UPDLOCK, HOLDLOCK)
           WHERE OrgID = @POrgID
             AND ISNULL(IsDelete, 0) = 0
             AND IsLeaveApprover = 1
             AND TimesheetApproverSetupID <> @TimesheetApproverSetupID
       )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50002, 'The organisation already has a leave approver.', 1;
    END

    IF @TimesheetApproverSetupID = 0
    BEGIN
        INSERT INTO dbo.Timesheet_Approver_setup
            (ApproverID, ProjectID, OrgID, IsTimesheetApprover, IsLeaveApprover,
             CreateDate, CreatedBy, IsDelete)
        VALUES
            (@PApproverID, @PProjectID, @POrgID, @PIsTimesheetApprover, @PIsLeaveApprover,
             @PDate, @PLoginUserID, 0);

        SET @TimesheetApproverSetupID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.Timesheet_Approver_setup
        SET ProjectID           = @PProjectID,
            ApproverID          = @PApproverID,
            IsTimesheetApprover = @PIsTimesheetApprover,
            IsLeaveApprover     = @PIsLeaveApprover
        WHERE TimesheetApproverSetupID = @TimesheetApproverSetupID
          AND OrgID = @POrgID
          AND ISNULL(IsDelete, 0) = 0;
    END

    COMMIT TRANSACTION;

    -- =================================================================
    -- The saved row - same columns as spc_GetTimesheetApproverSetup
    -- =================================================================
    SELECT
        tas.TimesheetApproverSetupID AS TimesheetApproverSetupID,
        tas.OrgID                    AS OrganizationID,
        tas.ProjectID                AS ProjectID,
        tp.Code                      AS ProjectCode,
        tp.Projectname               AS ProjectName,
        tas.ApproverID               AS ApproverID,
        s.Name                       AS ApproverName,
        s.Email                      AS ApproverEmail,
        tas.IsTimesheetApprover      AS IsTimesheetApprover,
        tas.IsLeaveApprover          AS IsLeaveApprover,
        tas.CreateDate               AS CreateDate,
        tas.CreatedBy                AS CreatedBy
    FROM dbo.Timesheet_Approver_setup tas
    LEFT JOIN dbo.TimesheetProject tp ON tp.ProjectID = tas.ProjectID
    LEFT JOIN dbo.Signup s ON s.UserID = tas.ApproverID
    WHERE tas.TimesheetApproverSetupID = @TimesheetApproverSetupID
      AND tas.OrgID = @POrgID
      AND ISNULL(tas.IsDelete, 0) = 0;
END
GO
