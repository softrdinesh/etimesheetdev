/*
    Procedure: dbo.spc_AddEditUserRoleByAdmin
    Status:    DRAFT for review - not applied to any database.

    Adds a role to an organisation in dbo.TimesheetUserRole, or renames one,
    and returns the saved row. Called by
    POST api/v1/Admin/add-edit-user-role.

    Parameters:
      @PRoleID      NULL or 0 adds a new role; any other value edits that role.
      @POrgID       the organisation the role belongs to. On edit, the role
                    must belong to THIS organisation - a role id from another
                    organisation is treated as not found.
      @PRoleName    the role's name.
      @PCreatedBy   written to Createdby on add only. An edit leaves the
                    original creator in place.
      @PCreateDate  written to CreateDate on add only - the API's clock (UTC),
                    passed in so the procedure and the API agree on "now".

    Add (@PRoleID NULL or 0):
      Inserts a live row (IsDelete = 0). RoleID is IDENTITY(1,1), so the
      database assigns it; the new id is read back with SCOPE_IDENTITY().

    Edit (@PRoleID > 0):
      Renames the role only if it belongs to @POrgID and is live
      (ISNULL(IsDelete, 0) = 0). A deleted role is not edited back to life.

    Result:
      The saved role - RoleID, RoleName - the same columns as
      dbo.spc_GetUserRoleListByOrgID. NO ROW when an edit names a role that
      does not exist, is deleted, or belongs to another organisation; the API
      answers 404 for that.

    Validation lives in the API (AdminService): a positive @POrgID, a
    non-blank @PRoleName of at most 50 characters, and no other live role of
    the same name in the organisation. The procedure does not repeat it.

    The table is recorded at:
      ../schema/dbo.TimesheetUserRole.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditUserRoleByAdmin]
    @PRoleID      INT           = NULL,
    @POrgID       INT,
    @PRoleName    NVARCHAR(50),
    @PCreatedBy   INT,
    @PCreateDate  DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @RoleID INT;

    IF ISNULL(@PRoleID, 0) = 0
    BEGIN
        INSERT INTO dbo.TimesheetUserRole
            (Rolename, OrganizationID, CreateDate, Createdby, IsDelete, Deleteddate, Deletedby)
        VALUES
            (@PRoleName, @POrgID, @PCreateDate, @PCreatedBy, 0, NULL, NULL);

        SET @RoleID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.TimesheetUserRole
        SET Rolename = @PRoleName
        WHERE RoleID = @PRoleID
          AND OrganizationID = @POrgID
          AND ISNULL(IsDelete, 0) = 0;

        SET @RoleID = @PRoleID;
    END

    -- =========================================================
    -- The saved row - same columns as spc_GetUserRoleListByOrgID
    -- =========================================================
    SELECT
        tur.RoleID    AS RoleID,
        tur.Rolename  AS RoleName
    FROM dbo.TimesheetUserRole tur
    WHERE tur.RoleID = @RoleID
      AND tur.OrganizationID = @POrgID
      AND ISNULL(tur.IsDelete, 0) = 0;
END
GO
