/*
    Procedure: dbo.spc_GetUserAccessSetup
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-18 - DRAFT: new procedure.

    Returns the module assignments in dbo.Timesheet_role_assignment, filtered
    by organisation, user or both, for
    GET api/v1/TimesheetProject/get-user-access-setup?orgID=&userID=.

    Parameters:
      @POrgID   the organisation - taken from the USER
                (dbo.Signup.OrganizationID), because
                dbo.Timesheet_role_assignment has no organisation column.
                NULL or 0 means "any organisation".
      @PUserID  the user (dbo.Timesheet_role_assignment.UserID). NULL or 0
                means "any user".

    Filter:
      - Only @POrgID given   -> every assignment of a live user of that
                                organisation.
      - Only @PUserID given  -> the user's assignment (at most one).
      - Both given           -> the user's assignment, if the user is in that
                                organisation.
      - Neither given        -> the API refuses this with a 400 before the
                                procedure is called, so the whole table is
                                never returned.
      OPTION (RECOMPILE) so each combination gets its own plan, rather than
      one plan cached for whichever combination ran first.

    Live users only:
      The table has no soft-delete column, so every assignment is live; the
      USER must be live - dbo.Signup.IsDelete = 0, as every other procedure on
      dbo.Signup treats only 0 as live. An assignment of a removed user is
      not listed.

    Result columns:
      AssignmentID, UserID, UserName (from dbo.Signup.Name), Email,
      OrganizationID (from dbo.Signup), IsClientSetup, IsProject,
      IsProjectBudget, IsProjectCategory, IsStatusSetup, IsRoleSetup,
      IsUserSetup, IsTimeLog (aliased from the Mod_ columns), CreateDate,
      CreatedBy, UpdatedDate, UpdatedBy (aliased from Updatedby). The aliases
      are what the EF Core result mapping binds to.

    Order:
      By user name, then AssignmentID so the order is always the same.

    Result:
      No matching assignments is an empty result, not an error.

    The table is recorded at:
      ../schema/dbo.Timesheet_role_assignment.sql
    NOT RECORDED - dbo.Signup; the test container builds it from the EF
    mapping (SignupConfiguration).
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetUserAccessSetup]
    @POrgID   INT = NULL,
    @PUserID  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

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
    INNER JOIN dbo.Signup s ON s.UserID = tra.UserID
    WHERE s.IsDelete = 0
      AND (ISNULL(@POrgID, 0) = 0 OR s.OrganizationID = @POrgID)
      AND (ISNULL(@PUserID, 0) = 0 OR tra.UserID = @PUserID)
    ORDER BY s.Name, tra.AssignmentID
    OPTION (RECOMPILE);
END
GO
