/*
    Procedure: dbo.spc_GetTimesheetApproverSetup
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-20 - DRAFT: new procedure.
    Revised:   2026-10-21 - DRAFT: @PApproverID filter removed; filter by
               organisation and project only.
    Revised:   2026-10-25 - DRAFT: IsLeaveApprover added to the result, after
               IsTimesheetApprover.

    Returns the live approver setups in dbo.Timesheet_Approver_setup, filtered
    by organisation, project or both, for
    GET api/v1/TimesheetProject/get-timesheet-approver-setup?orgID=&projectID=.

    Parameters:
      @POrgID       the organisation (dbo.Timesheet_Approver_setup.OrgID).
                    NULL or 0 means "any organisation".
      @PProjectID   the project (dbo.Timesheet_Approver_setup.ProjectID).
                    NULL or 0 means "any project".

    Filter:
      - Only @POrgID given     -> every setup of that organisation.
      - Only @PProjectID given -> the approvers of that project (at most one
                                  timesheet approver and one leave approver).
      - Both given             -> the approvers of that project in that
                                  organisation.
      - Neither given          -> the API refuses this with a 400 before the
                                  procedure is called, so the whole table is
                                  never returned.
      OPTION (RECOMPILE) so each combination gets its own plan, rather than
      one plan cached for whichever combination ran first.

    Live rows only:
      ISNULL(IsDelete, 0) = 0 - the add writes 0, the API's soft delete writes
      1, and a NULL is treated as live.

    Result columns:
      TimesheetApproverSetupID, OrganizationID (aliased from OrgID),
      ProjectID, ProjectCode (from dbo.TimesheetProject.Code), ProjectName
      (from dbo.TimesheetProject.Projectname), ApproverID, ApproverName (from
      dbo.Signup.Name), ApproverEmail (from dbo.Signup.Email),
      IsTimesheetApprover, IsLeaveApprover, CreateDate, CreatedBy. The aliases are what the EF
      Core result mapping binds to.

      The names come from LEFT JOINs, so a setup with no (or an unknown)
      project or approver is still returned, with NULL names.

    Order:
      By project name, then TimesheetApproverSetupID so the order is always
      the same.

    Result:
      No matching setups is an empty result, not an error.

    The tables are recorded at:
      ../schema/dbo.Timesheet_Approver_setup.sql
      ../schema/dbo.TimesheetProject.sql
    NOT RECORDED - dbo.Signup; the test container builds it from the EF
    mapping (SignupConfiguration).
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetApproverSetup]
    @POrgID       INT = NULL,
    @PProjectID   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

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
    WHERE ISNULL(tas.IsDelete, 0) = 0
      AND (ISNULL(@POrgID, 0) = 0 OR tas.OrgID = @POrgID)
      AND (ISNULL(@PProjectID, 0) = 0 OR tas.ProjectID = @PProjectID)
    ORDER BY tp.Projectname, tas.TimesheetApproverSetupID
    OPTION (RECOMPILE);
END
GO
