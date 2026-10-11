/*
    Procedure: dbo.spc_GetUserRoleListByOrgID
    Recorded:  2026-10-11 (applied by the database owner, who dropped
               OrganizationID from the result - the caller already knows it)

    Returns the live roles of one organisation from dbo.TimesheetUserRole,
    for GET api/v1/Admin/get-user-role-list-by-orgid/{orgID}.

    Parameters:
      @POrgID  the organisation (dbo.TimesheetUserRole.OrganizationID).

    Roles:
      Only live rows - IsDelete 0 OR NULL. The column is a nullable bit, and
      only 1 means deleted, so the test is ISNULL(IsDelete, 0) = 0; a plain
      "IsDelete = 0" would silently drop every role whose flag was never set.
      A role with no OrganizationID belongs to no organisation and is never
      returned.

    Result columns:
      RoleID, RoleName (aliased from Rolename).
      The aliases are what an EF Core result mapping binds to.

    Order:
      By RoleName, then RoleID so two roles with the same name always come
      back in the same order.

    Result:
      An organisation with no roles gets an empty result, not an error.

    The table is recorded at:
      ../schema/dbo.TimesheetUserRole.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetUserRoleListByOrgID]
    @POrgID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tur.RoleID          AS RoleID,
        tur.Rolename        AS RoleName
    FROM dbo.TimesheetUserRole tur
    WHERE tur.OrganizationID = @POrgID
      AND ISNULL(tur.IsDelete, 0) = 0
    ORDER BY tur.Rolename, tur.RoleID;
END
GO
