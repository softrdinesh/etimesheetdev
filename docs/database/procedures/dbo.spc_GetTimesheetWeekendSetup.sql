/*
    Procedure: dbo.spc_GetTimesheetWeekendSetup
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-18 - DRAFT: new procedure.

    Returns the live weekend setups in dbo.TimeSheet_Weekend_setup, filtered
    by organisation, project or both, for
    GET api/v1/TimesheetProject/get-timesheet-weekend-setup?orgID=&projectID=.

    Parameters:
      @POrgID      the organisation (dbo.TimeSheet_Weekend_setup.OrgID).
                   NULL or 0 means "any organisation".
      @PProjectID  the project (dbo.TimeSheet_Weekend_setup.ProjectID). NULL
                   or 0 means "any project".

    Filter:
      - Only @POrgID given     -> every setup of that organisation.
      - Only @PProjectID given -> the setup of that project (at most one).
      - Both given             -> the setup of that project in that
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
      TimeSheetWeekendSetupID, OrganizationID (aliased from OrgID), ClientID,
      ClientName (from dbo.TimesheetClientSetup.Clientname), ProjectID,
      ProjectCode (from dbo.TimesheetProject.Code), ProjectName (from
      dbo.TimesheetProject.Projectname), IsMondayWeekend .. IsSundayWeekend,
      CreateDate, CreatedBy, UpdatedDate, UpdatedBy. The aliases are what the
      EF Core result mapping binds to.

      The names come from LEFT JOINs, so a setup with no (or an unknown)
      client or project is still returned, with NULL names.

    Order:
      By project name, then TimeSheetWeekendSetupID so the order is always
      the same.

    Result:
      No matching setups is an empty result, not an error.

    The tables are recorded at:
      ../schema/dbo.TimeSheet_Weekend_setup.sql
      ../schema/dbo.Timesheetclientsetup.sql
      ../schema/dbo.TimesheetProject.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetWeekendSetup]
    @POrgID      INT = NULL,
    @PProjectID  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

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
    WHERE ISNULL(tws.IsDelete, 0) = 0
      AND (ISNULL(@POrgID, 0) = 0 OR tws.OrgID = @POrgID)
      AND (ISNULL(@PProjectID, 0) = 0 OR tws.ProjectID = @PProjectID)
    ORDER BY tp.Projectname, tws.TimeSheetWeekendSetupID
    OPTION (RECOMPILE);
END
GO
