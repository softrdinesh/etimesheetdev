/*
    Table: dbo.TimesheetHolidaysetup
    Recorded: 2026-10-17 (sp_help column list supplied by the database owner,
              who created the table by hand)
    Revised:  2026-10-17 - TimesheetHolidaysetupID int IDENTITY(1,1) added
              as the primary key (stated by the owner).
    Revised:  2026-10-18 - by the owner: Description nvarchar(100) NULL
              added, after HolidayDate.

    One row per holiday date, scoped by organization and optionally narrowed
    to a country, client or project, with soft-delete and audit columns.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetHolidaySetup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetHolidaySetupConfiguration.cs
      (for the soft delete in POST api/v1/TimesheetProject/remove-timesheet-holiday-setup)

    Read by:
      ../procedures/dbo.spc_GetTimesheetHolidaySetup.sql
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-holiday-setup)

    Written by:
      ../procedures/dbo.spc_AddEditTimesheetHolidaySetup.sql
      (TimesheetProjectRepository, for POST api/v1/TimesheetProject/add-edit-timesheet-holiday-setup)

    Column quirks worth knowing:
      - TimesheetHolidaysetupID is IDENTITY(1,1) and the primary key: the
        database assigns it on insert.
      - Column names are irregularly cased: Holidaysetup in the table name
        (lower-case "setup"), IsDelete (not IsDeleted). Map each one by its
        exact name.
      - Every column but TimesheetHolidaysetupID is nullable - including
        HolidayDate and IsDelete, so a row may have no date, and may be
        neither deleted nor not-deleted.
      - HolidayDate is date (no time part).
      - Description is nvarchar(100), nullable - the holiday's name, such as
        "Christmas Day".
      - OrganizationID is presumably dbo.Organization's id, CountryID a
        dbo.Country id, ClientID a dbo.TimesheetClientSetup.ClientID and
        ProjectID a dbo.TimesheetProject.ProjectID. No foreign keys were
        reported.
      - CreateDate is plain datetime - local or UTC is not stated.
      - CreatedBy is an int user id; no foreign key was reported.

    STILL UNCONFIRMED:
      - The column's position. It is recorded first; if it was added with
        ALTER TABLE ... ADD it is physically last, which changes nothing for
        the mapping.
      - Defaults, constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.TimesheetHolidaysetup', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetHolidaysetup;
GO

CREATE TABLE dbo.TimesheetHolidaysetup
(
    TimesheetHolidaysetupID int     IDENTITY(1,1) NOT NULL,
    HolidayDate     date        NULL,
    Description     nvarchar(100)   NULL,
    OrganizationID  int         NULL,
    CountryID       int         NULL,
    ClientID        int         NULL,
    ProjectID       int         NULL,
    CreateDate      datetime    NULL,
    CreatedBy       int         NULL,
    IsDelete        bit         NULL,
    CONSTRAINT PK_TimesheetHolidaysetup PRIMARY KEY (TimesheetHolidaysetupID)
);
GO
