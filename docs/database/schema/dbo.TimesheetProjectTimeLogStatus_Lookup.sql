/*
    Table: dbo.TimesheetProjectTimeLogStatus_Lookup
    Recorded: 2026-11-04 (written at the database owner's request, to be
              created by hand; confirm here once it has been applied)

    !! NOT YET APPLIED !!  The owner has not run this against PPMUAT yet. The
    entity and the two endpoints below are written against it, so until the
    CREATE TABLE has been run by hand the API will fail on it with
    "Invalid object name 'dbo.TimesheetProjectTimeLogStatus_Lookup'".
    Replace this block with a Revised: line once it is applied.

    Lookup table for the status of a single time log entry:
    dbo.TimesheetProjectTimeLog.StatusID. Reference data, owned by the
    database.

    DELIBERATELY EMPTY. No rows were supplied, and none are inserted here -
    the owner adds the statuses when they are agreed. Until then every entry's
    StatusID is NULL, and an entry saved with a StatusID reads back with a
    null statusName, because no row matches it. That is expected, not a bug.

    READ-ONLY TO THE API. Nothing writes this table; the two time log
    endpoints join it for the name only, and neither checks a StatusID
    against it - any positive id is stored as sent.

    Mapped by (read-only):
      src/ETimeSheet.Application/Models/Entities/TimesheetProjectTimeLogStatusLookup.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimesheetProjectTimeLogStatusLookupConfiguration.cs
      (TimesheetProjectRepository.GetProjectTimeLogStatusListAsync, for
      GET api/v1/TimesheetProject/get-timesheet-project-time-log and
      POST api/v1/TimesheetProject/add-edit-timesheet-project-time-log).
      Excluded from the integration tests' reset, like the other lookups, so
      whatever rows it holds survive.

    Column quirks worth knowing:
      - StatusID is NOT an IDENTITY: the ids are written explicitly, as in
        dbo.TimesheetProjectSubmissionStatus, so they can be chosen to match
        whatever dbo.TimesheetProjectTimeLog.StatusID already holds.
      - StatusName is nvarchar(50) and UNIQUE, so it is safe to look a row up
        by name.
      - No foreign key from dbo.TimesheetProjectTimeLog.StatusID to this table
        is declared, matching every other id on that table.

    STILL UNCONFIRMED:
      - The statuses themselves - which ids, which names.
      - Defaults, other constraints and indexes - none were supplied.
*/

IF OBJECT_ID(N'dbo.TimesheetProjectTimeLogStatus_Lookup', N'U') IS NOT NULL
    DROP TABLE dbo.TimesheetProjectTimeLogStatus_Lookup;
GO

CREATE TABLE dbo.TimesheetProjectTimeLogStatus_Lookup
(
    StatusID    int             NOT NULL,
    StatusName  nvarchar(50)    NOT NULL,

    CONSTRAINT PK_TimesheetProjectTimeLogStatus_Lookup
        PRIMARY KEY CLUSTERED (StatusID),
    CONSTRAINT UQ_TimesheetProjectTimeLogStatus_Lookup_StatusName
        UNIQUE (StatusName)
);
GO

-- No rows on purpose: the statuses have not been agreed. Add them here, as an
-- INSERT, once the owner has inserted them in SQL Server.
