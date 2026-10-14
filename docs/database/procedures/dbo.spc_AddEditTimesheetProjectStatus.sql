/*
    Procedure: dbo.spc_AddEditTimesheetProjectStatus
    Status:    DRAFT for review - not applied to any database.

    Adds a project status to an organisation in dbo.TimesheetProjectStatus,
    or edits one, and returns the saved row. Called by
    POST api/v1/TimesheetProject/add-edit-timesheet-project-status.

    Parameters:
      @PStatusID   NULL or 0 adds a new status; any other value edits that
                   status.
      @POrgID      the organisation the status belongs to. On edit, the status
                   must belong to THIS organisation - an id from another
                   organisation is treated as not found.
      @PStatus     the status's name.
      @PColorCode  the display colour; NULL clears it on edit.
      @PUserID     written to CreatedBy on add, Updatedby on edit.
      @PDate       written to CreateDate on add, UpdatedDate on edit - the
                   API's clock (UTC), passed in so the procedure and the API
                   agree on "now".

    Add (@PStatusID NULL or 0):
      Inserts a live row (DeletedDate NULL). StatusID is IDENTITY(1,1), so the
      database assigns it; the new id is read back with SCOPE_IDENTITY().

    Edit (@PStatusID > 0):
      Updates Status and Colorcode only if the status belongs to @POrgID and
      is live (DeletedDate IS NULL). A deleted status is not edited back to
      life. CreateDate / CreatedBy are left as they were.

    Result:
      The saved status - StatusID, Status, ColorCode - the same columns as
      dbo.spc_GetTimesheetProjectStatus. NO ROW when an edit names a status
      that does not exist, is deleted, or belongs to another organisation; the
      API answers 404 for that.

    Validation lives in the API (TimesheetProjectService): a positive @POrgID
    and @PUserID, a non-blank @PStatus of at most 20 characters, and a
    @PColorCode of at most 25. The procedure does not repeat it.

    The table is recorded at:
      ../schema/dbo.TimesheetProjectStatus.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditTimesheetProjectStatus]
    @PStatusID   INT           = NULL,
    @POrgID      INT,
    @PStatus     NVARCHAR(20),
    @PColorCode  NVARCHAR(25)  = NULL,
    @PUserID     INT,
    @PDate       DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @StatusID INT;

    IF ISNULL(@PStatusID, 0) = 0
    BEGIN
        INSERT INTO dbo.TimesheetProjectStatus
            (Status, Colorcode, OrgID, CreateDate, CreatedBy)
        VALUES
            (@PStatus, @PColorCode, @POrgID, @PDate, @PUserID);

        SET @StatusID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.TimesheetProjectStatus
        SET Status      = @PStatus,
            Colorcode   = @PColorCode,
            UpdatedDate = @PDate,
            Updatedby   = @PUserID
        WHERE StatusID = @PStatusID
          AND OrgID = @POrgID
          AND DeletedDate IS NULL;

        SET @StatusID = @PStatusID;
    END

    -- =================================================================
    -- The saved row - same columns as spc_GetTimesheetProjectStatus
    -- =================================================================
    SELECT
        tps.StatusID     AS StatusID,
        tps.Status       AS Status,
        tps.Colorcode    AS ColorCode
    FROM dbo.TimesheetProjectStatus tps
    WHERE tps.StatusID = @StatusID
      AND tps.OrgID = @POrgID
      AND tps.DeletedDate IS NULL;
END
GO
