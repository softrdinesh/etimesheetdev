/*
    Table: dbo.TimeSheetSubmissionAuditLog
    Recorded: 2026-09-30 (column list supplied by the database owner)
    Revised:  2026-09-30 - TimeSheetSubmissionID changed by the database owner
              from int to varchar(20), collation SQL_Latin1_General_CP1_CI_AS.
    Revised:  2026-09-30 - owner confirmed TimeSheetSubmissionAuditLogID is the
              primary key and an auto-incrementing sequence (IDENTITY).

    One row per status change on a submitted timesheet - who moved it to which
    status, and when.

    Mapped by:
      src/ETimeSheet.Application/Models/Entities/TimeSheetSubmissionAuditLog.cs
      src/ETimeSheet.Infrastructure/Data/Configurations/TimeSheetSubmissionAuditLogConfiguration.cs

    Written by:
      src/ETimeSheet.Infrastructure/Repositories/SheetSubmissionRepository.cs
      (one row per submit, via POST /api/v1/SheetSubmission/submit-timesheet)

    Column meanings that are not obvious from the type:
      - StatusID  the status the sheet moved to - the same values as
                  dbo.TimesheetSubmission.StatusID: 1 = Submitted,
                  2 = Approved, 3 = Rejected.

    Column quirks worth knowing:
      - Every column is NOT NULL.
      - TimeSheetSubmissionAuditLogID is IDENTITY: a sequence number and nothing
        more. The database generates it; the API never supplies one.
      - CreateDate is plain datetime, like dbo.TimeLog and
        dbo.TimesheetMasterSetup.
      - TimeSheetSubmissionID is varchar(20), not int, despite its name. It
        holds a timesheet code - dbo.TimesheetSubmission.Timesheetcode, which is
        varchar(15), so every code fits.

    STILL UNCONFIRMED:
      - The primary key's constraint name, and whether it is clustered (SQL
        Server's default for a primary key, so recorded as such). The name
        PK_TimeSheetSubmissionAuditLog below is a placeholder.
      - The IDENTITY seed and increment; (1,1) is recorded as the default.
      - Whether TimeSheetSubmissionID has a foreign key, and to what.
      - Whether any column carries a default.
*/

IF OBJECT_ID(N'dbo.TimeSheetSubmissionAuditLog', N'U') IS NOT NULL
    DROP TABLE dbo.TimeSheetSubmissionAuditLog;
GO

CREATE TABLE dbo.TimeSheetSubmissionAuditLog
(
    TimeSheetSubmissionAuditLogID  int          IDENTITY(1,1) NOT NULL,
    TimeSheetSubmissionID          varchar(20)  COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
    StatusID                       int          NOT NULL,
    CreatedBy                      int          NOT NULL,
    CreateDate                     datetime     NOT NULL,

    CONSTRAINT PK_TimeSheetSubmissionAuditLog PRIMARY KEY CLUSTERED (TimeSheetSubmissionAuditLogID)
);
GO
