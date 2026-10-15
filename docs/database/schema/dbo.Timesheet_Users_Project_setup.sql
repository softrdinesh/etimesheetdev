/*
    Table: dbo.Timesheet_Users_Project_setup
    Recorded: 2026-10-15 (sp_help column list supplied by the database owner;
              the table already exists)
    Revised:  2026-10-16 - DRAFT, requested by the owner, NOT YET APPLIED:
              TimesheetUserProjectSetupID int IDENTITY(1,1) NOT NULL added as
              the primary key (PK_Timesheet_Users_Project_setup), as the last
              column, where ALTER TABLE ... ADD puts it. Change to applied
              once the owner has run it.

    A user's assignment to a project: their project role, cost and billable
    rates per hour, job type, work-location range and the project location.
    One row per assignment.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetUserProjectSetup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetUserProjectSetupConfiguration.cs
      (for POST api/v1/TimesheetProject/edit-timesheet-user and
      POST api/v1/TimesheetProject/remove-timesheet-user)

    Written by:
      ../procedures/dbo.spc_Signup_timesheet_user.sql
      (TimesheetProjectRepository, for POST api/v1/TimesheetProject/create-timesheet-user)
      TimesheetProjectRepository through EF Core - the edit, by
      TimesheetUserProjectSetupID, and the soft delete (IsDelete = 1,
      DeletedDate, DeletedBy) of every live row of one UserID + ProjectID

    Column quirks worth knowing:
      - Column names mix conventions: snake_case (Cost_rate_per_hour,
        Billable_rate_per_hour), irregular lower case (Createdby, Jobtype,
        ProjectlocLatitude) and PascalCase (DeletedDate, DeletedBy,
        WorklocationRange). Map each one by its exact name.
      - TimesheetUserProjectSetupID is IDENTITY(1,1) and the primary key: the
        database assigns it on insert, and numbered the existing rows when the
        column was added. spc_Signup_timesheet_user does not list it, so its
        inserts are unaffected.
      - UserID is dbo.Signup.UserID. No foreign key was reported.
      - ProjectID is presumably dbo.TimesheetProject.ProjectID, and
        ProjectRoleID presumably dbo.TimesheetUserRole.RoleID. No foreign key
        was reported for either.
      - Cost_rate_per_hour and Billable_rate_per_hour are decimal(18,2).
      - IsDelete is an int, nullable, as on dbo.Signup - not a bit.
        spc_Signup_timesheet_user writes 0 and the API's soft delete writes 1;
        the API treats 0 or NULL as live.
      - CreateDate and DeletedDate are plain datetime. spc_Signup_timesheet_user
        writes CreateDate with GETDATE() - server local time, not UTC.
      - ProjectlocLatitude and ProjectlocLongtitude ("Longtitude" - the
        database's spelling) are nvarchar(25): text, not numbers.
        spc_Signup_timesheet_user writes the latitude for a new user only, and
        never writes the longitude.
      - Nothing stops the same user being assigned to the same project twice:
        no unique constraint was reported, and spc_Signup_timesheet_user does
        not check.

    STILL UNCONFIRMED:
      - Defaults, other constraints and indexes - none were supplied.

    Lengths below are characters. sp_help reports nvarchar lengths in BYTES, so
    the 50 it prints for ProjectlocLatitude / ProjectlocLongtitude is 25
    characters.
*/

IF OBJECT_ID(N'dbo.Timesheet_Users_Project_setup', N'U') IS NOT NULL
    DROP TABLE dbo.Timesheet_Users_Project_setup;
GO

CREATE TABLE dbo.Timesheet_Users_Project_setup
(
		TimesheetUserProjectSetupID int     IDENTITY(1,1) NOT NULL,
    UserID                  int             NOT NULL,
    ProjectRoleID           int             NULL,
    Cost_rate_per_hour      decimal(18,2)   NULL,
    Billable_rate_per_hour  decimal(18,2)   NULL,
    CreateDate              datetime        NULL,
    Createdby               int             NULL,
    IsDelete                int             NULL,
    DeletedDate             datetime        NULL,
    DeletedBy               int             NULL,
    Jobtype                 int             NULL,
    WorklocationRange       int             NULL,
    ProjectID               int             NULL,
    ProjectlocLatitude      nvarchar(25)    NULL,
    ProjectlocLongtitude    nvarchar(25)    NULL,
    CONSTRAINT PK_Timesheet_Users_Project_setup PRIMARY KEY (TimesheetUserProjectSetupID)
);
GO
