/*
    Procedure: dbo.spc_GetTimesheetProjectStatus
    Status:    DRAFT for review - not applied to any database.

    Returns every project status from the dbo.TimesheetProjectStatus lookup,
    for GET api/v1/TimesheetProject/get-timesheet-project-status-list.

    Parameters:
      None. The lookup is shared by every organisation.

    Result columns:
      StatusID, Status. The names are what an EF Core result mapping binds to.

    Order:
      By StatusID - the order the owner inserted them in (1 Open, 2 Closed,
      3 Pending), so a dropdown built from it is stable.

    The table is recorded at:
      ../schema/dbo.TimesheetProjectStatus.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetProjectStatus]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tps.StatusID    AS StatusID,
        tps.Status      AS Status
    FROM dbo.TimesheetProjectStatus tps
    ORDER BY tps.StatusID;
END
GO
