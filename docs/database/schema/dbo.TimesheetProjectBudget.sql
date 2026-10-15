/*
    Table: dbo.TimesheetProjectBudget
    Recorded: 2026-10-13 (sp_help column list supplied by the database owner,
              who created the table by hand)
    Revised:  2026-10-13 - BudgetID is IDENTITY(1,1) (stated by the owner).
    Revised:  2026-10-13 - by the owner: ProjectID changed from nchar(10) to
              int, and ProjectCode nvarchar(50) NULL added after it.

    A budget line of a project: hours and an hourly rate allocated to one
    project category over a period, and the amount that comes to.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetProjectBudget.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetProjectBudgetConfiguration.cs
      (for the hard delete in POST api/v1/TimesheetProject/remove-timesheet-project-budget)

    Read by:
      ../procedures/dbo.spc_GetTimesheetProjectBudget.sql
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-project-budget-by-projectID/{projectID})
      ../procedures/dbo.spc_GetTimesheetProjectBudgetByOrgID.sql
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-project-budget-by-orgID/{orgID})

    Written by:
      ../procedures/dbo.spc_AddEditTimesheetProjectBudget.sql
      (TimesheetProjectRepository, for POST api/v1/TimesheetProject/add-edit-timesheet-project-budget)

    Column quirks worth knowing:
      - BudgetID is IDENTITY(1,1): the database assigns it on insert.
      - ProjectID is dbo.TimesheetProject.ProjectID (int; nchar(10) until
        2026-10-13). No foreign key was reported.
      - ProjectCode is a copy of dbo.TimesheetProject.Code (PR0001 ...),
        written by spc_AddEditTimesheetProjectBudget from the project on every
        save - never taken from the API caller. Rows saved before the column
        was added hold NULL until they are next saved.
      - ONE budget per project, by rule of the API, not of the table: no
        unique constraint on ProjectID was reported.
        spc_AddEditTimesheetProjectBudget keeps it so by updating a
        project's existing row instead of adding a second.
      - Removing a project removes its budget rows too, in the same
        transaction - done by the API (TimesheetProjectRepository.DeleteAsync),
        because no foreign key with ON DELETE CASCADE was reported.
      - ProjectCategoryID is presumably dbo.TimesheetProjectCategory.ID; no
        foreign key was reported.
      - Column names mix conventions: PascalCase (AllocatedHours, PeriodStart),
        an irregular lower case (Hourlyrate) and snake_case
        (Allocated_amount). Map each one by its exact name.
      - AllocatedHours is decimal(18,2) - a number of hours, the same as
        dbo.TimesheetProject.Totalhours.
      - Allocated_amount is stored, not computed. The API works it out as
        AllocatedHours * Hourlyrate on every save, but nothing in the table
        keeps it so.
      - PeriodStart and PeriodEnd are date - no time part.
      - Only BudgetID is NOT NULL. Everything else is nullable.

    STILL UNCONFIRMED:
      - Whether BudgetID is the primary key. It is IDENTITY(1,1), but no key
        was reported, so none is recorded below.
      - Defaults, constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectBudget', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectBudget;
GO

CREATE TABLE dbo.TimesheetProjectBudget
(
    BudgetID           int             IDENTITY(1,1) NOT NULL,
    ProjectID          int             NULL,
    ProjectCode        nvarchar(50)    NULL,
    ProjectCategoryID  int             NULL,
    AllocatedHours     decimal(18,2)   NULL,
    Hourlyrate         decimal(18,2)   NULL,
    Allocated_amount   decimal(18,2)   NULL,
    PeriodStart        date            NULL,
    PeriodEnd          date            NULL
);
GO
