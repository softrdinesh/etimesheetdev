/*
    Table: dbo.Timesheet_role_assignment
    Recorded: 2026-10-18 (sp_help column list supplied by the database owner,
              who created the table by hand; AssignmentID is IDENTITY(1,1) as
              stated by the owner)

    Which modules of the application a user may use - one bit per module -
    with audit columns.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetRoleAssignment.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetRoleAssignmentConfiguration.cs
      (for the one-assignment-per-user check and the hard delete in
      POST api/v1/TimesheetProject/remove-user-access-setup)

    Read by:
      ../procedures/dbo.spc_GetUserAccessSetup.sql
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-user-access-setup)

    Written by:
      ../procedures/dbo.spc_AddEditUserAccessSetup.sql
      (TimesheetProjectRepository, for POST api/v1/TimesheetProject/add-edit-user-access-setup)

    Column quirks worth knowing:
      - The table name is exactly "Timesheet_role_assignment" - underscores,
        lower-case "role" and "assignment". Map it by its exact name.
      - AssignmentID is IDENTITY(1,1): the database assigns it on insert. It
        is the only NOT NULL column.
      - UserID is presumably a dbo.Signup.UserID; no foreign key was
        reported. It is nullable.
      - One bit per module, each prefixed "Mod_" and irregularly cased:
          Mod_IsclientSetup      (lower-case "client")
          Mod_Isproject          (lower-case "project")
          Mod_IsProjectbudget    (lower-case "budget")
          Mod_IsProjectCategory
          Mod_IsStatusSetup
          Mod_IsRoleSetup
          Mod_IsUsersetup        (lower-case "setup")
          Mod_IsTimeLog
        Map each one by its exact name. 1 presumably grants the module; all
        are nullable, so a module may be neither granted nor refused.
      - Audit columns are irregularly cased too: Updatedby (lower-case "by"),
        alongside CreatedBy.
      - CreateDate and UpdatedDate are plain datetime - local or UTC is not
        stated.
      - CreatedBy and Updatedby are int user ids; no foreign key was reported.
      - There is no soft-delete column: a removed row would be physically
        deleted.

    STILL UNCONFIRMED:
      - Whether AssignmentID is the primary key. It is IDENTITY(1,1), but no
        key was reported, so none is recorded below.
      - One assignment per user is a rule of the API and of
        spc_AddEditUserAccessSetup, not of the table: no unique
        constraint on UserID was reported. A unique index on UserID would
        make the table enforce it too - that is the owner's call.
      - Defaults, constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.Timesheet_role_assignment', N'U') IS NOT NULL
    DROP TABLE dbo.Timesheet_role_assignment;
GO

CREATE TABLE dbo.Timesheet_role_assignment
(
    AssignmentID            int         IDENTITY(1,1) NOT NULL,
    UserID                  int         NULL,
    Mod_IsclientSetup       bit         NULL,
    Mod_Isproject           bit         NULL,
    Mod_IsProjectbudget     bit         NULL,
    Mod_IsProjectCategory   bit         NULL,
    Mod_IsStatusSetup       bit         NULL,
    Mod_IsRoleSetup         bit         NULL,
    Mod_IsUsersetup         bit         NULL,
    Mod_IsTimeLog           bit         NULL,
    CreateDate              datetime    NULL,
    CreatedBy               int         NULL,
    UpdatedDate             datetime    NULL,
    Updatedby               int         NULL
);
GO
