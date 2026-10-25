/*
    Table: dbo.TimesheetLeaveStatus_Lookup
    Recorded: 2026-10-25 (sp_help column list supplied by the database owner,
              who creates the table by hand)

    Lookup table for leave statuses: a numeric Value and its display Text.
    Fixed reference data: the three rows at the bottom of this file are the
    whole table and are inserted with the schema, so the throwaway test
    database built from this folder holds them too.

    The statuses of dbo.Timesheet_Leave_User_apply.LeaveStatus, as stated
    by the owner on 2026-10-25. The API reads status names from here rather
    than hard-coding them.

    Mapped by (read-only):
      src/ETimeSheet.Application/Models/Entities/TimesheetLeaveStatusLookup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetLeaveStatusLookupConfiguration.cs
      (TimesheetProjectRepository.GetLeaveStatusValueAsync looks a status up
      by Text). Excluded from the integration tests' reset, like the other
      lookups, so its seed rows survive.

    Read by:
      ../procedures/dbo.spc_GetTimesheetUserLeaveApply.sql
      ../procedures/dbo.spc_AddEditTimesheetUserLeaveApply.sql

    Column quirks worth knowing:
      - ID is the only NOT NULL column. Value and Text are both nullable.
      - Value is a plain int, separate from ID - which of the two other tables
        will store is not stated.

    STILL UNCONFIRMED:
      - Whether ID is IDENTITY. The sp_help column list does not report
        identity either way, so none is recorded below.
      - Whether ID is the primary key. No key was reported, so none is
        recorded below.
      - Whether Value or Text is unique - no constraint was supplied.
      - Defaults, constraints and indexes - none were supplied.

    Lengths below are characters. sp_help reports nvarchar lengths in BYTES, so
    the 100 it prints for Text is 50 characters.
*/

IF OBJECT_ID(N'dbo.TimesheetLeaveStatus_Lookup', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetLeaveStatus_Lookup;
GO

CREATE TABLE dbo.TimesheetLeaveStatus_Lookup
(
    ID       int             NOT NULL,
    Value    int             NULL,
    Text     nvarchar(50)    NULL
);
GO

INSERT INTO dbo.TimesheetLeaveStatus_Lookup (ID, Value, Text)
VALUES
    (1, 1, N'Submitted'),
    (2, 2, N'Approved'),
    (3, 3, N'Rejected');
GO
