/*
    Procedure: dbo.spc_AddEditUserAccessSetup
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-18 - DRAFT: new procedure.

    Adds a user's module assignment to dbo.Timesheet_role_assignment, or edits
    one, and returns the saved row. Called by
    POST api/v1/TimesheetProject/add-edit-user-access-setup.

    Parameters:
      @PAssignmentID        NULL or 0 adds a new assignment; any other value
                            edits that assignment.
      @PUserID              the user the assignment is for.
      @PIsClientSetup ..
      @PIsTimeLog           1 grants that module, 0 refuses it - one per
                            Mod_ column.
      @PLoginUserID         written to CreatedBy on add, Updatedby on edit.
      @PDate                written to CreateDate on add, UpdatedDate on edit
                            - the API's clock (UTC), passed in so the
                            procedure and the API agree on "now".

    Add (@PAssignmentID NULL or 0):
      Inserts a row. AssignmentID is IDENTITY(1,1), so the database assigns
      it; the new id is read back with SCOPE_IDENTITY().

    Edit (@PAssignmentID > 0):
      Updates UserID and the eight module flags. CreateDate / CreatedBy are
      left as they were.

    One assignment per user:
      A user may have only one assignment. The API checks this before calling
      (409 Conflict), and the procedure checks it again so that a caller other
      than the API cannot break it: a second assignment for the same user is
      refused with error 50001, and nothing is written. No unique constraint
      enforces it in the table.

    Result:
      The saved assignment - the same columns as
      dbo.spc_GetUserAccessSetup. NO ROW when an edit names an
      assignment that does not exist; the API answers 404 for that.

    Validation lives in the API (TimesheetProjectService): a positive
    @PUserID and @PLoginUserID, and a user who is a live user of the
    caller's organisation. The procedure does not repeat it.

    The table is recorded at:
      ../schema/dbo.Timesheet_role_assignment.sql
    NOT RECORDED - dbo.Signup; the test container builds it from the EF
    mapping (SignupConfiguration).
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditUserAccessSetup]
    @PAssignmentID        INT  = NULL,
    @PUserID              INT,
    @PIsClientSetup       BIT,
    @PIsProject           BIT,
    @PIsProjectBudget     BIT,
    @PIsProjectCategory   BIT,
    @PIsStatusSetup       BIT,
    @PIsRoleSetup         BIT,
    @PIsUserSetup         BIT,
    @PIsTimeLog           BIT,
    @PLoginUserID         INT,
    @PDate                DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @AssignmentID INT = ISNULL(@PAssignmentID, 0);

    BEGIN TRANSACTION;

    -- UPDLOCK + HOLDLOCK so two saves for the same user cannot both pass the
    -- check before either has written.
    IF EXISTS
    (
        SELECT 1
        FROM dbo.Timesheet_role_assignment WITH (UPDLOCK, HOLDLOCK)
        WHERE UserID = @PUserID
          AND AssignmentID <> @AssignmentID
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, 'The user already has a user access setup.', 1;
    END

    IF @AssignmentID = 0
    BEGIN
        INSERT INTO dbo.Timesheet_role_assignment
            (UserID,
             Mod_IsclientSetup, Mod_Isproject, Mod_IsProjectbudget, Mod_IsProjectCategory,
             Mod_IsStatusSetup, Mod_IsRoleSetup, Mod_IsUsersetup, Mod_IsTimeLog,
             CreateDate, CreatedBy)
        VALUES
            (@PUserID,
             @PIsClientSetup, @PIsProject, @PIsProjectBudget, @PIsProjectCategory,
             @PIsStatusSetup, @PIsRoleSetup, @PIsUserSetup, @PIsTimeLog,
             @PDate, @PLoginUserID);

        SET @AssignmentID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.Timesheet_role_assignment
        SET UserID                = @PUserID,
            Mod_IsclientSetup     = @PIsClientSetup,
            Mod_Isproject         = @PIsProject,
            Mod_IsProjectbudget   = @PIsProjectBudget,
            Mod_IsProjectCategory = @PIsProjectCategory,
            Mod_IsStatusSetup     = @PIsStatusSetup,
            Mod_IsRoleSetup       = @PIsRoleSetup,
            Mod_IsUsersetup       = @PIsUserSetup,
            Mod_IsTimeLog         = @PIsTimeLog,
            UpdatedDate           = @PDate,
            Updatedby             = @PLoginUserID
        WHERE AssignmentID = @AssignmentID;
    END

    COMMIT TRANSACTION;

    -- =================================================================
    -- The saved row - same columns as spc_GetUserAccessSetup
    -- =================================================================
    SELECT
        tra.AssignmentID            AS AssignmentID,
        tra.UserID                  AS UserID,
        s.Name                      AS UserName,
        s.Email                     AS Email,
        s.OrganizationID            AS OrganizationID,
        tra.Mod_IsclientSetup       AS IsClientSetup,
        tra.Mod_Isproject           AS IsProject,
        tra.Mod_IsProjectbudget     AS IsProjectBudget,
        tra.Mod_IsProjectCategory   AS IsProjectCategory,
        tra.Mod_IsStatusSetup       AS IsStatusSetup,
        tra.Mod_IsRoleSetup         AS IsRoleSetup,
        tra.Mod_IsUsersetup         AS IsUserSetup,
        tra.Mod_IsTimeLog           AS IsTimeLog,
        tra.CreateDate              AS CreateDate,
        tra.CreatedBy               AS CreatedBy,
        tra.UpdatedDate             AS UpdatedDate,
        tra.Updatedby               AS UpdatedBy
    FROM dbo.Timesheet_role_assignment tra
    LEFT JOIN dbo.Signup s ON s.UserID = tra.UserID
    WHERE tra.AssignmentID = @AssignmentID;
END
GO
