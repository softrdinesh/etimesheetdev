/*
    Table: dbo.Timesheet_Approver_setup
    Recorded: 2026-10-20 (sp_help column list supplied by the database owner;
              the table already exists)
    Revised:  2026-10-20 - TimesheetApproverSetupID int IDENTITY(1,1) NOT
              NULL added as the primary key (PK_Timesheet_Approver_setup), per
              the database owner.

    The timesheet approver of each project - one per project - with
    audit and soft-delete columns.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetApproverSetup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetApproverSetupConfiguration.cs
      (for the one-approver-per-project check and the soft delete in
      POST api/v1/TimesheetProject/remove-timesheet-approver-setup)

    Read by:
      ../procedures/dbo.spc_GetTimesheetApproverSetup.sql
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-approver-setup)

    Written by:
      ../procedures/dbo.spc_AddEditTimesheetApproverSetup.sql
      (TimesheetProjectRepository, for POST api/v1/TimesheetProject/add-edit-timesheet-approver-setup)
      TimesheetProjectRepository through EF Core - the soft delete
      (IsDelete = 1, DeletedDate, DeletedBy)

    Column quirks worth knowing:
      - The table name is exactly "Timesheet_Approver_setup" - underscores,
        capital "A", lower-case "setup". Map it by its exact name.
      - ApproverID is presumably a dbo.Signup.UserID (the approving user),
        ProjectID presumably dbo.TimesheetProject.ProjectID, and OrgID
        presumably dbo.Signup.OrganizationID. No foreign key was reported for
        any of them.
      - The organisation column is "OrgID" here, not "OrganizationID" as on
        dbo.Signup.
      - TimesheetApproverSetupID is IDENTITY(1,1) and the primary key: the
        database assigns it on insert. It is the only NOT NULL column.
      - Every other column is nullable, ApproverID included.
      - IsDelete is a bit, nullable - unlike dbo.Signup and
        dbo.Timesheet_Users_Project_setup, where it is an int. The add writes
        0 and the API's soft delete writes 1; the API and both procedures
        treat 0 or NULL as live.
      - IsTimesheetApprover is a bit, nullable; 1 presumably makes the user an
        approver on the project.
      - CreateDate and DeletedDate are plain datetime - local or UTC is not
        stated.
      - CreatedBy and DeletedBy are int user ids; no foreign key was reported.
      - There are no UpdatedDate / UpdatedBy columns: an edit leaves no audit
        trail on the row.

    STILL UNCONFIRMED:
      - The column position of TimesheetApproverSetupID. It is recorded
        first; if it was added with ALTER TABLE ... ADD it is last in the
        live table instead. Position does not affect the EF mapping.
      - One live approver per project is a rule of the API and of
        spc_AddEditTimesheetApproverSetup (stated by the owner on
        2026-10-20), not of the table: no unique constraint on ProjectID was
        reported.
      - Defaults, constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.Timesheet_Approver_setup', N'U') IS NOT NULL
    DROP TABLE dbo.Timesheet_Approver_setup;
GO

CREATE TABLE dbo.Timesheet_Approver_setup
(
    TimesheetApproverSetupID int        IDENTITY(1,1) NOT NULL,
    ApproverID              int         NULL,
    ProjectID               int         NULL,
    CreateDate              datetime    NULL,
    CreatedBy               int         NULL,
    IsDelete                bit         NULL,
    DeletedDate             datetime    NULL,
    DeletedBy               int         NULL,
    IsTimesheetApprover     bit         NULL,
    OrgID                   int         NULL,
    CONSTRAINT PK_Timesheet_Approver_setup PRIMARY KEY (TimesheetApproverSetupID)
);
GO
