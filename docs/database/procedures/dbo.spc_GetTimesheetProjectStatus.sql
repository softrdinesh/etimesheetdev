/*
    Procedure: dbo.spc_GetTimesheetProjectStatus
    Status:    DRAFT for review - not applied to any database.
    Revised:   2026-10-15 - DRAFT: statuses are now per organisation.
               @POrgID added, deleted statuses excluded, ColorCode returned.

    Returns the live project statuses of one organisation from
    dbo.TimesheetProjectStatus, for
    GET api/v1/TimesheetProject/get-timesheet-project-status-by-orgID/{orgID}.

    Parameters:
      @POrgID  the organisation (dbo.TimesheetProjectStatus.OrgID).

    Statuses:
      Only live rows - DeletedDate IS NULL. The table has no IsDelete flag;
      the delete stamp is the only marker.

    Result columns:
      StatusID, Status, ColorCode (aliased from Colorcode). The names are what
      an EF Core result mapping binds to. OrgID is not returned - the caller
      already knows it.

    Order:
      By StatusID - the order the organisation added them in, so a workflow
      such as Open -> Pending -> Closed reads in the order it was set up, and
      a dropdown built from it is stable.

    Result:
      An organisation with no statuses gets an empty result, not an error.

    The table is recorded at:
      ../schema/dbo.TimesheetProjectStatus.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetProjectStatus]
    @POrgID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tps.StatusID     AS StatusID,
        tps.Status       AS Status,
        tps.Colorcode    AS ColorCode
    FROM dbo.TimesheetProjectStatus tps
    WHERE tps.OrgID = @POrgID
      AND tps.DeletedDate IS NULL
    ORDER BY tps.StatusID;
END
GO
