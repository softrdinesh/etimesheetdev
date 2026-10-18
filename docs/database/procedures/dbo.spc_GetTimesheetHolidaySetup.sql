/*
    Procedure: dbo.spc_GetTimesheetHolidaySetup
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-17 - DRAFT: new procedure.
    Revised:   2026-10-18 - DRAFT: Description returned.

    Returns the live holidays in dbo.TimesheetHolidaysetup, filtered by
    organisation, project or both, for
    GET api/v1/TimesheetProject/get-timesheet-holiday-setup?orgID=&projectID=.

    Parameters:
      @POrgID      the organisation (dbo.TimesheetHolidaysetup.OrganizationID).
                   NULL or 0 means "any organisation".
      @PProjectID  the project (dbo.TimesheetHolidaysetup.ProjectID). NULL or
                   0 means "any project".

    Filter:
      - Only @POrgID given     -> every holiday of that organisation, whatever
                                  its project (including none).
      - Only @PProjectID given -> every holiday of that project, whatever its
                                  organisation.
      - Both given             -> holidays of that project in that
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
      TimesheetHolidaySetupID (aliased from TimesheetHolidaysetupID),
      HolidayDate, Description, OrganizationID, CountryID, CountryName (from
      dbo.Country.Name), ClientID, ClientName (from
      dbo.TimesheetClientSetup.Clientname), ProjectID, ProjectCode (from
      dbo.TimesheetProject.Code), ProjectName (from
      dbo.TimesheetProject.Projectname), CreateDate, CreatedBy. The aliases
      are what the EF Core result mapping binds to.

      The names come from LEFT JOINs, so a holiday with no (or an unknown)
      country, client or project is still returned, with NULL names.

    Order:
      By HolidayDate, then TimesheetHolidaysetupID so the order is always the
      same.

    Result:
      No matching holidays is an empty result, not an error.

    The tables are recorded at:
      ../schema/dbo.TimesheetHolidaysetup.sql
      ../schema/dbo.Country.sql
      ../schema/dbo.Timesheetclientsetup.sql
      ../schema/dbo.TimesheetProject.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetHolidaySetup]
    @POrgID      INT = NULL,
    @PProjectID  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ths.TimesheetHolidaysetupID AS TimesheetHolidaySetupID,
        ths.HolidayDate             AS HolidayDate,
        ths.Description             AS Description,
        ths.OrganizationID          AS OrganizationID,
        ths.CountryID               AS CountryID,
        c.Name                      AS CountryName,
        ths.ClientID                AS ClientID,
        tcs.Clientname              AS ClientName,
        ths.ProjectID               AS ProjectID,
        tp.Code                     AS ProjectCode,
        tp.Projectname              AS ProjectName,
        ths.CreateDate              AS CreateDate,
        ths.CreatedBy               AS CreatedBy
    FROM dbo.TimesheetHolidaysetup ths
    LEFT JOIN dbo.Country c ON c.ID = ths.CountryID
    LEFT JOIN dbo.TimesheetClientSetup tcs ON tcs.ClientID = ths.ClientID
    LEFT JOIN dbo.TimesheetProject tp ON tp.ProjectID = ths.ProjectID
    WHERE ISNULL(ths.IsDelete, 0) = 0
      AND (ISNULL(@POrgID, 0) = 0 OR ths.OrganizationID = @POrgID)
      AND (ISNULL(@PProjectID, 0) = 0 OR ths.ProjectID = @PProjectID)
    ORDER BY ths.HolidayDate, ths.TimesheetHolidaysetupID
    OPTION (RECOMPILE);
END
GO
