/*
    Table: dbo.TimesheetProjectStatus
    Recorded: 2026-10-13 (definition supplied by the database owner, who
              creates and populates the table by hand)

    Lookup of the statuses a project can be in: Open, Closed, Pending.

    Mapped by:
      (nothing yet)

    Read by:
      ../procedures/dbo.spc_GetTimesheetProject.sql and
      ../procedures/dbo.spc_AddEditTimesheetProject.sql - LEFT JOINed on
      dbo.TimesheetProject.Status for StatusName.
      ../procedures/dbo.spc_GetTimesheetProjectStatus.sql - the whole list,
      for GET api/v1/TimesheetProject/get-timesheet-project-status-list.

    Populated by:
      ../data/dbo.TimesheetProjectStatus_Insert.sql - run by hand, once, after
      the table is created. The INSERT at the bottom of this file is the same
      rows, so the throwaway test database built from this folder holds them
      too.

    Column quirks worth knowing:
      - StatusID is IDENTITY(1,1). The rows are inserted with explicit ids
        under IDENTITY_INSERT (1 Open, 2 Closed, 3 Pending), so an id means
        the same status in every database.
      - dbo.TimesheetProject.Status is presumably a StatusID from this table;
        no foreign key was reported.

    STILL UNCONFIRMED:
      - Whether StatusID is the primary key. It is IDENTITY(1,1), but no key
        was reported, so none is recorded below.
      - Whether Status is unique - no constraint was supplied.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectStatus', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectStatus;
GO

CREATE TABLE dbo.TimesheetProjectStatus
(
    StatusID       int             IDENTITY(1,1) NOT NULL,
    Status         nvarchar(20)    NOT NULL
);
GO

SET IDENTITY_INSERT dbo.TimesheetProjectStatus ON;

INSERT INTO dbo.TimesheetProjectStatus (StatusID, Status)
VALUES
    (1, N'Open'),
    (2, N'Closed'),
    (3, N'Pending');

SET IDENTITY_INSERT dbo.TimesheetProjectStatus OFF;
GO
