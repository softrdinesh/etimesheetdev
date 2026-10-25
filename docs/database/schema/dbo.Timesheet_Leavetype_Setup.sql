/*
    Table: dbo.Timesheet_Leavetype_Setup
    Recorded: 2026-10-25 (sp_help column list supplied by the database owner,
              who creates the table by hand; LeaveTypeID is IDENTITY(1,1)
              as stated by the owner)

    The leave types of each organisation - "Casual Leave", "Sick Leave" ... -
    with the number of days allocated for each, audit and soft-delete
    columns.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetLeaveTypeSetup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetLeaveTypeSetupConfiguration.cs
      (for the soft delete in POST api/v1/TimesheetProject/remove-timesheet-leave-setup)

    Read by:
      ../procedures/dbo.spc_GetTimesheetLeaveSetup.sql
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-leave-setup)

    Written by:
      ../procedures/dbo.spc_AddEditTimesheetLeaveSetup.sql
      (TimesheetProjectRepository, for POST api/v1/TimesheetProject/add-edit-timesheet-leave-setup)
      TimesheetProjectRepository through EF Core - the soft delete
      (IsDelete = 1, DeletedDate, DeletedBy)

    Column quirks worth knowing:
      - The table name is exactly "Timesheet_Leavetype_Setup" - underscores,
        lower-case "type", capital "S" on "Setup". Map it by its exact name.
      - LeaveTypeID is IDENTITY(1,1): the database assigns it on insert.
        It is the only NOT NULL column. Every other column is nullable,
        LeaveType included.
      - NoofLeaveAllocated is decimal(4,2): at most 99.99, and half days
        (0.5) can be expressed. The period it covers (per year?) is not
        stated.
      - OrgID is presumably dbo.Signup.OrganizationID; no foreign key was
        reported. As on dbo.Timesheet_Approver_setup, the column is "OrgID",
        not "OrganizationID".
      - IsDelete is a bit, nullable - as on dbo.Timesheet_Approver_setup,
        unlike dbo.Signup where it is an int. NULL and 0 would both be live.
      - CreateDate, UpdatedDate and DeletedDate are plain datetime - local or
        UTC is not stated.
      - CreatedBy, UpdatedBy and DeletedBy are int user ids; no foreign key
        was reported. All three are spelt with a capital "B" here.

    STILL UNCONFIRMED:
      - Whether LeaveTypeID is the primary key. It is IDENTITY(1,1), but no
        key was reported, so none is recorded below.
      - Whether LeaveType is unique per OrgID - no constraint was supplied.
      - Defaults, constraints and indexes - none were supplied.

    Lengths below are characters. sp_help reports nvarchar lengths in BYTES, so
    the 100 it prints for LeaveType is 50 characters.
*/

IF OBJECT_ID(N'dbo.Timesheet_Leavetype_Setup', N'U') IS NOT NULL
    DROP TABLE dbo.Timesheet_Leavetype_Setup;
GO

CREATE TABLE dbo.Timesheet_Leavetype_Setup
(
    LeaveTypeID             int             IDENTITY(1,1) NOT NULL,
    LeaveType               nvarchar(50)    COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
    NoofLeaveAllocated      decimal(4,2)    NULL,
    OrgID                   int             NULL,
    CreateDate              datetime        NULL,
    CreatedBy               int             NULL,
    IsDelete                bit             NULL,
    UpdatedDate             datetime        NULL,
    UpdatedBy               int             NULL,
    DeletedDate             datetime        NULL,
    DeletedBy               int             NULL
);
GO
