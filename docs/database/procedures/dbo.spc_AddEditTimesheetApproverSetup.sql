/*
    Procedure: dbo.spc_AddEditTimesheetApproverSetup
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-20 - DRAFT: new procedure.

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
      @PApproverID                the approving user (dbo.Signup.UserID).
      @PIsTimesheetApprover       1 makes the user an approver of the
                                  project's timesheets, 0 does not.
      @PLoginUserID               written to CreatedBy on add.
      @PDate                      written to CreateDate on add - the API's
                                  clock (UTC), passed in so the procedure and
                                  the API agree on "now".

    Add (@PTimesheetApproverSetupID NULL or 0):
      Inserts a live row (IsDelete = 0). TimesheetApproverSetupID is
      IDENTITY(1,1), so the database assigns it; the new id is read back with
      SCOPE_IDENTITY().

    Edit (@PTimesheetApproverSetupID > 0):
      Updates ProjectID, ApproverID and IsTimesheetApprover only if the setup
      belongs to @POrgID and is live (IsDelete 0 or NULL). A deleted setup is
      not edited back to life. CreateDate / CreatedBy are left as they were.
      The table has no UpdatedDate / UpdatedBy columns, so an edit is not
      stamped; @PLoginUserID and @PDate are not written on edit.

    One approver per project:
      A project may have only one live approver setup - one approver reviews
      its timesheets. One user may still approve several projects. The API
      checks this before calling (409 Conflict), and the procedure checks it
      again so that a caller other than the API cannot break it: a second
      live setup for the same project is refused with error 50001, and
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
    @PProjectID                 INT,
    @PApproverID                INT,
    @PIsTimesheetApprover       BIT,
    @PLoginUserID               INT,
    @PDate                      DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @TimesheetApproverSetupID INT = ISNULL(@PTimesheetApproverSetupID, 0);

    BEGIN TRANSACTION;

    -- UPDLOCK + HOLDLOCK so two saves for the same project cannot both pass
    -- the check before either has written.
    IF EXISTS
    (
        SELECT 1
        FROM dbo.Timesheet_Approver_setup WITH (UPDLOCK, HOLDLOCK)
        WHERE ProjectID = @PProjectID
          AND ISNULL(IsDelete, 0) = 0
          AND TimesheetApproverSetupID <> @TimesheetApproverSetupID
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, 'The project already has an approver setup.', 1;
    END

    IF @TimesheetApproverSetupID = 0
    BEGIN
        INSERT INTO dbo.Timesheet_Approver_setup
            (ApproverID, ProjectID, OrgID, IsTimesheetApprover,
             CreateDate, CreatedBy, IsDelete)
        VALUES
            (@PApproverID, @PProjectID, @POrgID, @PIsTimesheetApprover,
             @PDate, @PLoginUserID, 0);

        SET @TimesheetApproverSetupID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.Timesheet_Approver_setup
        SET ProjectID           = @PProjectID,
            ApproverID          = @PApproverID,
            IsTimesheetApprover = @PIsTimesheetApprover
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
