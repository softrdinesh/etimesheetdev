/*
    Procedure: dbo.spc_GetTimesheetProjectBudget
    Status:    DRAFT for review - not applied to any database.
    Revised:   2026-10-13 - ProjectID is now an int on the table, so it is
               compared directly; ProjectCode added to the result.

    Returns the budget lines of one project from dbo.TimesheetProjectBudget,
    for GET api/v1/TimesheetProject/get-timesheet-project-budget-by-projectID/{projectID}.

    Parameters:
      @PProjectID  the project (dbo.TimesheetProject.ProjectID, an int).

    Result columns:
      BudgetID, ProjectCode, ProjectCategoryID, CategoryName (from
      dbo.TimesheetProjectCategory), AllocatedHours, HourlyRate (aliased from
      Hourlyrate), AllocatedAmount (aliased from Allocated_amount),
      PeriodStart, PeriodEnd. The aliases are what an EF Core result mapping
      binds to. ProjectID is not returned - the caller already knows it.

      CategoryName comes from a LEFT JOIN, so a budget line whose category is
      NULL, unknown or deleted is still returned, with a NULL CategoryName.

    Order:
      By PeriodStart, then BudgetID so the order is always the same.

    Result:
      A project with no budget lines gets an empty result, not an error.

    The tables are recorded at:
      ../schema/dbo.TimesheetProjectBudget.sql
      ../schema/dbo.TimesheetProjectCategory.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_GetTimesheetProjectBudget]
    @PProjectID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tpb.BudgetID            AS BudgetID,
        tpb.ProjectCode         AS ProjectCode,
        tpb.ProjectCategoryID   AS ProjectCategoryID,
        tpc.Categoryname        AS CategoryName,
        tpb.AllocatedHours      AS AllocatedHours,
        tpb.Hourlyrate          AS HourlyRate,
        tpb.Allocated_amount    AS AllocatedAmount,
        tpb.PeriodStart         AS PeriodStart,
        tpb.PeriodEnd           AS PeriodEnd
    FROM dbo.TimesheetProjectBudget tpb
    LEFT JOIN dbo.TimesheetProjectCategory tpc
        ON tpc.ID = tpb.ProjectCategoryID
       AND ISNULL(tpc.IsDelete, 0) = 0
    WHERE tpb.ProjectID = @PProjectID
    ORDER BY tpb.PeriodStart, tpb.BudgetID;
END
GO
