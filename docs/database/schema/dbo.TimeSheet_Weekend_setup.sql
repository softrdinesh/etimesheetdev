/*
    Table: dbo.TimeSheet_Weekend_setup
    Recorded: 2026-10-18 (sp_help column list supplied by the database owner,
              who created the table by hand)
    Revised:  2026-10-18 - TimeSheetWeekendSetupID int IDENTITY(1,1) added
              as the primary key (stated by the owner).

    Which days of the week are weekend days, per organization and optionally
    narrowed to a client or project, with soft-delete and audit columns.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetWeekendSetup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetWeekendSetupConfiguration.cs
      (for the one-setup-per-project check and the soft delete in
      POST api/v1/TimesheetProject/remove-timesheet-weekend-setup)

    Read by:
      ../procedures/dbo.spc_GetTimesheetWeekendSetup.sql
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-weekend-setup)

    Written by:
      ../procedures/dbo.spc_AddEditTimesheetWeekendSetup.sql
      (TimesheetProjectRepository, for POST api/v1/TimesheetProject/add-edit-timesheet-weekend-setup)

    Column quirks worth knowing:
      - TimeSheetWeekendSetupID is IDENTITY(1,1) and the primary key: the
        database assigns it on insert. Note it has no underscores, unlike
        the table name.
      - The table name is exactly "TimeSheet_Weekend_setup" - capital "S" in
        "TimeSheet", underscores, lower-case "setup" - unlike
        dbo.TimesheetHolidaysetup. Map it by its exact name.
      - OrgID is the only NOT NULL column besides TimeSheetWeekendSetupID -
        presumably dbo.Organization's id; no foreign key was reported. Note
        the name: OrgID, not OrganizationID as on dbo.TimesheetHolidaysetup.
      - ClientID is presumably a dbo.TimesheetClientSetup.ClientID and
        ProjectID a dbo.TimesheetProject.ProjectID. No foreign keys were
        reported.
      - One bit per weekday, IsMondayWeekend .. IsSundayWeekend; 1 means that
        day is a weekend day. All nullable, so a day may be neither.
      - IsDelete is a nullable bit, so a row may be neither deleted nor
        not-deleted.
      - CreateDate, DeletedDate and UpdatedDate are plain datetime - local or
        UTC is not stated.
      - CreatedBy, DeletedBy and UpdatedBy are int user ids; no foreign key
        was reported.

    STILL UNCONFIRMED:
      - TimeSheetWeekendSetupID's position. It is recorded first; if it was
        added with ALTER TABLE ... ADD it is physically last, which changes
        nothing for a mapping.
      - One live setup per project is a rule of the API and of
        spc_AddEditTimesheetWeekendSetup, not of the table: no unique
        constraint was reported. A filtered unique index on ProjectID
        WHERE IsDelete = 0 would make the table enforce it too (the add
        always writes 0; a filtered index cannot use ISNULL or OR) - that is
        the owner's call.
      - Defaults, constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.TimeSheet_Weekend_setup', N'U') IS NOT NULL
    DROP TABLE dbo.TimeSheet_Weekend_setup;
GO

CREATE TABLE dbo.TimeSheet_Weekend_setup
(
    TimeSheetWeekendSetupID int     IDENTITY(1,1) NOT NULL,
    OrgID               int         NOT NULL,
    ClientID            int         NULL,
    ProjectID           int         NULL,
    IsMondayWeekend     bit         NULL,
    IsTuesdayWeekend    bit         NULL,
    IsWednesdayWeekend  bit         NULL,
    IsThursdayWeekend   bit         NULL,
    IsFridayWeekend     bit         NULL,
    IsSaturdayWeekend   bit         NULL,
    IsSundayWeekend     bit         NULL,
    CreateDate          datetime    NULL,
    CreatedBy           int         NULL,
    IsDelete            bit         NULL,
    DeletedDate         datetime    NULL,
    DeletedBy           int         NULL,
    UpdatedDate         datetime    NULL,
    UpdatedBy           int         NULL,
    CONSTRAINT PK_TimeSheet_Weekend_setup PRIMARY KEY (TimeSheetWeekendSetupID)
);
GO
