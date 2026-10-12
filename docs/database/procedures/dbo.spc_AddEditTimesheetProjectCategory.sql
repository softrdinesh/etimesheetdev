/*
    Procedure: dbo.spc_AddEditTimesheetProjectCategory
    Status:    DRAFT for review - not applied to any database.

    Adds a project category to an organisation in
    dbo.TimesheetProjectCategory, or renames one, and returns the saved row.
    Called by POST api/v1/Admin/add-edit-timesheet-project-category.

    Parameters:
      @PCategoryID   NULL or 0 adds a new category; any other value edits that
                     category.
      @POrgID        the organisation the category belongs to. On edit, the
                     category must belong to THIS organisation - an id from
                     another organisation is treated as not found.
      @PCategoryName the category's name.
      @PCreatedBy    written to CreatedBy on add only. An edit leaves the
                     original creator in place.
      @PCreateDate   written to CreateDate on add only - the API's clock (UTC),
                     passed in so the procedure and the API agree on "now".

    Add (@PCategoryID NULL or 0):
      Inserts a live row (IsDelete = 0). ID is IDENTITY(1,1), so the database
      assigns it; the new id is read back with SCOPE_IDENTITY().

    Edit (@PCategoryID > 0):
      Renames the category only if it belongs to @POrgID and is live
      (ISNULL(IsDelete, 0) = 0). A deleted category is not edited back to life.

    Result:
      The saved category - CategoryID, CategoryName - the same columns as
      dbo.spc_GetTimesheetProjectCategory. NO ROW when an edit names a
      category that does not exist, is deleted, or belongs to another
      organisation; the API answers 404 for that.

    Validation lives in the API (AdminService): a positive @POrgID and
    @PCreatedBy, and a non-blank @PCategoryName of at most 50 characters. The
    procedure does not repeat it.

    The table is recorded at:
      ../schema/dbo.TimesheetProjectCategory.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditTimesheetProjectCategory]
    @PCategoryID    INT           = NULL,
    @POrgID         INT,
    @PCategoryName  NVARCHAR(50),
    @PCreatedBy     INT,
    @PCreateDate    DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @CategoryID INT;

    IF ISNULL(@PCategoryID, 0) = 0
    BEGIN
        INSERT INTO dbo.TimesheetProjectCategory
            (Categoryname, OrganizationID, IsDelete, CreateDate, CreatedBy, DeletedDate, Deletedby)
        VALUES
            (@PCategoryName, @POrgID, 0, @PCreateDate, @PCreatedBy, NULL, NULL);

        SET @CategoryID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.TimesheetProjectCategory
        SET Categoryname = @PCategoryName
        WHERE ID = @PCategoryID
          AND OrganizationID = @POrgID
          AND ISNULL(IsDelete, 0) = 0;

        SET @CategoryID = @PCategoryID;
    END

    -- =================================================================
    -- The saved row - same columns as spc_GetTimesheetProjectCategory
    -- =================================================================
    SELECT
        tpc.ID              AS CategoryID,
        tpc.Categoryname    AS CategoryName
    FROM dbo.TimesheetProjectCategory tpc
    WHERE tpc.ID = @CategoryID
      AND tpc.OrganizationID = @POrgID
      AND ISNULL(tpc.IsDelete, 0) = 0;
END
GO
