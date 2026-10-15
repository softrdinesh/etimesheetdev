/*
    Procedure: dbo.spc_GetTimesheetProjectBudgetByOrgID
    Status:    DRAFT for review - not applied to any database.
    Recorded:  2026-10-15 - DRAFT: new procedure.

    Returns the budget lines of every project of one organisation from
    dbo.TimesheetProjectBudget, for
    GET api/v1/TimesheetProject/get-timesheet-project-budget-by-orgID/{orgID}.

    Parameters:
      @POrgID  the organisation (dbo.TimesheetProject.OrganizationID).

    Organisation:
      dbo.TimesheetProjectBudget has no organisation column, so the
      organisation is reached through the project: an INNER JOIN on
      dbo.TimesheetProject.ProjectID. A budget line whose project no longer
      exists belongs to no organisation and is not returned.

    Result columns:
      ProjectID, ProjectCode, ProjectName (aliased from Projectname),
      BudgetID, ProjectCategoryID, CategoryName (from
      dbo.TimesheetProjectCategory), AllocatedHours, HourlyRate (aliased from
      Hourlyrate), AllocatedAmount (aliased from Allocated_amount),
      PeriodStart, PeriodEnd. The aliases are what an EF Core result mapping
      binds to. OrganizationID is not returned - the caller already knows it.

      ProjectCode is taken from dbo.TimesheetProject.Code, not from the copy
      on the budget row: the copy is NULL on rows saved before the column was
      added, and the project's own code is the source of it anyway.

      CategoryName comes from a LEFT JOIN, so a budget line whose category is
      NULL, unknown or deleted is still returned, with a NULL CategoryName.

    Order:
      By project name, then PeriodStart, then BudgetID so the order is always
      the same.

    Result:
      An organisation with no budget lines gets an empty result, not an error.
      A project with no budget is not listed - this is a list of budget lines,
      not of projects.

    The tables are recorded at:
      ../schema/dbo.TimesheetProjectBudget.sql
      ../schema/dbo.TimesheetProject.sql
      ../schema/dbo.TimesheetProjectCategory.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetProjectBudgetByOrgID]
    @POrgID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tp.ProjectID            AS ProjectID,
        tp.Code                 AS ProjectCode,
        tp.Projectname          AS ProjectName,
        tpb.BudgetID            AS BudgetID,
        tpb.ProjectCategoryID   AS ProjectCategoryID,
        tpc.Categoryname        AS CategoryName,
        tpb.AllocatedHours      AS AllocatedHours,
        tpb.Hourlyrate          AS HourlyRate,
        tpb.Allocated_amount    AS AllocatedAmount,
        tpb.PeriodStart         AS PeriodStart,
        tpb.PeriodEnd           AS PeriodEnd
    FROM dbo.TimesheetProjectBudget tpb
    INNER JOIN dbo.TimesheetProject tp
        ON tp.ProjectID = tpb.ProjectID
    LEFT JOIN dbo.TimesheetProjectCategory tpc
        ON tpc.ID = tpb.ProjectCategoryID
       AND ISNULL(tpc.IsDelete, 0) = 0
    WHERE tp.OrganizationID = @POrgID
    ORDER BY tp.Projectname, tpb.PeriodStart, tpb.BudgetID;
END
GO
