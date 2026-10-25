/*
    Table: dbo.Timesheet_Approver_setup
    Recorded: 2026-10-20 (sp_help column list supplied by the database owner;
              the table already exists)
    Revised:  2026-10-20 - TimesheetApproverSetupID int IDENTITY(1,1) NOT
              NULL added as the primary key (PK_Timesheet_Approver_setup), per
              the database owner.
    Revised:  2026-10-26 - per the owner, an organisation has ONE leave
              approver, whatever the project; ProjectID may be NULL on a
              leave-only row.
    Revised:  2026-10-25 - IsLeaveApprover bit NULL added as the last column
              by the database owner. A project now has one approver PER
              AREA - one timesheet approver and one leave approver - rather
              than one setup per project.

    The timesheet approver of each project and the leave approver of each
    organisation - at most one live one of each - with audit and soft-delete
    columns.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetApproverSetup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetApproverSetupConfiguration.cs
      (for the one-approver-per-project-per-area checks and the soft delete in
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
      - IsTimesheetApprover is a bit, nullable; 1 makes the user the approver
        of the project's timesheets. Only that row may approve or reject them.
      - IsLeaveApprover is a bit, nullable (added 2026-10-25); 1 makes the
        user the organisation's leave approver - one per organisation
        (stated by the owner on 2026-10-26), whatever ProjectID says.
      - One row may carry both flags - the same user approving both areas.
        NULL counts as 0 for both.
      - ProjectID may be NULL on a leave-only row. A timesheet approver
        always has a project.
      - CreateDate and DeletedDate are plain datetime - local or UTC is not
        stated.
      - CreatedBy and DeletedBy are int user ids; no foreign key was reported.
      - There are no UpdatedDate / UpdatedBy columns: an edit leaves no audit
        trail on the row.

    STILL UNCONFIRMED:
      - The column position of TimesheetApproverSetupID. It is recorded
        first; if it was added with ALTER TABLE ... ADD it is last in the
        live table instead. Position does not affect the EF mapping.
      - One live timesheet approver per project and one live leave approver
        per organisation (stated by the owner on 2026-10-25 and 2026-10-26)
        are rules of the API and of spc_AddEditTimesheetApproverSetup, not
        of the table: no unique constraint was reported. Filtered unique
        indexes - on ProjectID WHERE IsTimesheetApprover = 1, and on OrgID
        WHERE IsLeaveApprover = 1, live rows only - would make the table
        enforce them; that is the owner's call.
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
    IsLeaveApprover         bit         NULL,
    CONSTRAINT PK_Timesheet_Approver_setup PRIMARY KEY (TimesheetApproverSetupID)
);
GO
