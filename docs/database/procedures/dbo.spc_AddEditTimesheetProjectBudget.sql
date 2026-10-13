/*
    Procedure: dbo.spc_AddEditTimesheetProjectBudget
    Status:    DRAFT for review - not applied to any database.
    Revised:   2026-10-13 - ProjectID is now an int on the table, so it is
               written and compared directly; ProjectCode is copied from
               dbo.TimesheetProject.Code on every save and returned.
    Revised:   2026-10-13 - ONE budget per project: @PBudgetID removed. The
               project decides add or update - its existing budget is
               updated, and a new row is added only when it has none.

    Saves THE budget of a project in dbo.TimesheetProjectBudget - adding it
    the first time, updating it every time after - and returns the saved row.
    Called by POST api/v1/TimesheetProject/add-edit-timesheet-project-budget.

    Parameters:
      @PProjectID         the project (dbo.TimesheetProject.ProjectID, an int).
                          It alone decides add or update.
      @PProjectCategoryID the project category.
      @PAllocatedHours    hours allocated, decimal(18,2).
      @PHourlyRate        the hourly rate, decimal(18,2).
      @PAllocatedAmount   the amount, decimal(18,2) - worked out by the API
                          as hours x rate; the procedure stores what it is
                          given.
      @PPeriodStart       first day of the period.
      @PPeriodEnd         last day of the period.

    ProjectCode (add and update alike):
      Copied from dbo.TimesheetProject.Code of @PProjectID - never a
      parameter. Re-copied on every update, so a budget saved before the
      column existed picks its code up the next time it is saved.

    Add or update:
      The project's budget is looked up WITH (UPDLOCK, HOLDLOCK) inside the
      transaction, so two saves for the same project at the same moment
      cannot both find nothing and both insert: the second waits for the
      first to commit, then finds its row and updates it.

      - The project has a budget: it is updated - every column except
        BudgetID and ProjectID. A NULL parameter writes NULL; the payload is
        the whole budget, not a patch.
      - The project has none: a row is inserted. BudgetID is IDENTITY(1,1),
        so the database assigns it; it is read back with SCOPE_IDENTITY().

      Should a project already hold more than one row - saved before this
      rule existed - the one with the LOWEST BudgetID is the budget that is
      updated and returned. The others are left alone. Nothing in the table
      enforces one row per project; this procedure is what keeps it so.

    Result:
      The saved budget - the same columns as dbo.spc_GetTimesheetProjectBudget.
      Always exactly one row.

    Validation lives in the API (TimesheetProjectService): the project exists,
    the category is a live category of the project's organisation,
    non-negative hours and rate, and @PPeriodEnd not before @PPeriodStart.
    The procedure does not repeat it.

    The tables are recorded at:
      ../schema/dbo.TimesheetProjectBudget.sql
      ../schema/dbo.TimesheetProjectCategory.sql
*/

CREATE OR ALTER PROCEDURE [dbo].[spc_AddEditTimesheetProjectBudget]
    @PProjectID         INT,
    @PProjectCategoryID INT             = NULL,
    @PAllocatedHours    DECIMAL(18,2)   = NULL,
    @PHourlyRate        DECIMAL(18,2)   = NULL,
    @PAllocatedAmount   DECIMAL(18,2)   = NULL,
    @PPeriodStart       DATE            = NULL,
    @PPeriodEnd         DATE            = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET XACT_ABORT ON;

    DECLARE @ProjectCode NVARCHAR(50) =
    (
        SELECT tp.Code
        FROM dbo.TimesheetProject tp
        WHERE tp.ProjectID = @PProjectID
    );

    BEGIN TRANSACTION;

    -- =================================================================
    -- The project's budget, held locked until COMMIT
    -- =================================================================
    DECLARE @BudgetID INT =
    (
        SELECT MIN(tpb.BudgetID)
        FROM dbo.TimesheetProjectBudget tpb WITH (UPDLOCK, HOLDLOCK)
        WHERE tpb.ProjectID = @PProjectID
    );

    IF @BudgetID IS NULL
    BEGIN
        INSERT INTO dbo.TimesheetProjectBudget
            (ProjectID, ProjectCode, ProjectCategoryID, AllocatedHours, Hourlyrate, Allocated_amount, PeriodStart, PeriodEnd)
        VALUES
            (@PProjectID, @ProjectCode, @PProjectCategoryID, @PAllocatedHours, @PHourlyRate, @PAllocatedAmount, @PPeriodStart, @PPeriodEnd);

        SET @BudgetID = CAST(SCOPE_IDENTITY() AS INT);
    END
    ELSE
    BEGIN
        UPDATE dbo.TimesheetProjectBudget
        SET ProjectCode       = @ProjectCode,
            ProjectCategoryID = @PProjectCategoryID,
            AllocatedHours    = @PAllocatedHours,
            Hourlyrate        = @PHourlyRate,
            Allocated_amount  = @PAllocatedAmount,
            PeriodStart       = @PPeriodStart,
            PeriodEnd         = @PPeriodEnd
        WHERE BudgetID = @BudgetID;
    END

    COMMIT TRANSACTION;

    -- =================================================================
    -- The saved row - same columns as spc_GetTimesheetProjectBudget
    -- =================================================================
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
    WHERE tpb.BudgetID = @BudgetID;
END
GO
