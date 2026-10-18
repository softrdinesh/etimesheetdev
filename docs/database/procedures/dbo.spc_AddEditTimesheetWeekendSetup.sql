/*
    Procedure: dbo.spc_AddEditTimesheetWeekendSetup
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-18 - DRAFT: new procedure.

    Adds a weekend setup to dbo.TimeSheet_Weekend_setup, or edits one, and
    returns the saved row. Called by
    POST api/v1/TimesheetProject/add-edit-timesheet-weekend-setup.

    Parameters:
      @PTimeSheetWeekendSetupID  NULL or 0 adds a new setup; any other value
                                 edits that setup.
      @POrgID                    the organisation the setup belongs to. On
                                 edit, the setup must belong to THIS
                                 organisation - an id from another
                                 organisation is treated as not found.
      @PClientID                 optional; NULL clears it on edit.
      @PProjectID                the project the setup is for.
      @PIsMondayWeekend ..
      @PIsSundayWeekend          1 when that day is a weekend day, 0 when not.
      @PUserID                   written to CreatedBy on add, UpdatedBy on
                                 edit.
      @PDate                     written to CreateDate on add, UpdatedDate on
                                 edit - the API's clock (UTC), passed in so the
                                 procedure and the API agree on "now".

    Add (@PTimeSheetWeekendSetupID NULL or 0):
      Inserts a live row (IsDelete = 0). TimeSheetWeekendSetupID is
      IDENTITY(1,1), so the database assigns it; the new id is read back with
      SCOPE_IDENTITY().

    Edit (@PTimeSheetWeekendSetupID > 0):
      Updates ClientID, ProjectID and the seven day flags only if the setup
      belongs to @POrgID and is live (IsDelete 0 or NULL). A deleted setup is
      not edited back to life. CreateDate / CreatedBy are left as they were.

    One setup per project:
      A project may have only one live setup. The API checks this before
      calling (409 Conflict), and the procedure checks it again so that a
      caller other than the API cannot break it: a second live setup for the
      same project is refused with error 50001, and nothing is written. No
      unique constraint enforces it in the table.

    Result:
      The saved setup - the same columns as dbo.spc_GetTimesheetWeekendSetup.
      NO ROW when an edit names a setup that does not exist, is deleted, or
      belongs to another organisation; the API answers 404 for that.

    Validation lives in the API (TimesheetProjectService): a positive @POrgID,
    @PProjectID and @PUserID, and a client and project that exist in @POrgID.
    The procedure does not repeat it.

    The tables are recorded at:
      ../schema/dbo.TimeSheet_Weekend_setup.sql
      ../schema/dbo.Timesheetclientsetup.sql
      ../schema/dbo.TimesheetProject.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditTimesheetWeekendSetup]
    @PTimeSheetWeekendSetupID  INT  = NULL,
    @POrgID                    INT,
    @PClientID                 INT  = NULL,
    @PProjectID                INT,
    @PIsMondayWeekend          BIT,
    @PIsTuesdayWeekend         BIT,
    @PIsWednesdayWeekend       BIT,
    @PIsThursdayWeekend        BIT,
    @PIsFridayWeekend          BIT,
    @PIsSaturdayWeekend        BIT,
    @PIsSundayWeekend          BIT,
    @PUserID                   INT,
    @PDate                     DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @TimeSheetWeekendSetupID INT = ISNULL(@PTimeSheetWeekendSetupID, 0);

    BEGIN TRANSACTION;

    -- UPDLOCK + HOLDLOCK so two saves for the same project cannot both pass
    -- the check before either has written.
    IF EXISTS
    (
        SELECT 1
        FROM dbo.TimeSheet_Weekend_setup WITH (UPDLOCK, HOLDLOCK)
        WHERE ProjectID = @PProjectID
          AND ISNULL(IsDelete, 0) = 0
          AND TimeSheetWeekendSetupID <> @TimeSheetWeekendSetupID
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50001, 'The project already has a weekend setup.', 1;
    END

    IF @TimeSheetWeekendSetupID = 0
    BEGIN
        INSERT INTO dbo.TimeSheet_Weekend_setup
            (OrgID, ClientID, ProjectID,
             IsMondayWeekend, IsTuesdayWeekend, IsWednesdayWeekend, IsThursdayWeekend,
             IsFridayWeekend, IsSaturdayWeekend, IsSundayWeekend,
             CreateDate, CreatedBy, IsDelete)
        VALUES
            (@POrgID, @PClientID, @PProjectID,
             @PIsMondayWeekend, @PIsTuesdayWeekend, @PIsWednesdayWeekend, @PIsThursdayWeekend,
             @PIsFridayWeekend, @PIsSaturdayWeekend, @PIsSundayWeekend,
             @PDate, @PUserID, 0);

        SET @TimeSheetWeekendSetupID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.TimeSheet_Weekend_setup
        SET ClientID           = @PClientID,
            ProjectID          = @PProjectID,
            IsMondayWeekend    = @PIsMondayWeekend,
            IsTuesdayWeekend   = @PIsTuesdayWeekend,
            IsWednesdayWeekend = @PIsWednesdayWeekend,
            IsThursdayWeekend  = @PIsThursdayWeekend,
            IsFridayWeekend    = @PIsFridayWeekend,
            IsSaturdayWeekend  = @PIsSaturdayWeekend,
            IsSundayWeekend    = @PIsSundayWeekend,
            UpdatedDate        = @PDate,
            UpdatedBy          = @PUserID
        WHERE TimeSheetWeekendSetupID = @TimeSheetWeekendSetupID
          AND OrgID = @POrgID
          AND ISNULL(IsDelete, 0) = 0;
    END

    COMMIT TRANSACTION;

    -- =================================================================
    -- The saved row - same columns as spc_GetTimesheetWeekendSetup
    -- =================================================================
    SELECT
        tws.TimeSheetWeekendSetupID AS TimeSheetWeekendSetupID,
        tws.OrgID                   AS OrganizationID,
        tws.ClientID                AS ClientID,
        tcs.Clientname              AS ClientName,
        tws.ProjectID               AS ProjectID,
        tp.Code                     AS ProjectCode,
        tp.Projectname              AS ProjectName,
        tws.IsMondayWeekend         AS IsMondayWeekend,
        tws.IsTuesdayWeekend        AS IsTuesdayWeekend,
        tws.IsWednesdayWeekend      AS IsWednesdayWeekend,
        tws.IsThursdayWeekend       AS IsThursdayWeekend,
        tws.IsFridayWeekend         AS IsFridayWeekend,
        tws.IsSaturdayWeekend       AS IsSaturdayWeekend,
        tws.IsSundayWeekend         AS IsSundayWeekend,
        tws.CreateDate              AS CreateDate,
        tws.CreatedBy               AS CreatedBy,
        tws.UpdatedDate             AS UpdatedDate,
        tws.UpdatedBy               AS UpdatedBy
    FROM dbo.TimeSheet_Weekend_setup tws
    LEFT JOIN dbo.TimesheetClientSetup tcs ON tcs.ClientID = tws.ClientID
    LEFT JOIN dbo.TimesheetProject tp ON tp.ProjectID = tws.ProjectID
    WHERE tws.TimeSheetWeekendSetupID = @TimeSheetWeekendSetupID
      AND tws.OrgID = @POrgID
      AND ISNULL(tws.IsDelete, 0) = 0;
END
GO
