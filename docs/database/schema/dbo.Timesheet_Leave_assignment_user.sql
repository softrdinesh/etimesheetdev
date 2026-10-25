/*
    Table: dbo.Timesheet_Leave_assignment_user
    Recorded: 2026-10-25 (sp_help column list supplied by the database owner,
              who creates the table by hand; TimesheetLeaveAssignmentUserID
              int IDENTITY(1,1) NOT NULL added as the primary key
              (PK_Timesheet_Leave_assignment_user), per the database owner)
    Revised:  2026-10-25 - UpdatedDate datetime NULL and UpdatedBy int NULL
              added after CreatedBy, per the database owner's revised
              definition.

    Which leave types each user is assigned, with the balance left of each,
    and audit and soft-delete columns.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimesheetLeaveAssignmentUser.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetLeaveAssignmentUserConfiguration.cs
      (for the one-assignment-per-user-and-leave-type check and the soft delete in
      POST api/v1/TimesheetProject/remove-timesheet-user-leave-assignment)

    Read by:
      ../procedures/dbo.spc_GetTimesheetUserLeaveAssignment.sql
      (TimesheetProjectRepository, for GET api/v1/TimesheetProject/get-timesheet-user-leave-assignment)

    Written by:
      ../procedures/dbo.spc_AddTimesheetUserLeaveAssignment.sql
      (TimesheetProjectRepository, for POST api/v1/TimesheetProject/add-timesheet-user-leave-assignment)
      ../procedures/dbo.spc_EditTimesheetUserLeaveAssignment.sql
      (TimesheetProjectRepository, for POST api/v1/TimesheetProject/edit-timesheet-user-leave-assignment)
      TimesheetProjectRepository through EF Core - the soft delete
      (IsDelete = 1, DeletedDate, DeletedBy)

    Column quirks worth knowing:
      - The table name is exactly "Timesheet_Leave_assignment_user" -
        underscores, lower-case "assignment" and "user". Map it by its exact
        name.
      - TimesheetLeaveAssignmentUserID is IDENTITY(1,1) and the primary key:
        the database assigns it on insert. It is the only NOT NULL column.
      - UserID is presumably a dbo.Signup.UserID, and LeavetypeID a
        dbo.Timesheet_Leavetype_Setup.LeaveTypeID. No foreign key was
        reported for either. Both are nullable.
      - "LeavetypeID" has a lower-case "t" here, where the leave type table
        has "LeaveTypeID". SQL Server's default collation is
        case-insensitive, but map it by its exact name.
      - BalanceLeave is decimal(4,2), the same as
        dbo.Timesheet_Leavetype_Setup.NoofLeaveAllocated: at most 99.99, and
        half days can be expressed.
      - The audit date column is "CreatedDate" here - not "CreateDate" as on
        dbo.Timesheet_Leavetype_Setup and most other tables.
      - IsDelete is a bit, nullable. NULL and 0 would both be live.
      - CreatedDate, UpdatedDate and DeletedDate are plain datetime - local
        or UTC is not stated.
      - CreatedBy, UpdatedBy and DeletedBy are int user ids; no foreign key
        was reported. All three are spelt with a capital "B".

    STILL UNCONFIRMED:
      - The column position of TimesheetLeaveAssignmentUserID. It is recorded
        first; if it was added with ALTER TABLE ... ADD it is last in the live
        table instead. Position does not affect an EF mapping.
      - One live assignment per user and leave type is a rule of the API and
        of spc_AddTimesheetUserLeaveAssignment / spc_EditTimesheetUserLeaveAssignment (chosen 2026-10-25, not
        yet confirmed by the owner), not of the table: no unique constraint
        on (UserID, LeavetypeID) was supplied.
      - Where BalanceLeave starts: the add sets it to the leave type's
        NoofLeaveAllocated unless a balance is sent (chosen 2026-10-25, not
        yet confirmed by the owner). Nothing reduces it yet - there is no
        leave application.
      - Defaults, other constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.Timesheet_Leave_assignment_user', N'U') IS NOT NULL
    DROP TABLE dbo.Timesheet_Leave_assignment_user;
GO

CREATE TABLE dbo.Timesheet_Leave_assignment_user
(
    TimesheetLeaveAssignmentUserID  int             IDENTITY(1,1) NOT NULL,
    UserID                          int             NULL,
    LeavetypeID                     int             NULL,
    IsDelete                        bit             NULL,
    CreatedDate                     datetime        NULL,
    CreatedBy                       int             NULL,
    UpdatedDate                     datetime        NULL,
    UpdatedBy                       int             NULL,
    DeletedDate                     datetime        NULL,
    DeletedBy                       int             NULL,
    BalanceLeave                    decimal(4,2)    NULL,
    CONSTRAINT PK_Timesheet_Leave_assignment_user PRIMARY KEY (TimesheetLeaveAssignmentUserID)
);
GO
