/*
    Table: dbo.Timesheet_Leave_User_apply
    Recorded: 2026-10-25 (sp_help column list supplied by the database owner,
              who creates the table by hand; TimesheetLeaveUserApplyID int
              IDENTITY(1,1) NOT NULL added as the primary key
              (PK_Timesheet_Leave_User_apply), per the database owner)
    Revised:  2026-10-25 - OrgID int NULL added after UserID, per the
              database owner's revised definition.

    The leave each user applies for - the dates, the number of days, the
    leave type - and its approval or rejection, with comments from both
    sides.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetLeaveUserApply.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetLeaveUserApplyConfiguration.cs
      (for the approval check on edit, and the hard delete - with the
      balance give-back for approved leave - in
      POST api/v1/TimesheetProject/remove-timesheet-user-leave-apply)

    Read by:
      ../procedures/dbo.spc_GetTimesheetUserLeaveApply.sql
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-user-leave-apply)

    Written by:
      ../procedures/dbo.spc_AddEditTimesheetUserLeaveApply.sql
      (TimesheetProjectRepository, for POST api/v1/TimesheetProject/add-edit-timesheet-user-leave-apply)
      TimesheetProjectRepository through EF Core - the hard delete

    Column quirks worth knowing:
      - The table name is exactly "Timesheet_Leave_User_apply" - underscores,
        capital "L" and "U", lower-case "apply". Map it by its exact name.
      - TimesheetLeaveUserApplyID is IDENTITY(1,1) and the primary key: the
        database assigns it on insert.
      - LeaveID is a SEPARATE int NOT NULL column - the only other NOT NULL
        one - and is not an identity, so every insert must supply it. What it
        holds (a per-user or per-organisation leave number?) is not stated.
      - UserID is presumably a dbo.Signup.UserID, and LeavetypeID a
        dbo.Timesheet_Leavetype_Setup.LeaveTypeID ("LeavetypeID", lower-case
        "t", as on dbo.Timesheet_Leave_assignment_user). No foreign key was
        reported for either.
      - OrgID is the organisation the application belongs to - presumably
        dbo.Signup.OrganizationID of the user; no foreign key was reported.
        The add writes it; the list, edit and remove filter on it. As on
        dbo.Timesheet_Approver_setup, the column is "OrgID".
      - LeaveStatus takes its values from dbo.TimesheetLeaveStatus_Lookup
        (1 Submitted, 2 Approved, 3 Rejected), as stated by the owner on
        2026-10-25. The status name is read from the lookup's Text, never
        hard-coded. No foreign key was reported.
      - StartDate and EndDate are datetime, not date, so a time of day can be
        stored alongside. Local or UTC is not stated.
      - NoofDays is decimal(4,2), as NoofLeaveAllocated and BalanceLeave:
        at most 99.99, and half days can be expressed.
      - UserComments and ApproverComments are nvarchar(max) (sp_help prints
        length -1).
      - Approval and rejection each have their own date and user columns
        (ApprovedDate / ApprovedBy, RejectedDate / RejectedBy). The audit
        date column is "CreatedDate", as on
        dbo.Timesheet_Leave_assignment_user.
      - There is no IsDelete and no UpdatedDate / UpdatedBy: an application
        cannot be soft-deleted, and a change to one leaves no audit trail
        beyond the approval / rejection columns.
      - Every column but the two ids is nullable.

    STILL UNCONFIRMED:
      - What LeaveID is. Until it is stated, spc_AddEditTimesheetUserLeaveApply
        assigns the highest LeaveID in the table plus one (chosen 2026-10-25).
      - The column position of TimesheetLeaveUserApplyID. It is recorded
        first; if it was added with ALTER TABLE ... ADD it is last in the
        live table instead. Position does not affect an EF mapping.
      - Defaults, other constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.Timesheet_Leave_User_apply', N'U') IS NOT NULL
    DROP TABLE dbo.Timesheet_Leave_User_apply;
GO

CREATE TABLE dbo.Timesheet_Leave_User_apply
(
    TimesheetLeaveUserApplyID   int             IDENTITY(1,1) NOT NULL,
    LeaveID                     int             NOT NULL,
    UserID                      int             NULL,
    OrgID                       int             NULL,
    StartDate                   datetime        NULL,
    EndDate                     datetime        NULL,
    NoofDays                    decimal(4,2)    NULL,
    LeavetypeID                 int             NULL,
    CreatedDate                 datetime        NULL,
    CreatedBy                   int             NULL,
    ApprovedDate                datetime        NULL,
    ApprovedBy                  int             NULL,
    RejectedDate                datetime        NULL,
    RejectedBy                  int             NULL,
    UserComments                nvarchar(max)   COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
    ApproverComments            nvarchar(max)   COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
    LeaveStatus                 int             NULL,
    CONSTRAINT PK_Timesheet_Leave_User_apply PRIMARY KEY (TimesheetLeaveUserApplyID)
);
GO
